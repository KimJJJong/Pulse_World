using Client.Data;
using GameShared.Data;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public sealed class P2PHostController : MonoBehaviour
{
    private const int MaxCatchUpBeatsPerUpdate = 10;

    public static P2PHostController Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(P2PHostController));
                _instance = go.AddComponent<P2PHostController>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    private static P2PHostController _instance;

    public bool IsHost { get; private set; }
    public int HostActorId { get; private set; }

    private readonly List<ScheduledSkill> _activeSkills = new();
    private readonly Dictionary<int, PlayerCombatSnapshot> _combatSnapshots = new();
    private readonly Dictionary<int, long> _lastAcceptedBeatByActor = new();
    private readonly object _snapshotLock = new();
    private readonly object _commandLock = new();
    private readonly BeatCommandScheduler _beatScheduler = new();
    private readonly BeatCommandScheduler _delayedScheduler = new();
    private readonly List<QueuedCombatCommand> _commandBuffer = new(16);

    private readonly List<SC_BeatActions.BeatActionResult> _batchedBeatResults = new();
    private readonly HashSet<int> _batchedDeadEntities = new();
    private readonly Dictionary<int, Vector2Int> _hostPositions = new();

    private long _lastProcessedBeat = long.MinValue;
    private long _lastJudgeWindowBeat = long.MinValue;
    private bool _snapshotRefreshInFlight;
    private bool _resultSubmitted;
    private long _totalDamageDealt;
    private string _activeSessionKey = "";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ResetForMatchEnd()
    {
        IsHost = false;
        HostActorId = 0;
        _lastProcessedBeat = long.MinValue;
        _lastJudgeWindowBeat = long.MinValue;
        _resultSubmitted = false;
        _totalDamageDealt = 0;
        _snapshotRefreshInFlight = false;
        _activeSessionKey = "";

        _activeSkills.Clear();
        _lastAcceptedBeatByActor.Clear();

        lock (_commandLock)
        {
            _beatScheduler.Clear();
            _delayedScheduler.Clear();
            _commandBuffer.Clear();
        }

        lock (_snapshotLock)
        {
            _combatSnapshots.Clear();
        }

        if (RhythmInputController.HasInstance)
            RhythmInputController.Instance.IsInputBlocked = false;

        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log("[P2PHostController] Match state reset");
    }

    public void SetHostMode(bool isHost)
    {
        if (IsHost == isHost)
        {
            if (isHost)
                MaybeRefreshSnapshots();
            return;
        }

        IsHost = isHost;

        if (!IsHost)
        {
            if (RhythmInputController.HasInstance)
                RhythmInputController.Instance.IsInputBlocked = false;

            ResetCombatState();
            if (P2PDebugConfig.TraceHostFlow)
                Debug.Log("[P2PHostController] Host mode disabled");
            return;
        }

        SyncSessionKey();
        P2PCombatContentCache.WarmUpSkills();
        MaybeRefreshSnapshots();
        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] Host mode enabled actor={HostActorId}");
    }

    public void SetHostActorId(int actorId)
    {
        HostActorId = actorId;

        if (IsHost)
            MaybeRefreshSnapshots();

        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] Host actor set to {actorId}");
    }

    public void EnqueueLocalActionRequest(CS_ActionRequest req)
    {
        if (!IsHost || req == null)
            return;

        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] EnqueueLocal actor={req.ActorId} action={(ActionKind)req.ActionKind} slot={req.SlotIndex} target=({req.TargetX},{req.TargetY}) sendMs={req.ClientSendTimeMs}");

        HandleActionRequest(req, req.ActorId, fromLocalSource: true);
    }

    public void EnqueueGuestActionRequest(CS_P2PPayload pkt)
    {
        if (!IsHost || pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return;

        if (!TryDecodeActionRequest(pkt.Payload, out var req))
            return;

        int actorId = pkt.SenderActorId > 0 ? pkt.SenderActorId : req.ActorId;
        HandleActionRequest(req, actorId, fromLocalSource: false);
    }

    public void EnqueueAiAction(
        int actorId,
        ActionKind actionKind,
        int targetX,
        int targetY,
        float rotation,
        string skillId,
        long executeBeat)
    {
        if (!IsHost || RhythmClient.Instance == null)
            return;

        var req = new CS_ActionRequest
        {
            ActorId = actorId,
            ActionKind = (int)actionKind,
            SlotIndex = -1,
            TargetX = targetX,
            TargetY = targetY,
            Rotation = rotation,
            ClientSendTimeMs = RhythmClient.Instance.GetBeatTimeMs(executeBeat)
        };

        EnqueueScheduledCommand(new QueuedCombatCommand
        {
            Request = req,
            ActorId = actorId,
            FromLocalSource = false,
            BypassInputGuards = true,
            SkillIdOverride = skillId ?? "",
            ForcedExecuteBeat = executeBeat
        });
    }

    public void OnLocalActionRequest(CS_ActionRequest req)
    {
        if (!IsHost || req == null)
            return;

        EnqueueLocalActionRequest(req);
    }

    public void OnReceiveGuestInput(CS_P2PPayload pkt)
    {
        EnqueueGuestActionRequest(pkt);
    }

    private float _debugTimer = 0f;

    private void Update()
    {
        _debugTimer += Time.deltaTime;
        if (_debugTimer > 1f)
        {
            _debugTimer = 0f;
        }

        if (!IsHost)
            return;

        if (RhythmClient.Instance == null || ClientGameState.Instance == null)
            return;

        long currentBeat = RhythmClient.Instance.GetCurrentBeatIndex();
        if (currentBeat < 0)
            return;

        if (_lastProcessedBeat == long.MinValue)
            _lastProcessedBeat = currentBeat - 1;

        var contentDirector = P2PContentDirector.HasInstance ? P2PContentDirector.Instance : null;
        int processedBeats = 0;
        while (_lastProcessedBeat < currentBeat && processedBeats < MaxCatchUpBeatsPerUpdate)
        {
            _lastProcessedBeat++;
            contentDirector?.PrepareHostBeat(_lastProcessedBeat);
            ProcessScheduledCommandsAtBeat(_lastProcessedBeat);
            contentDirector?.FinalizeHostBeat(_lastProcessedBeat);
            ProcessSkillsAtBeat(_lastProcessedBeat);
            
            FlushBatchedBeatResults(_lastProcessedBeat);
            
            processedBeats++;
        }

        ProcessJudgeWindowCallbacks();
    }

    private void ProcessJudgeWindowCallbacks()
    {
        if (RhythmClient.Instance == null)
            return;

        if (_lastJudgeWindowBeat == long.MinValue)
            _lastJudgeWindowBeat = _lastProcessedBeat - 1;

        long now = RhythmClient.Instance.GetCurrentServerTimeMs();
        double judgeWindowMs = RhythmClient.Instance.judgeWindowMs > 0 ? RhythmClient.Instance.judgeWindowMs : 100.0;
        int processedBeats = 0;

        while (processedBeats < MaxCatchUpBeatsPerUpdate)
        {
            long nextBeat = _lastJudgeWindowBeat + 1;
            long windowEndMs = RhythmClient.Instance.GetBeatTimeMs(nextBeat) + (long)Math.Ceiling(judgeWindowMs);
            if (now < windowEndMs)
                break;

            _lastJudgeWindowBeat = nextBeat;
            ProcessDelayedCommandsAtBeat(nextBeat);
            
            FlushBatchedBeatResults(nextBeat);
            
            processedBeats++;
        }
    }

    private void EnqueueScheduledCommand(QueuedCombatCommand command)
    {
        if (command == null)
            return;

        lock (_commandLock)
        {
            long beat = command.ForcedExecuteBeat ?? 0;
            var scheduler = IsImmediateBeatCommand(command) ? _beatScheduler : _delayedScheduler;
            scheduler.Enqueue(beat, command);
        }
    }

    private void ProcessScheduledCommandsAtBeat(long beat)
    {
        _commandBuffer.Clear();
        lock (_commandLock)
        {
            _beatScheduler.PopActions(beat, _commandBuffer);
        }

        if (_commandBuffer.Count == 0)
            return;

        foreach (var command in _commandBuffer)
        {
            if (command?.Request == null)
                continue;

            ActivateScheduledCommand(command, beat);
        }
    }

    private void ProcessDelayedCommandsAtBeat(long beat)
    {
        _commandBuffer.Clear();
        lock (_commandLock)
        {
            _delayedScheduler.PopActions(beat, _commandBuffer);
        }

        if (_commandBuffer.Count == 0)
            return;

        foreach (var command in _commandBuffer)
        {
            if (command?.Request == null)
                continue;

            ActivateScheduledCommand(command, beat);
        }
    }

    private static bool IsImmediateBeatCommand(QueuedCombatCommand command)
    {
        if (command?.Request == null)
            return false;

        var kind = (ActionKind)command.Request.ActionKind;
        return kind == ActionKind.Move || kind == ActionKind.Wait;
    }

    private void HandleActionRequest(
        CS_ActionRequest req,
        int actorId,
        bool fromLocalSource,
        string skillIdOverride = "",
        bool bypassInputGuards = false,
        long? forcedExecuteBeat = null)
    {
        if (!IsHost || req == null || RhythmClient.Instance == null || ClientGameState.Instance == null)
        {
            if (fromLocalSource)
                Debug.LogWarning($"[P2PHostController] DropLocal action={(ActionKind?)req?.ActionKind} reason=HostOrDepsMissing isHost={IsHost} rhythm={(RhythmClient.Instance != null)} gs={(ClientGameState.Instance != null)}");
            return;
        }

        if (!ClientGameState.Instance.TryGetEntity(actorId, out var actorInfo))
        {
            if (fromLocalSource)
                Debug.LogWarning($"[P2PHostController] DropLocal actor={actorId} action={(ActionKind)req.ActionKind} reason=ActorNotFound");
            return;
        }

        if (actorInfo.Hp <= 0)
        {
            if (fromLocalSource)
                Debug.LogWarning($"[P2PHostController] DropLocal actor={actorId} action={(ActionKind)req.ActionKind} reason=ActorDead hp={actorInfo.Hp}");
            return;
        }

        if (!bypassInputGuards && RhythmClient.Instance.GetCurrentBeatIndex() < 0)
        {
            if (fromLocalSource)
                Debug.LogWarning($"[P2PHostController] DropLocal actor={actorId} action={(ActionKind)req.ActionKind} reason=CurrentBeatInvalid");
            return;
        }

        long executeBeat;
        if (forcedExecuteBeat.HasValue)
        {
            executeBeat = forcedExecuteBeat.Value;
        }
        else
        {
            executeBeat = RhythmClient.Instance.GetNearestBeatIndex(req.ClientSendTimeMs);
        }

        if (!bypassInputGuards)
        {
            long judgeTimeMs = RhythmClient.Instance.GetBeatTimeMs(executeBeat);
            long diffMs = Math.Abs(req.ClientSendTimeMs - judgeTimeMs);
            double judgeWindowMs = RhythmClient.Instance.judgeWindowMs > 0 ? RhythmClient.Instance.judgeWindowMs : 100.0;

            if (diffMs > judgeWindowMs)
            {
                if (P2PDebugConfig.TraceHostFlow)
                    Debug.Log($"[P2PHostController] Reject action actor={actorId} diff={diffMs}ms window={judgeWindowMs:F0}ms");
                if (fromLocalSource)
                    //Debug.LogWarning($"[P2PHostController] RejectLocal actor={actorId} action={(ActionKind)req.ActionKind} reason=OutOfJudgeWindow beat={executeBeat} diffMs={diffMs} windowMs={judgeWindowMs:F0}");
                return;
            }

            if (!TryConsumeActionBeat(actorId, executeBeat))
            {
                if (P2PDebugConfig.TraceHostFlow)
                    Debug.Log($"[P2PHostController] Duplicate action blocked actor={actorId} beat={executeBeat}");
                if (fromLocalSource)
                    Debug.LogWarning($"[P2PHostController] RejectLocal actor={actorId} action={(ActionKind)req.ActionKind} reason=DuplicateBeat executeBeat={executeBeat}");
                return;
            }
        }

        if (req.ActionKind == (int)ActionKind.Move)
        {
            ProcessImmediateMoveAction(actorId, req, executeBeat, actorInfo);
            return;
        }

        if (req.ActionKind == (int)ActionKind.Wait)
            return;

        if (req.ActionKind != (int)ActionKind.Attack && req.ActionKind != (int)ActionKind.Skill)
            return;

        string skillId = ResolveSkillId(actorId, req.SlotIndex, req.ActionKind, skillIdOverride);
        long startTick = executeBeat * 480;
        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] AcceptLocal actor={actorId} action={(ActionKind)req.ActionKind} skill={skillId} beat={executeBeat} startTick={startTick} target=({req.TargetX},{req.TargetY})");
        BroadcastInstantSkill(actorId, skillId, req.Rotation, startTick, fromLocalSource);

        EnqueueScheduledCommand(new QueuedCombatCommand
        {
            Request = req,
            ActorId = actorId,
            FromLocalSource = fromLocalSource,
            SkillIdOverride = skillId,
            ForcedExecuteBeat = executeBeat,
            InstantBroadcasted = true
        });
    }

    private void ActivateScheduledCommand(QueuedCombatCommand command, long beat)
    {
        if (command?.Request == null || ClientGameState.Instance == null)
            return;

        int actorId = command.ActorId > 0 ? command.ActorId : command.Request.ActorId;
        if (!ClientGameState.Instance.TryGetEntity(actorId, out var actorInfo) || actorInfo.Hp <= 0)
            return;

        var kind = (ActionKind)command.Request.ActionKind;
        if (kind == ActionKind.Move)
        {
            ProcessMoveAction(actorId, command.Request, beat, actorInfo);
            return;
        }

        if (kind == ActionKind.Wait)
            return;

        if (kind != ActionKind.Attack && kind != ActionKind.Skill)
            return;

        string skillId = ResolveSkillId(actorId, command.Request.SlotIndex, command.Request.ActionKind, command.SkillIdOverride);
        long startTick = beat * 480;

        if (!command.InstantBroadcasted)
            BroadcastInstantSkill(actorId, skillId, command.Request.Rotation, startTick, command.FromLocalSource);

        var scheduled = new ScheduledSkill
        {
            Request = command.Request,
            ActorId = actorId,
            SkillId = skillId,
            StartBeat = beat,
            StartTick = startTick,
            Rotation = command.Request.Rotation,
            FromLocalSource = command.FromLocalSource,
            SkillDef = LoadSkillDefinition(skillId)
        };

        if (scheduled.SkillDef == null || !HasGameplayEvents(scheduled.SkillDef))
        {
            ProcessFallbackDamage(scheduled, actorInfo, beat);
            return;
        }

        _activeSkills.Add(scheduled);
        ProcessSkillsAtBeat(beat);
    }

    private void ProcessDueSkills(long currentBeat)
    {
        if (_activeSkills.Count == 0)
        {
            _lastProcessedBeat = Math.Max(_lastProcessedBeat, currentBeat);
            return;
        }

        if (_lastProcessedBeat == long.MinValue)
            _lastProcessedBeat = currentBeat - 1;

        while (_lastProcessedBeat < currentBeat)
        {
            _lastProcessedBeat++;
            ProcessSkillsAtBeat(_lastProcessedBeat);
        }
    }

    private void ProcessSkillsAtBeat(long beat)
    {
        if (_activeSkills.Count == 0 || ClientGameState.Instance == null)
            return;

        for (int i = _activeSkills.Count - 1; i >= 0; i--)
        {
            var scheduled = _activeSkills[i];

            if (!ClientGameState.Instance.TryGetEntity(scheduled.ActorId, out var actorInfo) || actorInfo.Hp <= 0)
            {
                _activeSkills.RemoveAt(i);
                continue;
            }

            var dueEvents = CollectDueEvents(scheduled, beat);
            if (dueEvents.Count == 0)
            {
                if (IsSkillFinished(scheduled, beat))
                    _activeSkills.RemoveAt(i);
                continue;
            }

            dueEvents.Sort((a, b) =>
            {
                int cmp = a.TriggerBeat.CompareTo(b.TriggerBeat);
                if (cmp != 0) return cmp;

                cmp = GetActionPriority(a.Event.Action).CompareTo(GetActionPriority(b.Event.Action));
                if (cmp != 0) return cmp;

                cmp = a.TrackIndex.CompareTo(b.TrackIndex);
                if (cmp != 0) return cmp;

                return a.EventIndex.CompareTo(b.EventIndex);
            });

            foreach (var entry in dueEvents)
            {
                var action = entry.Event.Action;
                if (action == null)
                    continue;

                switch (action.GetSkillActionType())
                {
                    case SkillActionType.Damage:
                        if (action is DamageAction damageAction)
                            ProcessDamageEvent(scheduled, actorInfo, beat, damageAction);
                        break;

                    case SkillActionType.Move:
                        if (action is MoveAction moveAction)
                            ProcessMoveSkillEvent(scheduled, actorInfo, beat, moveAction);
                        break;
                }

                scheduled.ExecutedEvents.Add(entry.EventKey);
            }

            if (IsSkillFinished(scheduled, beat))
                _activeSkills.RemoveAt(i);
        }
    }

    private void ProcessFallbackDamage(ScheduledSkill scheduled, ClientEntityInfo actorInfo, long beat)
    {
        int damage = ResolveDamageAmount(scheduled.SkillDef, null, scheduled.ActorId);
        var hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>();
        var deadEntities = new HashSet<int>();
        var hitTargets = new HashSet<int>();
        bool anyHit = false;

        foreach (var target in EnumerateTargetsAt(scheduled.Request.TargetX, scheduled.Request.TargetY))
        {
            if (!hitTargets.Add(target.EntityId))
                continue;

            if (!CanHitTarget(actorInfo, target, null))
                continue;

            if (ApplyDamageToTarget(target, damage, hpUpdates, deadEntities))
                anyHit = true;
        }

        BroadcastBeatResult(
            scheduled.ActorId,
            beat,
            scheduled.Request.TargetX,
            scheduled.Request.TargetY,
            scheduled.Request.TargetX,
            scheduled.Request.TargetY,
            scheduled.Rotation,
            anyHit,
            hpUpdates,
            deadEntities);
    }

    private void ProcessDamageEvent(ScheduledSkill scheduled, ClientEntityInfo actorInfo, long beat, DamageAction damageAction)
    {
        int damage = ResolveDamageAmount(scheduled.SkillDef, damageAction, scheduled.ActorId);
        var origin = GetActorOrigin(scheduled.ActorId);
        var cells = ResolveDamageCells(damageAction, origin, scheduled.Rotation, scheduled.Request.TargetX, scheduled.Request.TargetY);

        var hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>();
        var deadEntities = new HashSet<int>();
        var hitTargets = new HashSet<int>();
        bool anyHit = false;

        foreach (var cell in cells)
        {
            foreach (var target in EnumerateTargetsAt(cell.x, cell.y))
            {
                if (!hitTargets.Add(target.EntityId))
                    continue;

                if (target.EntityId == scheduled.ActorId)
                    continue;

                if (!CanHitTarget(actorInfo, target, damageAction))
                    continue;

                if (ApplyDamageToTarget(target, damage, hpUpdates, deadEntities))
                    anyHit = true;
            }
        }

        BroadcastBeatResult(scheduled.ActorId, beat, origin.x, origin.y, origin.x, origin.y, scheduled.Rotation, anyHit, hpUpdates, deadEntities);
    }

    private void ProcessMoveSkillEvent(ScheduledSkill scheduled, ClientEntityInfo actorInfo, long beat, MoveAction moveAction)
    {
        var origin = GetActorOrigin(scheduled.ActorId);
        var rotated = RotateDirForMove(moveAction.DirectionX, moveAction.DirectionY, scheduled.Rotation);

        int targetX = origin.x + rotated.X * Math.Max(1, moveAction.Distance);
        int targetY = origin.y + rotated.Y * Math.Max(1, moveAction.Distance);
        bool accepted = false;

        switch (moveAction.MoveType)
        {
            case MoveType.Dash:
                accepted = TryDashMove(scheduled.ActorId, origin.x, origin.y, rotated.X, rotated.Y, moveAction.Distance, out targetX, out targetY);
                break;

            case MoveType.Blink:
                accepted = TryBlinkMove(scheduled.ActorId, origin.x, origin.y, rotated.X, rotated.Y, moveAction.Distance, out targetX, out targetY);
                break;

            default:
                accepted = TryMoveActor(scheduled.ActorId, targetX, targetY);
                break;
        }

        if (accepted)
        {
            _hostPositions[scheduled.ActorId] = new Vector2Int(targetX, targetY);
        }

        var result = new SC_BeatActions.BeatActionResult
        {
            ActorId = scheduled.ActorId,
            ActionKind = (int)ActionKind.Move,
            FromX = origin.x,
            FromY = origin.y,
            ToX = targetX,
            ToY = targetY,
            Rotation = scheduled.Rotation,
            Accepted = accepted,
            hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>()
        };

        _batchedBeatResults.Add(result);
    }

    private void ProcessMoveAction(int actorId, CS_ActionRequest req, long beat, ClientEntityInfo actorInfo)
    {
        int fromX = actorInfo.X;
        int fromY = actorInfo.Y;
        int toX = req.TargetX;
        int toY = req.TargetY;

        bool accepted = TryMoveActor(actorId, toX, toY, out string rejectReason);
        if (!accepted)
        {
            toX = fromX;
            toY = fromY;
        }
        else
        {
            _hostPositions[actorId] = new Vector2Int(toX, toY);
        }

        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] MoveResult actor={actorId} beat={beat} from=({fromX},{fromY}) reqTo=({req.TargetX},{req.TargetY}) finalTo=({toX},{toY}) accepted={accepted} reason={(accepted ? "OK" : rejectReason)}");

        var result = new SC_BeatActions.BeatActionResult
        {
            ActorId = actorId,
            ActionKind = (int)ActionKind.Move,
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            Rotation = req.Rotation,
            Accepted = accepted,
            hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>()
        };

        _batchedBeatResults.Add(result);
    }

    private void ProcessImmediateMoveAction(int actorId, CS_ActionRequest req, long beat, ClientEntityInfo actorInfo)
    {
        int fromX = actorInfo.X;
        int fromY = actorInfo.Y;
        int toX = req.TargetX;
        int toY = req.TargetY;

        bool accepted = TryMoveActor(actorId, toX, toY, out string rejectReason);
        if (!accepted)
        {
            toX = fromX;
            toY = fromY;
        }
        else
        {
            _hostPositions[actorId] = new Vector2Int(toX, toY);
        }

        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] ImmediateMove actor={actorId} beat={beat} from=({fromX},{fromY}) reqTo=({req.TargetX},{req.TargetY}) finalTo=({toX},{toY}) accepted={accepted} reason={(accepted ? "OK" : rejectReason)}");

        var result = new SC_BeatActions.BeatActionResult
        {
            ActorId = actorId,
            ActionKind = (int)ActionKind.Move,
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            Rotation = req.Rotation,
            Accepted = accepted,
            hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>()
        };

        var pkt = new SC_BeatActions
        {
            BeatIndex = beat,
            beatActionResults = new List<SC_BeatActions.BeatActionResult> { result }
        };

        SendLocalAndRelay(pkt);
    }

    private void BroadcastBeatResult(
        int actorId,
        long beat,
        int fromX,
        int fromY,
        int toX,
        int toY,
        float rotation,
        bool accepted,
        List<SC_BeatActions.BeatActionResult.HpUpdate> hpUpdates,
        HashSet<int> deadEntities)
    {
        var result = new SC_BeatActions.BeatActionResult
        {
            ActorId = actorId,
            ActionKind = (int)ActionKind.Skill,
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            Rotation = rotation,
            Accepted = accepted,
            hpUpdates = hpUpdates ?? new List<SC_BeatActions.BeatActionResult.HpUpdate>()
        };

        _batchedBeatResults.Add(result);

        if (deadEntities != null)
        {
            foreach (var deadId in deadEntities)
                _batchedDeadEntities.Add(deadId);
        }
    }

    private void FlushBatchedBeatResults(long beat)
    {
        if (_batchedBeatResults.Count > 0)
        {
            var pkt = new SC_BeatActions
            {
                BeatIndex = beat,
                beatActionResults = new List<SC_BeatActions.BeatActionResult>(_batchedBeatResults)
            };
            BroadcastAndRelay(pkt);
            _batchedBeatResults.Clear();
        }

        if (_batchedDeadEntities.Count > 0)
        {
            foreach (var deadId in _batchedDeadEntities)
            {
                BroadcastAndRelay(new SC_EntityDespawn { BeatIndex = beat, EntityId = deadId });
            }
            _batchedDeadEntities.Clear();
        }
    }

    private void BroadcastBeatPacket(SC_BeatActions pkt)
    {
        if (pkt == null)
            return;

        BroadcastAndRelay(pkt);
    }

    public void SendLocalAndRelay(IPacket packet)
    {
        BroadcastAndRelay(packet);
    }

    private void BroadcastAndRelay(IPacket packet)
    {
        var bridge = P2PRelayClientBridge.Instance;
        if (packet == null || !bridge.IsRelayMode)
            return;

        bridge.DispatchLocal(packet);
        bridge.SendWrappedPacket(packet);
    }

    private void BroadcastInstantSkill(int actorId, string skillId, float rotation, long startTick, bool fromLocalSource)
    {
        var pkt = new SC_ActionInstantBroadcast
        {
            ActorId = actorId,
            ActionKind = (int)ActionKind.Skill,
            SkillId = skillId ?? "",
            Rotation = rotation,
            StartTick = startTick
        };

        var bridge = P2PRelayClientBridge.Instance;
        bridge.DispatchLocal(pkt);
        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] LocalInstantSkill actor={actorId} skill={skillId} startTick={startTick} rot={rotation:F0}");
        bridge.SendWrappedPacket(pkt);
    }

    private bool IsTileOccupied(int targetX, int targetY, int ignoreActorId)
    {
        foreach (var kv in _hostPositions)
        {
            if (kv.Key == ignoreActorId) continue;
            if (kv.Value.x == targetX && kv.Value.y == targetY)
                return true;
        }

        if (ClientGameState.Instance != null)
        {
            foreach (var entity in ClientGameState.Instance.EnumerateEntities())
            {
                if (entity.Hp <= 0 || entity.EntityId == ignoreActorId)
                    continue;

                if (_hostPositions.ContainsKey(entity.EntityId))
                    continue;

                if (entity.X == targetX && entity.Y == targetY)
                    return true;
            }
        }

        return false;
    }

    private bool TryMoveActor(int actorId, int targetX, int targetY)
    {
        return TryMoveActor(actorId, targetX, targetY, out _);
    }

    private bool TryMoveActor(int actorId, int targetX, int targetY, out string reason)
    {
        var current = ClientGameState.Instance;
        if (current == null || !current.TryGetEntity(actorId, out var actor))
        {
            reason = "ActorMissing";
            return false;
        }

        int currentX = _hostPositions.TryGetValue(actorId, out var p) ? p.x : actor.X;
        int currentY = _hostPositions.TryGetValue(actorId, out p) ? p.y : actor.Y;

        if (currentX == targetX && currentY == targetY)
        {
            reason = "SameTile";
            return false;
        }

        if (!current.IsWalkable(targetX, targetY))
        {
            reason = $"BlockedTile kind={current.GetTileKind(targetX, targetY)}";
            return false;
        }

        if (IsTileOccupied(targetX, targetY, actorId))
        {
            reason = "Occupied";
            return false;
        }

        reason = "OK";
        return true;
    }

    private bool ApplyDamageToTarget(
        ClientEntityInfo target,
        int damage,
        List<SC_BeatActions.BeatActionResult.HpUpdate> hpUpdates,
        HashSet<int> deadEntities)
    {
        if (damage <= 0)
            damage = 1;

        int before = target.Hp;
        int after = Math.Max(0, before - damage);
        if (after == before)
            return false;

        hpUpdates.Add(new SC_BeatActions.BeatActionResult.HpUpdate
        {
            EntityId = target.EntityId,
            NewHp = after
        });

        _totalDamageDealt += Math.Max(0, before - after);

        if (after <= 0 && deadEntities.Add(target.EntityId))
        {
            _hostPositions.Remove(target.EntityId);
            return true;
        }

        return true;
    }

    private IEnumerable<ClientEntityInfo> EnumerateTargetsAt(int x, int y)
    {
        if (ClientGameState.Instance == null)
            yield break;

        foreach (var entity in ClientGameState.Instance.EnumerateEntities())
        {
            int cx = _hostPositions.TryGetValue(entity.EntityId, out var p) ? p.x : entity.X;
            int cy = _hostPositions.TryGetValue(entity.EntityId, out p) ? p.y : entity.Y;

            if (cx == x && cy == y)
                yield return entity;
        }
    }

    private Vector2Int GetActorOrigin(int actorId)
    {
        if (_hostPositions.TryGetValue(actorId, out var pos))
            return pos;

        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetEntity(actorId, out var actor))
            return new Vector2Int(actor.X, actor.Y);

        return Vector2Int.zero;
    }

    private static GridPoint RotateDirForMove(int x, int y, float rotation)
    {
        int deg = (int)((rotation + 45) / 90) * 90;
        deg = (deg % 360 + 360) % 360;

        if (deg == 90) return new GridPoint(y, -x);
        if (deg == 180) return new GridPoint(-x, -y);
        if (deg == 270) return new GridPoint(-y, x);
        return new GridPoint(x, y);
    }

    private bool TryDashMove(int actorId, int fromX, int fromY, int dirX, int dirY, int distance, out int targetX, out int targetY)
    {
        targetX = fromX;
        targetY = fromY;

        if (distance <= 0)
            return false;

        var state = ClientGameState.Instance;
        if (state == null)
            return false;

        int bestX = fromX;
        int bestY = fromY;

        for (int step = 1; step <= distance; step++)
        {
            int nx = fromX + dirX * step;
            int ny = fromY + dirY * step;

            if (!state.IsWalkable(nx, ny))
                break;

            bestX = nx;
            bestY = ny;
        }

        if (bestX == fromX && bestY == fromY)
            return false;

        if (IsTileOccupied(bestX, bestY, actorId))
            return false;

        targetX = bestX;
        targetY = bestY;
        return true;
    }

    private bool TryBlinkMove(int actorId, int fromX, int fromY, int dirX, int dirY, int distance, out int targetX, out int targetY)
    {
        targetX = fromX + dirX * Math.Max(1, distance);
        targetY = fromY + dirY * Math.Max(1, distance);

        if (targetX == fromX && targetY == fromY)
            return false;

        if (ClientGameState.Instance == null || !ClientGameState.Instance.IsWalkable(targetX, targetY))
            return false;

        if (IsTileOccupied(targetX, targetY, actorId))
            return false;

        return true;
    }

    private int ResolveDamageAmount(NewSkillDef skillDef, DamageAction damageAction, int actorId)
    {
        if (damageAction != null && damageAction.Amount > 0)
            return damageAction.Amount;

        if (TryGetCombatSnapshot(actorId, out var snapshot) && snapshot.TotalAtk > 0)
            return snapshot.TotalAtk;

        return 1;
    }

    private List<Vector2Int> ResolveDamageCells(DamageAction damageAction, Vector2Int origin, float rotation, int fallbackTargetX, int fallbackTargetY)
    {
        var cells = CalculateShapeCells(damageAction?.Shape, origin, rotation);
        if (cells.Count == 0)
            cells.Add(new Vector2Int(fallbackTargetX, fallbackTargetY));

        return cells;
    }

    private static List<Vector2Int> CalculateShapeCells(IShapeDef shape, Vector2Int origin, float rotation)
    {
        var result = new List<Vector2Int>();
        if (shape == null)
            return result;

        List<GridPoint> offsets = new();
        if (shape is CustomCellsShape customShape)
        {
            offsets = customShape.Cells ?? new List<GridPoint>();
        }
        else if (shape is RectShape rect)
        {
            int halfW = rect.Width / 2;
            int halfH = rect.Height / 2;
            for (int x = -halfW; x <= halfW; x++)
                for (int y = -halfH; y <= halfH; y++)
                    offsets.Add(new GridPoint(x, y));
        }
        else if (shape is DiamondShape diamond)
        {
            int r = diamond.Radius;
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    if (Math.Abs(x) + Math.Abs(y) <= r)
                        offsets.Add(new GridPoint(x, y));
        }

        foreach (var p in offsets)
        {
            var pt = RotateGridPoint(p.X, p.Y, shape.RotateWithCaster ? rotation : 0f);
            result.Add(new Vector2Int(origin.x + pt.X, origin.y + pt.Y));
        }

        return result;
    }

    private static GridPoint RotateGridPoint(int x, int y, float rotation)
    {
        float corrected = rotation + 180f;
        int deg = (int)((corrected + 45) / 90) * 90;
        deg = (deg % 360 + 360) % 360;

        if (deg == 90) return new GridPoint(y, -x);
        if (deg == 180) return new GridPoint(-x, -y);
        if (deg == 270) return new GridPoint(-y, x);
        return new GridPoint(x, y);
    }

    private bool TryDecodeActionRequest(string payload, out CS_ActionRequest req)
    {
        req = null!;

        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length < 4)
                return false;

            ushort packetId = BitConverter.ToUInt16(bytes, 2);
            if (packetId != (ushort)PacketID.CS_ActionRequest)
                return false;

            req = new CS_ActionRequest();
            req.Read(new ArraySegment<byte>(bytes));
            return true;
        }
        catch (Exception ex)
        {
            if (P2PDebugConfig.TraceHostFlow)
                Debug.LogWarning($"[P2PHostController] Failed to decode guest payload: {ex.Message}");
            return false;
        }
    }

    private bool TryGetCombatSnapshot(int actorId, out PlayerCombatSnapshot snapshot)
    {
        lock (_snapshotLock)
        {
            return _combatSnapshots.TryGetValue(actorId, out snapshot);
        }
    }

    private void MaybeRefreshSnapshots()
    {
        if (!IsHost || _snapshotRefreshInFlight)
            return;

        RefreshSnapshotsAsync();
    }

    private void SyncSessionKey()
    {
        var key = SessionContext.Instance != null ? SessionContext.Instance.Key ?? "" : "";
        if (string.Equals(_activeSessionKey, key, StringComparison.Ordinal))
            return;

        _activeSessionKey = key;
        ResetCombatState();
    }

    private void ResetCombatState()
    {
        _lastProcessedBeat = long.MinValue;
        _lastJudgeWindowBeat = long.MinValue;
        _resultSubmitted = false;
        _totalDamageDealt = 0;
        _snapshotRefreshInFlight = false;
        _activeSkills.Clear();
        _lastAcceptedBeatByActor.Clear();
        lock (_commandLock)
        {
            _beatScheduler.Clear();
            _delayedScheduler.Clear();
            _commandBuffer.Clear();
        }

        lock (_snapshotLock)
        {
            _combatSnapshots.Clear();
        }

        _batchedBeatResults.Clear();
        _batchedDeadEntities.Clear();
        _hostPositions.Clear();
    }

    private async void RefreshSnapshotsAsync()
    {
        _snapshotRefreshInFlight = true;
        int refreshedCount = 0;

        try
        {
            if (AppBootstrap.Instance == null || AppBootstrap.Instance.Root == null || ClientGameState.Instance == null)
                return;

            var roster = ClientGameState.Instance.EnumeratePlayerRoster().ToList();
            if (roster.Count == 0)
                return;

            var tasks = roster.Select(async entry =>
            {
                if (!ClientGameState.Instance.TryGetPlayerUid(entry.ActorId, out var uid) || string.IsNullOrWhiteSpace(uid))
                    return (ok: false, actorId: entry.ActorId, snapshot: default(PlayerCombatSnapshot));

                var result = await AppBootstrap.Instance.Root.PlayerStateApi.GetPlayerStateAsync(uid);
                if (!result.Ok || result.Data == null)
                    return (ok: false, actorId: entry.ActorId, snapshot: default(PlayerCombatSnapshot));

                var data = result.Data;
                var snapshot = new PlayerCombatSnapshot
                {
                    ActorId = entry.ActorId,
                    Uid = uid,
                    NormalAttackSkillId = data.NormalAttackSkillId ?? "",
                    ActiveSkillSlots = data.ActiveSkillSlots ?? Array.Empty<string>(),
                    TotalHp = data.TotalHp,
                    TotalAtk = data.TotalAtk,
                    TotalDef = data.TotalDef,
                    AppearanceId = data.AppearanceId
                };

                return (ok: true, actorId: entry.ActorId, snapshot: snapshot);
            });

            var results = await Task.WhenAll(tasks);

            lock (_snapshotLock)
            {
                foreach (var result in results)
                {
                    if (!result.ok)
                        continue;

                    _combatSnapshots[result.actorId] = result.snapshot;
                    refreshedCount++;
                }
            }

        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] Snapshot refresh done count={refreshedCount}");
    }
    catch (Exception ex)
    {
        if (P2PDebugConfig.TraceHostFlow)
            Debug.LogWarning($"[P2PHostController] Snapshot refresh failed: {ex.Message}");
    }
        finally
        {
            _snapshotRefreshInFlight = false;
        }
    }

    public void CheckAndSubmitGameResultIfCleared()
    {
        if (_resultSubmitted || !IsHost || P2PRelayClientBridge.Instance == null || !P2PRelayClientBridge.Instance.IsRelayMode)
            return;

        if (ClientGameState.Instance == null || RhythmClient.Instance == null)
            return;

        bool anyMonstersAlive = ClientGameState.Instance.EnumerateEntities()
            .Any(e => e.EntityType == (int)EntityType.Monster && e.Hp > 0);

        if (anyMonstersAlive)
            return;

        long songStartMs = SessionContext.Instance.LastInitMap != null && SessionContext.Instance.LastInitMap.SongStartServerTime > 0
            ? SessionContext.Instance.LastInitMap.SongStartServerTime
            : RhythmClient.Instance.ServerSongStartMs;

        long playTimeMs = Math.Max(0, TimeSync.ServerNowMs() - songStartMs);

        var resultPkt = new CS_P2PGameResult
        {
            IsClear = true,
            PlayTimeMs = playTimeMs,
            TotalDamage = (int)Math.Min(int.MaxValue, _totalDamageDealt)
        };

        if (NetworkManager.Instance != null)
        {
            if (P2PDebugConfig.TraceHostFlow)
                Debug.Log($"[P2PHostController] Clear detected, submitting result playTime={playTimeMs} damage={_totalDamageDealt}");
            NetworkManager.Instance.Send(resultPkt.Write());
            _resultSubmitted = true;
            if (P2PDebugConfig.TraceHostFlow)
                Debug.Log($"[P2PHostController] Game result submitted playTime={playTimeMs} damage={_totalDamageDealt}");
        }
    }

    private bool TryConsumeActionBeat(int actorId, long beat)
    {
        if (_lastAcceptedBeatByActor.TryGetValue(actorId, out var lastBeat) && lastBeat == beat)
            return false;

        _lastAcceptedBeatByActor[actorId] = beat;
        return true;
    }

    private string ResolveSkillId(int actorId, int slotIndex, int actionKind, string skillIdOverride = "")
    {
        if (!string.IsNullOrWhiteSpace(skillIdOverride))
            return skillIdOverride;

        string skillId = "";

        if (actorId == ClientGameState.Instance.MyActorId)
        {
            var input = RhythmInputController.Instance;
            if (input == null)
                return "Attack";

            skillId = (slotIndex < 0 || actionKind == (int)ActionKind.Attack)
                ? input.GetNormalAttackSkillId()
                : input.GetSkillSlotId(slotIndex);
        }
        else if (TryGetCombatSnapshot(actorId, out var snapshot))
        {
            skillId = (slotIndex < 0 || actionKind == (int)ActionKind.Attack)
                ? snapshot.NormalAttackSkillId
                : (slotIndex >= 0 && slotIndex < snapshot.ActiveSkillSlots.Length ? snapshot.ActiveSkillSlots[slotIndex] : "");
        }

        if (string.IsNullOrWhiteSpace(skillId))
            skillId = "Attack";

        return skillId;
    }

    private static bool HasGameplayEvents(NewSkillDef skillDef)
    {
        if (skillDef == null || skillDef.Tracks == null)
            return false;

        foreach (var track in skillDef.Tracks)
        {
            if (track?.Events == null)
                continue;

            foreach (var ev in track.Events)
            {
                if (ev?.Action == null)
                    continue;

                var type = ev.Action.GetSkillActionType();
                if (type == SkillActionType.Damage || type == SkillActionType.Move)
                    return true;
            }
        }

        return false;
    }

    private static NewSkillDef LoadSkillDefinition(string skillId)
    {
        return P2PCombatContentCache.GetSkillDefinition(skillId);
    }

    private static int GetActionPriority(BaseAction action)
    {
        if (action == null)
            return int.MaxValue;

        return action.GetSkillActionType() switch
        {
            SkillActionType.Move => 0,
            SkillActionType.Warning => 1,
            SkillActionType.InputLock => 2,
            SkillActionType.Damage => 3,
            SkillActionType.Sound => 4,
            SkillActionType.Wait => 5,
            _ => 6
        };
    }

    private sealed class QueuedCombatCommand
    {
        public CS_ActionRequest Request;
        public int ActorId;
        public bool FromLocalSource;
        public bool BypassInputGuards;
        public bool InstantBroadcasted;
        public string SkillIdOverride = "";
        public long? ForcedExecuteBeat;
    }

    private sealed class BeatCommandScheduler
    {
        private readonly Dictionary<long, Dictionary<int, QueuedCombatCommand>> _byBeat = new();

        public void Enqueue(long beat, QueuedCombatCommand command)
        {
            if (!_byBeat.TryGetValue(beat, out var perActor))
            {
                perActor = new Dictionary<int, QueuedCombatCommand>(8);
                _byBeat[beat] = perActor;
            }

            int actorId = command?.ActorId ?? 0;
            perActor[actorId] = command;
        }

        public void PopActions(long beat, List<QueuedCombatCommand> output)
        {
            output.Clear();
            if (!_byBeat.TryGetValue(beat, out var perActor) || perActor.Count == 0)
                return;

            foreach (var command in perActor.Values)
                output.Add(command);

            output.Sort((a, b) =>
            {
                int kindA = a?.Request?.ActionKind ?? int.MaxValue;
                int kindB = b?.Request?.ActionKind ?? int.MaxValue;
                int cmp = kindA.CompareTo(kindB);
                if (cmp != 0)
                    return cmp;

                int actorA = a?.ActorId ?? int.MaxValue;
                int actorB = b?.ActorId ?? int.MaxValue;
                return actorA.CompareTo(actorB);
            });

            _byBeat.Remove(beat);
        }

        public void Clear()
        {
            _byBeat.Clear();
        }
    }

    private static List<ScheduledEventEntry> CollectDueEvents(ScheduledSkill skill, long beat)
    {
        var due = new List<ScheduledEventEntry>();
        if (skill.SkillDef == null || skill.SkillDef.Tracks == null)
            return due;

        for (int t = 0; t < skill.SkillDef.Tracks.Count; t++)
        {
            var track = skill.SkillDef.Tracks[t];
            if (track?.Events == null)
                continue;

            for (int e = 0; e < track.Events.Count; e++)
            {
                var ev = track.Events[e];
                if (ev == null || ev.Action == null)
                    continue;

                int eventKey = (t << 16) | e;
                if (skill.ExecutedEvents.Contains(eventKey))
                    continue;

                long eventBeat = skill.StartBeat + (ev.TriggerTick / 480);
                if (eventBeat > beat)
                    continue;

                due.Add(new ScheduledEventEntry
                {
                    TrackIndex = t,
                    EventIndex = e,
                    EventKey = eventKey,
                    TriggerBeat = eventBeat,
                    Event = ev
                });
            }
        }

        return due;
    }

    private static bool IsSkillFinished(ScheduledSkill skill, long currentBeat)
    {
        if (skill.SkillDef == null || skill.SkillDef.Tracks == null)
            return true;

        int totalEvents = 0;
        foreach (var track in skill.SkillDef.Tracks)
            totalEvents += track?.Events?.Count ?? 0;

        if (skill.ExecutedEvents.Count < totalEvents)
            return false;

        long totalDurationBeats = (skill.SkillDef.TotalDurationTicks + 479) / 480;
        return currentBeat >= skill.StartBeat + totalDurationBeats;
    }

    private static bool CanHitTarget(ClientEntityInfo attacker, ClientEntityInfo target, DamageAction damageAction)
    {
        if (target.EntityId == attacker.EntityId || target.Hp <= 0)
            return false;

        bool isTargetPlayer = target.EntityType == (int)EntityType.Player;
        bool isTargetMonster = target.EntityType == (int)EntityType.Monster || target.EntityType == (int)EntityType.Object;

        bool allowPlayers = damageAction?.HitPlayers ?? false;
        bool allowMonsters = damageAction?.HitMonsters ?? true;

        if (isTargetPlayer)
            return allowPlayers;

        if (isTargetMonster)
            return allowMonsters;

        return false;
    }

    private sealed class ScheduledSkill
    {
        public CS_ActionRequest Request;
        public int ActorId;
        public string SkillId = "";
        public NewSkillDef SkillDef;
        public long StartBeat;
        public long StartTick;
        public float Rotation;
        public bool FromLocalSource;
        public HashSet<int> ExecutedEvents = new();
    }

    private sealed class ScheduledEventEntry
    {
        public int TrackIndex;
        public int EventIndex;
        public int EventKey;
        public long TriggerBeat;
        public SkillEvent Event;
    }

    private struct PlayerCombatSnapshot
    {
        public int ActorId;
        public string Uid;
        public string NormalAttackSkillId;
        public string[] ActiveSkillSlots;
        public int TotalHp;
        public int TotalAtk;
        public int TotalDef;
        public int AppearanceId;
    }
}

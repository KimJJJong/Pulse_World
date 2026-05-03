using Client.Data;
using GameShared.Data;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public sealed partial class P2PHostController
{
    private void DrainPendingActionRequests()
    {
        if (!IsHost)
        {
            while (_pendingActionRequests.TryDequeue(out _)) { }
            return;
        }

        const int MaxPendingRequestsPerFrame = 128;
        int processed = 0;

        while (processed < MaxPendingRequestsPerFrame && _pendingActionRequests.TryDequeue(out var pending))
        {
            if (pending?.Request == null)
                continue;

            HandleActionRequest(
                pending.Request,
                pending.ActorId,
                pending.FromLocalSource,
                pending.SkillIdOverride,
                pending.BypassInputGuards,
                pending.ForcedExecuteBeat);
            processed++;
        }

        if (P2PDebugConfig.LogOverheadEnabled && processed > 0)
        {
            Debug.Log(
                $"[P2PHostController] DrainPending processed={processed} remaining={_pendingActionRequests.Count} " +
                $"hostActor={HostActorId} isHost={IsHost}");
        }

        ProcessLateResolvedCommands();
    }

    private void DriveHostBeatSimulation()
    {
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
            ProcessCurrentBeatCommandsAtBeat(_lastProcessedBeat);
            contentDirector?.FinalizeHostBeat(_lastProcessedBeat);
            processedBeats++;
        }

        ProcessResolvedBeatCallbacks();
    }

    private void ProcessResolvedBeatCallbacks()
    {
        if (RhythmClient.Instance == null)
            return;

        long resolvedBeat = GetResolvedBeatIndex();
        if (resolvedBeat < 0)
            return;

        long maxResolvedBeat = Math.Min(resolvedBeat, _lastProcessedBeat);
        if (maxResolvedBeat < 0)
            return;

        if (_lastJudgeWindowBeat == long.MinValue)
            _lastJudgeWindowBeat = maxResolvedBeat - 1;

        int processedBeats = 0;

        while (_lastJudgeWindowBeat < maxResolvedBeat && processedBeats < MaxCatchUpBeatsPerUpdate)
        {
            long nextBeat = _lastJudgeWindowBeat + 1;
            ProcessResolvedBeatCommandsAtBeat(nextBeat);
            ProcessSkillsAtBeat(nextBeat);
            FlushBatchedBeatResults(nextBeat);
            _lastJudgeWindowBeat = nextBeat;
            processedBeats++;
        }
    }

    private long GetResolvedBeatIndex()
    {
        if (RhythmClient.Instance == null)
            return long.MinValue;

        double judgeWindowMs = RhythmClient.Instance.judgeWindowMs > 0 ? RhythmClient.Instance.judgeWindowMs : 100.0;
        long nowMs = RhythmClient.Instance.GetCurrentServerTimeMs();
        double resolvedServerTimeMs = nowMs - Math.Ceiling(judgeWindowMs);
        double elapsedMs = resolvedServerTimeMs - RhythmClient.Instance.ServerSongStartMs;
        if (elapsedMs < 0)
            return -1;

        double beatDurationMs = RhythmClient.Instance.GetBeatDurationMs();
        long resolvedBeat = (long)Math.Floor(elapsedMs / beatDurationMs);
        long windowMs = (long)Math.Ceiling(judgeWindowMs);

        // RhythmSystem.OnJudgeWindowEnd와 동일하게 "GetBeatTimeMs(beat) + window" 기준으로 닫힌 비트를 판정한다.
        while (resolvedBeat >= 0 && nowMs < RhythmClient.Instance.GetBeatTimeMs(resolvedBeat) + windowMs)
            resolvedBeat--;

        while (nowMs >= RhythmClient.Instance.GetBeatTimeMs(resolvedBeat + 1) + windowMs)
            resolvedBeat++;

        return resolvedBeat;
    }

    private void EnqueueScheduledCommand(QueuedCombatCommand command)
    {
        if (command == null)
            return;

        lock (_commandLock)
        {
            long beat = command.ForcedExecuteBeat ?? 0;
            var scheduler = IsCurrentBeatCommand(command) ? _beatScheduler : _delayedScheduler;
            scheduler.Enqueue(beat, command);
        }
    }

    private void EnqueueLateResolvedCommand(QueuedCombatCommand command)
    {
        if (command == null)
            return;

        lock (_commandLock)
        {
            _lateResolvedScheduler.Enqueue(command.ForcedExecuteBeat ?? 0, command);
        }
    }

    private void ProcessLateResolvedCommands()
    {
        if (_lastJudgeWindowBeat == long.MinValue)
            return;

        long firstLateBeat;
        lock (_commandLock)
        {
            if (!_lateResolvedScheduler.TryPeekMinBeat(out firstLateBeat))
                return;
        }

        for (long beat = firstLateBeat; beat <= _lastJudgeWindowBeat; beat++)
        {
            _commandBuffer.Clear();
            lock (_commandLock)
            {
                _lateResolvedScheduler.PopActions(beat, _commandBuffer);
            }

            foreach (var command in _commandBuffer)
            {
                if (command?.Request == null)
                    continue;

                ActivateScheduledCommand(command, beat, lateCatchUp: true);
            }

            ProcessSkillsAtBeat(beat, lateCatchUpOnly: true);
            FlushBatchedBeatResults(beat);
        }

        PromoteLateCatchUpSkills();
    }

    private void PromoteLateCatchUpSkills()
    {
        foreach (var scheduled in _activeSkills)
        {
            if (scheduled != null && scheduled.IsLateCatchUp)
                scheduled.IsLateCatchUp = false;
        }
    }

    // currentBeat phase: only starts attack/skill flows and registers them for later resolution.
    private void ProcessCurrentBeatCommandsAtBeat(long beat)
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

    // resolvedBeat phase: movement must settle before any damage event is resolved.
    private void ProcessResolvedBeatCommandsAtBeat(long beat)
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

    private static bool IsCurrentBeatCommand(QueuedCombatCommand command)
    {
        if (command?.Request == null)
            return false;

        var kind = (ActionKind)command.Request.ActionKind;
        return kind == ActionKind.Attack || kind == ActionKind.Skill;
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
            else if (P2PDebugConfig.TraceHostFlow)
                Debug.LogWarning($"[P2PHostController] DropGuest actor={actorId} action={(ActionKind)req.ActionKind} reason=ActorNotFound entities={ClientGameState.Instance.EntityCount}");
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
            // [Fix] 클라이언트 TryGetJudgeWindowInfo와 동일한 nearest/prev 비교 로직.
            // GetNearestBeatIndex(반올림)만 쓰면 비트 포인트 후반부(T 이후) 입력 시
            // N+1이 반환되어 diffMs가 크게 나와 window 초과 → 차단됨.
            // nearest와 prev(nearest-1) 중 더 가까운 쪽을 선택해야
            // 비트 포인트 기준 ±window가 대칭으로 적용된다.
            long nearestBeat = RhythmClient.Instance.GetNearestBeatIndex(req.ClientSendTimeMs);
            long nearestTime = RhythmClient.Instance.GetBeatTimeMs(nearestBeat);
            long nearestDiff = Math.Abs(req.ClientSendTimeMs - nearestTime);

            long prevBeat = nearestBeat - 1;
            long prevTime = RhythmClient.Instance.GetBeatTimeMs(prevBeat);
            long prevDiff = Math.Abs(req.ClientSendTimeMs - prevTime);

            executeBeat = prevDiff < nearestDiff ? prevBeat : nearestBeat;
        }
        bool isLateResolvedBeat = !bypassInputGuards
                                  && _lastJudgeWindowBeat != long.MinValue
                                  && executeBeat <= _lastJudgeWindowBeat;
        bool hasMissedSkillStartBeat = !bypassInputGuards
                                       && _lastProcessedBeat != long.MinValue
                                       && executeBeat <= _lastProcessedBeat;

        if (P2PDebugConfig.LogOverheadEnabled)
        {
            Debug.Log(
                $"[P2PHostController] HandleAction src={(fromLocalSource ? "Local" : "Guest")} actor={actorId} reqActor={req.ActorId} " +
                $"action={(ActionKind)req.ActionKind} slot={req.SlotIndex} target=({req.TargetX},{req.TargetY}) " +
                $"sendMs={req.ClientSendTimeMs} executeBeat={executeBeat} lateResolved={isLateResolvedBeat} " +
                $"processedBeat={_lastProcessedBeat} judgeBeat={_lastJudgeWindowBeat}");
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

        if (req.ActionKind == (int)ActionKind.Wait)
            return;

        QueuedCombatCommand command = new QueuedCombatCommand
        {
            Request = req,
            ActorId = actorId,
            FromLocalSource = fromLocalSource,
            SkillIdOverride = skillIdOverride,
            ForcedExecuteBeat = executeBeat
        };

        if (req.ActionKind == (int)ActionKind.Move)
        {
            if (isLateResolvedBeat)
            {
                if (P2PDebugConfig.TraceHostFlow)
                    Debug.Log($"[P2PHostController] LateAccept move actor={actorId} beat={executeBeat} resolvedBeat={_lastJudgeWindowBeat}");
                EnqueueLateResolvedCommand(command);
            }
            else
            {
                EnqueueScheduledCommand(command);
            }
            return;
        }

        if (req.ActionKind != (int)ActionKind.Attack && req.ActionKind != (int)ActionKind.Skill)
            return;

        string skillId = ResolveSkillId(actorId, req.SlotIndex, req.ActionKind, skillIdOverride);
        command.SkillIdOverride = skillId;

        long startTick = executeBeat * 480;
        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] AcceptLocal actor={actorId} action={(ActionKind)req.ActionKind} skill={skillId} beat={executeBeat} startTick={startTick} target=({req.TargetX},{req.TargetY}) late={isLateResolvedBeat}");

        if (isLateResolvedBeat)
        {
            EnqueueLateResolvedCommand(command);
            return;
        }

        // currentBeat 시작 phase는 이미 지나갔지만 judge window 안이면,
        // 해당 비트의 스킬을 지금 즉시 활성화해서 "입력 씹힘" 없이 이어서 처리한다.
        if (hasMissedSkillStartBeat)
        {
            if (P2PDebugConfig.TraceHostFlow)
                Debug.Log($"[P2PHostController] CatchUpSkill actor={actorId} skill={skillId} beat={executeBeat} processedBeat={_lastProcessedBeat} judgeBeat={_lastJudgeWindowBeat}");
            ActivateScheduledCommand(command, executeBeat);
            return;
        }

        BroadcastInstantSkill(actorId, skillId, req.Rotation, startTick, fromLocalSource);
        command.InstantBroadcasted = true;
        EnqueueScheduledCommand(command);
    }

    private void ActivateScheduledCommand(QueuedCombatCommand command, long beat, bool lateCatchUp = false)
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
            SkillDef = LoadSkillDefinition(skillId),
            IsLateCatchUp = lateCatchUp
        };

        scheduled.UseFallbackDamage = scheduled.SkillDef == null || !HasGameplayEvents(scheduled.SkillDef);
        InitializeScheduledSkillState(scheduled);
        _activeSkills.Add(scheduled);
    }

    private void ProcessSkillsAtBeat(long beat, bool lateCatchUpOnly = false)
    {
        if (_activeSkills.Count == 0 || ClientGameState.Instance == null)
            return;

        for (int i = _activeSkills.Count - 1; i >= 0; i--)
        {
            var scheduled = _activeSkills[i];

            if (scheduled.IsLateCatchUp != lateCatchUpOnly)
                continue;

            if (!ClientGameState.Instance.TryGetEntity(scheduled.ActorId, out var actorInfo) || actorInfo.Hp <= 0)
            {
                _activeSkills.RemoveAt(i);
                continue;
            }

            if (scheduled.UseFallbackDamage)
            {
                if (beat >= scheduled.StartBeat)
                {
                    ProcessFallbackDamage(scheduled, actorInfo, beat);
                    _activeSkills.RemoveAt(i);
                }
                continue;
            }

            // [최적화] new List 할당 제거 → 재사용 버퍼 사용
            CollectDueEvents(scheduled, beat, _dueEventsBuffer);
            if (_dueEventsBuffer.Count == 0)
            {
                if (IsSkillFinished(scheduled, beat))
                    _activeSkills.RemoveAt(i);
                continue;
            }

            _dueEventsBuffer.Sort((a, b) =>
            {
                int cmp = a.TriggerBeat.CompareTo(b.TriggerBeat);
                if (cmp != 0)
                    return cmp;

                cmp = GetActionPriority(a.Event.Action).CompareTo(GetActionPriority(b.Event.Action));
                if (cmp != 0)
                    return cmp;

                cmp = a.TrackIndex.CompareTo(b.TrackIndex);
                if (cmp != 0)
                    return cmp;

                return a.EventIndex.CompareTo(b.EventIndex);
            });

            foreach (var entry in _dueEventsBuffer)
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
            _hostPositions[scheduled.ActorId] = new Vector2Int(targetX, targetY);

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

        if (P2PDebugConfig.LogOverheadEnabled && ShouldTraceHostPlayerActor(actorId))
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

        if (deadEntities == null)
            return;

        foreach (var deadId in deadEntities)
            _batchedDeadEntities.Add(deadId);
    }

    private void FlushBatchedBeatResults(long beat)
    {
        if (_batchedBeatResults.Count > 0)
        {
            if (P2PDebugConfig.LogOverheadEnabled)
            {
                var playerResults = _batchedBeatResults
                    .Where(x => x != null && ShouldTraceHostPlayerActor(x.ActorId))
                    .ToArray();
                string summary = string.Join(",",
                    playerResults.Select(x =>
                        $"{(ActionKind)x.ActionKind}:{x.ActorId} ({x.FromX},{x.FromY})->({x.ToX},{x.ToY}) accepted={x.Accepted}"));
                if (playerResults.Length > 0)
                    Debug.Log($"[P2PHostController] FlushBeatResults beat={beat} count={playerResults.Length} results={summary}");
            }

            var pkt = new SC_BeatActions
            {
                BeatIndex = beat,
                beatActionResults = new List<SC_BeatActions.BeatActionResult>(_batchedBeatResults)
            };
            BroadcastAndRelay(pkt);
            _batchedBeatResults.Clear();
        }

        if (_batchedDeadEntities.Count <= 0)
            return;

        foreach (var deadId in _batchedDeadEntities)
            BroadcastAndRelay(new SC_EntityDespawn { BeatIndex = beat, EntityId = deadId });

        _batchedDeadEntities.Clear();
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

        if (P2PDebugConfig.LogOverheadEnabled)
        {
            string detail = packet switch
            {
                SC_BeatActions beatPkt => $"beat={beatPkt.BeatIndex} count={beatPkt.beatActionResults?.Count ?? 0}",
                SC_ActionInstantBroadcast instantPkt => $"actor={instantPkt.ActorId} skill={instantPkt.SkillId} startTick={instantPkt.StartTick}",
                SC_EntityDespawn despawnPkt => $"entity={despawnPkt.EntityId} beat={despawnPkt.BeatIndex}",
                _ => ""
            };
            Debug.Log(
                $"[P2PHostController] BroadcastAndRelay packet={packet.GetType().Name} {detail} " +
                $"pendingSendBefore={_pendingSendQueue.Count}");
        }

        bridge.DispatchLocal(packet);
        // [최적화] 동기 전송 → 큐 적재 (LateUpdate에서 일괄 전송)
        _pendingSendQueue.Enqueue(packet);
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
        if (P2PDebugConfig.TraceHostFlow && ShouldTraceHostPlayerActor(actorId))
            Debug.Log($"[P2PHostController] LocalInstantSkill actor={actorId} skill={skillId} startTick={startTick} rot={rotation:F0}");
        // [최적화] 동기 전송 → 큐 적재
        _pendingSendQueue.Enqueue(pkt);
    }

    private bool ShouldTraceHostPlayerActor(int actorId)
    {
        if (actorId <= 0 || ClientGameState.Instance == null)
            return false;

        if (ClientGameState.Instance.TryGetPlayerUid(actorId, out _))
            return true;

        var playerActorIds = ClientGameState.Instance.PlayerActorIds;
        return playerActorIds != null && Array.IndexOf(playerActorIds, actorId) >= 0;
    }

    private bool IsTileOccupied(int targetX, int targetY, int ignoreActorId)
    {
        foreach (var kv in _hostPositions)
        {
            if (kv.Key == ignoreActorId)
                continue;
            if (kv.Value.x == targetX && kv.Value.y == targetY)
                return true;
        }

        if (ClientGameState.Instance == null)
            return false;

        foreach (var entity in ClientGameState.Instance.EnumerateEntities())
        {
            if (entity.Hp <= 0 || entity.EntityId == ignoreActorId)
                continue;

            if (_hostPositions.ContainsKey(entity.EntityId))
                continue;

            if (entity.X == targetX && entity.Y == targetY)
                return true;
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

        if (after > 0 || !deadEntities.Add(target.EntityId))
            return true;

        _hostPositions.Remove(target.EntityId);
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

        if (deg == 90)
            return new GridPoint(y, -x);
        if (deg == 180)
            return new GridPoint(-x, -y);
        if (deg == 270)
            return new GridPoint(-y, x);
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

        if ((bestX == fromX && bestY == fromY) || IsTileOccupied(bestX, bestY, actorId))
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

        return !IsTileOccupied(targetX, targetY, actorId);
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
            {
                for (int y = -halfH; y <= halfH; y++)
                    offsets.Add(new GridPoint(x, y));
            }
        }
        else if (shape is DiamondShape diamond)
        {
            int r = diamond.Radius;
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Math.Abs(x) + Math.Abs(y) <= r)
                        offsets.Add(new GridPoint(x, y));
                }
            }
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

        if (deg == 90)
            return new GridPoint(y, -x);
        if (deg == 180)
            return new GridPoint(-x, -y);
        if (deg == 270)
            return new GridPoint(-y, x);
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
            _lateResolvedScheduler.Clear();
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

        if (NetworkManager.Instance == null)
            return;

        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] Clear detected, submitting result playTime={playTimeMs} damage={_totalDamageDealt}");
        NetworkManager.Instance.Send(resultPkt.Write());
        _resultSubmitted = true;
        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] Game result submitted playTime={playTimeMs} damage={_totalDamageDealt}");
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

    private static void InitializeScheduledSkillState(ScheduledSkill skill)
    {
        if (skill == null || skill.SkillDef?.Tracks == null)
        {
            if (skill != null)
            {
                skill.RemainingActionEvents = 0;
                skill.NextEventIndexByTrack = Array.Empty<int>();
            }
            return;
        }

        skill.NextEventIndexByTrack = new int[skill.SkillDef.Tracks.Count];
        skill.RemainingActionEvents = 0;

        for (int t = 0; t < skill.SkillDef.Tracks.Count; t++)
        {
            var track = skill.SkillDef.Tracks[t];
            if (track?.Events == null)
                continue;

            for (int e = 0; e < track.Events.Count; e++)
            {
                if (track.Events[e]?.Action != null)
                    skill.RemainingActionEvents++;
            }
        }
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

    // [최적화] void + result 파라미터로 변경 — new List 할당 제거
    private static void CollectDueEvents(ScheduledSkill skill, long beat, List<ScheduledEventEntry> result)
    {
        result.Clear();
        if (skill.SkillDef == null || skill.SkillDef.Tracks == null)
            return;

        for (int t = 0; t < skill.SkillDef.Tracks.Count; t++)
        {
            var track = skill.SkillDef.Tracks[t];
            if (track?.Events == null)
                continue;

            int nextIndex = skill.NextEventIndexByTrack != null && t < skill.NextEventIndexByTrack.Length
                ? skill.NextEventIndexByTrack[t]
                : 0;

            while (nextIndex < track.Events.Count)
            {
                var ev = track.Events[nextIndex];
                if (ev == null || ev.Action == null)
                {
                    nextIndex++;
                    continue;
                }

                long eventBeat = skill.StartBeat + (ev.TriggerTick / 480);
                if (eventBeat > beat)
                    break;

                result.Add(new ScheduledEventEntry
                {
                    TrackIndex = t,
                    EventIndex = nextIndex,
                    EventKey = (t << 16) | nextIndex,
                    TriggerBeat = eventBeat,
                    Event = ev
                });

                nextIndex++;
                if (skill.RemainingActionEvents > 0)
                    skill.RemainingActionEvents--;
            }

            if (skill.NextEventIndexByTrack != null && t < skill.NextEventIndexByTrack.Length)
                skill.NextEventIndexByTrack[t] = nextIndex;
        }
    }

    private static bool IsSkillFinished(ScheduledSkill skill, long currentBeat)
    {
        if (skill.SkillDef == null || skill.SkillDef.Tracks == null)
            return true;

        if (skill.RemainingActionEvents > 0)
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
}

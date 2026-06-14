using Client.Data;
using GameShared.Data;
using ServerCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public sealed partial class P2PHostController : MonoBehaviour
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
    private readonly BeatCommandScheduler _lateResolvedScheduler = new();
    private readonly List<QueuedCombatCommand> _commandBuffer = new(16);

    private readonly List<SC_BeatActions.BeatActionResult> _batchedBeatResults = new();
    private readonly HashSet<int> _batchedDeadEntities = new();
    private readonly Dictionary<int, Vector2Int> _hostPositions = new();
    private readonly Dictionary<int, long> _transientEntityExpireBeatById = new();

    // 서버 RoomBase.Update와 동일하게, 입력은 먼저 큐에 쌓고 LateUpdate 시작에서 일괄 판정한다.
    private readonly ConcurrentQueue<PendingActionRequest> _pendingActionRequests = new();

    // [최적화] LateUpdate 일괄 전송 큐 — Update/HandleActionRequest에서 Enqueue, LateUpdate에서 Dequeue
    private readonly ConcurrentQueue<IPacket> _pendingSendQueue = new();

    // [최적화] CollectDueEvents 재사용 버퍼 — 매 Beat new List 할당 제거
    private readonly List<ScheduledEventEntry> _dueEventsBuffer = new(32);

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

    private void LateUpdate()
    {
        DrainPendingActionRequests();
        DriveHostBeatSimulation();

        if (!IsHost)
            return;

        var bridge = P2PRelayClientBridge.Instance;
        if (bridge == null || !bridge.IsRelayMode)
        {
            _pendingSendQueue.Clear();
            return;
        }

        // 같은 beat의 즉시/결과 패킷이 프레임 단위로 쪼개지면 원격에서 액션과 사운드가 밀린다.
        const int MaxSendPerFrame = 64;
        int sent = 0;
        while (sent < MaxSendPerFrame && _pendingSendQueue.TryDequeue(out var pkt))
        {
            bridge.SendWrappedPacket(pkt);
            sent++;
        }
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
        while (_pendingActionRequests.TryDequeue(out _)) { }

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

        _pendingSendQueue.Clear();

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
            Debug.Log($"[P2PPlayerSync] Host authority disabled actor={HostActorId}");
            if (P2PDebugConfig.TraceHostFlow)
                Debug.Log("[P2PHostController] Host mode disabled");
            return;
        }

        SyncSessionKey();
        P2PCombatContentCache.WarmUpSkills();
        MaybeRefreshSnapshots();
        Debug.Log($"[P2PPlayerSync] Host authority enabled actor={HostActorId} session={_activeSessionKey}");
        DumpHostInitDiagnostics("SetHostMode_Enable");
        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] Host mode enabled actor={HostActorId}");
    }

    public void SetHostActorId(int actorId)
    {
        if (HostActorId == actorId)
        {
            if (IsHost)
                MaybeRefreshSnapshots();
            return;
        }

        HostActorId = actorId;

        if (IsHost)
            MaybeRefreshSnapshots();

        Debug.Log($"[P2PPlayerSync] Host actor set actor={actorId} isHost={IsHost}");
        DumpHostInitDiagnostics("SetHostActorId");
        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] Host actor set to {actorId}");
    }

    /// <summary>
    /// [HostInit_Diag] Host 권한이 활성/변경된 시점에 ClientGameState 상태를 덤프하여
    /// 1) 본인 actor entity가 등록되어 있는지
    /// 2) 다른 player actor entity들도 모두 등록되어 있는지
    /// 3) HostActorId / SessionContext.MyActorId / GS.MyActorId 의 일관성
    /// 을 한눈에 확인할 수 있게 한다.
    /// </summary>
    private void DumpHostInitDiagnostics(string source)
    {
        var gs = ClientGameState.Instance;
        var session = SessionContext.Instance;
        int sessionActor = session?.MyActorId ?? 0;
        int gsActor = gs?.MyActorId ?? 0;
        int entityCount = gs?.EntityCount ?? -1;

        string roster = "<gs-null>";
        string playerEntities = "<gs-null>";
        bool hostHasOwnEntity = false;
        bool sessionActorHasEntity = false;

        if (gs != null)
        {
            var rosterEntries = new System.Text.StringBuilder();
            foreach (var entry in gs.EnumeratePlayerRoster())
            {
                if (rosterEntries.Length > 0) rosterEntries.Append(',');
                rosterEntries.Append(entry.ActorId).Append(':').Append(string.IsNullOrWhiteSpace(entry.Uid) ? "-" : entry.Uid);
            }
            roster = rosterEntries.Length == 0 ? "none" : rosterEntries.ToString();

            var entitySb = new System.Text.StringBuilder();
            foreach (var e in gs.EnumerateEntities())
            {
                if (e.EntityType != (int)EntityType.Player) continue;
                if (entitySb.Length > 0) entitySb.Append(',');
                entitySb.Append(e.EntityId).Append("@(").Append(e.X).Append(',').Append(e.Y).Append(") hp=").Append(e.Hp);
                if (e.EntityId == HostActorId) hostHasOwnEntity = true;
                if (e.EntityId == sessionActor) sessionActorHasEntity = true;
            }
            playerEntities = entitySb.Length == 0 ? "none" : entitySb.ToString();
        }

        Debug.Log(
            $"[HostInit_Diag] source={source} hostActor={HostActorId} isHost={IsHost} " +
            $"sessionActor={sessionActor} gsActor={gsActor} entityCount={entityCount} " +
            $"hostHasOwnEntity={hostHasOwnEntity} sessionActorHasEntity={sessionActorHasEntity} " +
            $"roster=[{roster}] playerEntities=[{playerEntities}]");

        // 일관성 위반 — 가장 자주 보고되는 결함 케이스
        if (IsHost)
        {
            if (HostActorId <= 0)
                Debug.LogWarning("[HostInit_Diag] WARN HostActorId is 0 while IsHost=true — Host authority will not pass GetHostAuthorityState gate");
            else if (sessionActor != HostActorId)
                Debug.LogWarning($"[HostInit_Diag] WARN sessionActor({sessionActor}) != HostActorId({HostActorId}) — host treats different actor as authority");
            else if (gsActor != HostActorId)
                Debug.LogWarning($"[HostInit_Diag] WARN GS.MyActorId({gsActor}) != HostActorId({HostActorId}) — InitMap not yet applied or actor mismatch");
            else if (!hostHasOwnEntity)
                Debug.LogWarning($"[HostInit_Diag] WARN HostActorId={HostActorId} but no Player entity for own actor in GS. Host requests will be dropped (ActorNotFound). Likely InitMap entitiess missing host or roster/MyActorId mismatch");
        }
    }

    public void EnqueueLocalActionRequest(CS_ActionRequest req)
    {
        if (!IsHost || req == null)
            return;

        if (P2PDebugConfig.TraceHostFlow)
            Debug.Log($"[P2PHostController] EnqueueLocal actor={req.ActorId} action={(ActionKind)req.ActionKind} slot={req.SlotIndex} target=({req.TargetX},{req.TargetY}) sendMs={req.ClientSendTimeMs}");

        _pendingActionRequests.Enqueue(new PendingActionRequest
        {
            Request = req,
            ActorId = req.ActorId,
            FromLocalSource = true
        });
        P2PTransportDiagnostics.RecordHostQueue(
            "LocalQueue",
            req.ActorId,
            ((ActionKind)req.ActionKind).ToString(),
            $"slot={req.SlotIndex} target=({req.TargetX},{req.TargetY})");
    }

    public void EnqueueGuestActionRequest(CS_P2PPayload pkt)
    {
        if (!IsHost || pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return;

        if (!TryDecodeActionRequest(pkt.Payload, out var req))
        {
            P2PTransportDiagnostics.RecordHostJudge("DecodeFail", pkt.SenderActorId, "Unknown", "guest payload decode failed");
            return;
        }

        int actorId = pkt.SenderActorId > 0 ? pkt.SenderActorId : req.ActorId;
        if (P2PRelayClientBridge.HasInstance)
        {
            P2PRelayClientBridge.Instance.ReportGuestActionTrace(
                actorId,
                req,
                P2PActionTraceStage.HostSeen);
        }
        if (P2PDebugConfig.LogOverheadEnabled)
        {
            Debug.Log(
                $"[P2PHostController] EnqueueGuest actor={actorId} reqActor={req.ActorId} action={(ActionKind)req.ActionKind} " +
                $"slot={req.SlotIndex} target=({req.TargetX},{req.TargetY}) sendMs={req.ClientSendTimeMs} hostActor={HostActorId}");
        }
        _pendingActionRequests.Enqueue(new PendingActionRequest
        {
            Request = req,
            ActorId = actorId,
            FromLocalSource = false
        });
        P2PTransportDiagnostics.RecordHostQueue(
            "GuestQueue",
            actorId,
            ((ActionKind)req.ActionKind).ToString(),
            $"reqActor={req.ActorId} slot={req.SlotIndex} target=({req.TargetX},{req.TargetY})");
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
}

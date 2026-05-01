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
    private readonly List<QueuedCombatCommand> _commandBuffer = new(16);

    private readonly List<SC_BeatActions.BeatActionResult> _batchedBeatResults = new();
    private readonly HashSet<int> _batchedDeadEntities = new();
    private readonly Dictionary<int, Vector2Int> _hostPositions = new();

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

        // 프레임당 전송 상한 — 스파이크 방지 (4인 기준 Beat당 최대 패킷 ~10개)
        const int MaxSendPerFrame = 16;
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

        _pendingActionRequests.Enqueue(new PendingActionRequest
        {
            Request = req,
            ActorId = req.ActorId,
            FromLocalSource = true
        });
    }

    public void EnqueueGuestActionRequest(CS_P2PPayload pkt)
    {
        if (!IsHost || pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return;

        if (!TryDecodeActionRequest(pkt.Payload, out var req))
            return;

        int actorId = pkt.SenderActorId > 0 ? pkt.SenderActorId : req.ActorId;
        _pendingActionRequests.Enqueue(new PendingActionRequest
        {
            Request = req,
            ActorId = actorId,
            FromLocalSource = false
        });
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

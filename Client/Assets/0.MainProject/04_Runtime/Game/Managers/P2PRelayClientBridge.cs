using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class P2PRelayClientBridge : MonoBehaviour
{
    public static P2PRelayClientBridge Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(P2PRelayClientBridge));
                _instance = go.AddComponent<P2PRelayClientBridge>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    private static P2PRelayClientBridge _instance;
    public static bool HasInstance => _instance != null;

    public bool IsP2PMode => _transportMode != P2PTransportMode.Disabled;
    public bool IsRelayMode => IsP2PMode; // Legacy compatibility for existing gameplay systems.
    public bool IsServerRelayTransport => _transportMode == P2PTransportMode.ServerRelay;
    public bool IsSteamTransport => _transportMode == P2PTransportMode.SteamP2P;
    public bool IsHostLocal { get; private set; }
    public int HostActorId { get; private set; }
    public string HostUid { get; private set; } = "";
    public string HostSteamId64 { get; private set; } = "";
    public int HostEpoch { get; private set; }
    public string RelayKey { get; private set; } = "";
    public bool IsDispatchingLocal { get; private set; }
    public long HostLastRttMs => _hostLastRttMs;
    public long HostAvgRttMs => _hostAvgRttMs;
    public long HostMaxRttMs => _hostMaxRttMs;
    public long HostMinRttMs => _hostMinRttMs == long.MaxValue ? 0 : _hostMinRttMs;
    public long HostLastRawRttMs => _hostLastRawRttMs;
    public long HostLastProcMs => _hostLastProcMs;
    public string HostPingStatus => _hostPingStatus;
    public bool HasRemoteHostPing => IsP2PMode && !IsHostLocal && HostActorId > 0;
    public string TransportName => IsSteamTransport ? (_steamTransport?.TransportName ?? "SteamP2P") : (IsServerRelayTransport ? "ServerRelay" : "Disabled");
    public bool IsSteamTransportRunning => _steamTransport?.IsRunning ?? false;
    public bool IsSteamTransportConnectedToHost => _steamTransport?.IsConnectedToHost ?? false;
    public int SteamConnectedPeerCount => _steamTransport?.ConnectedPeerCount ?? 0;
    public string TransportLastError => _steamTransport?.LastError ?? "";
    public string SteamConnectionPhase => _steamTransport?.ConnectionPhase ?? "-";
    public string SteamRouteHint => _steamTransport?.RouteHint ?? "Unknown";
    public string SteamDetailedStatusSnippet => _steamTransport?.DetailedStatusHint ?? "";
    public string SteamLastDisconnectReason => _steamTransport?.LastDisconnectReason ?? "";
    public long SteamInitialConnectAttemptAtMs => _steamTransport?.InitialConnectAttemptAtMs ?? 0L;
    public long SteamLastConnectAttemptAtMs => _steamTransport?.LastConnectAttemptAtMs ?? 0L;
    public long SteamConnectedAtMs => _steamTransport?.LastConnectedAtMs ?? 0L;
    public long SteamNextReconnectAtMs => _steamTransport?.NextReconnectAtMs ?? 0L;
    public int SteamConnectAttemptCount => _steamTransport?.ConnectAttemptCount ?? 0;
    public int SteamRetryCount => _steamTransport?.RetryCount ?? 0;
    public int SteamRetryBackoffMs => _steamTransport?.CurrentRetryBackoffMs ?? 0;
    public bool HasTransportPairStats => _transportPairStats.IsAvailable;
    public int TransportPairPingMs => _transportPairStats.IsAvailable ? _transportPairStats.PingMs : -1;
    public int TransportPendingReliableBytes => _transportPairStats.PendingReliable;
    public int TransportPendingUnreliableBytes => _transportPairStats.PendingUnreliable;
    public int TransportSentUnackedReliableBytes => _transportPairStats.SentUnackedReliable;
    public float TransportConnectionQualityLocal => _transportPairStats.ConnectionQualityLocal;
    public float TransportConnectionQualityRemote => _transportPairStats.ConnectionQualityRemote;
    public string TransportPairRouteHint => _transportPairStats.RouteHint ?? "Unknown";
    public string TransportPairDetail => _transportPairStats.TransportDetail ?? "";
    public long FallbackActivatedAtMs => _fallbackActivatedAtMs;
    public long RecoveryObservedAtMs => _recoveryObservedAtMs;
    public string FallbackReason => _fallbackReason ?? "None";
    public long GameplayLastStartRttMs => _gameplayLastStartRttMs;
    public long GameplayAvgStartRttMs => _gameplayAvgStartRttMs;
    public long GameplayMaxStartRttMs => _gameplayMaxStartRttMs;
    public long GameplayLastResultRttMs => _gameplayLastResultRttMs;
    public long GameplayAvgResultRttMs => _gameplayAvgResultRttMs;
    public long GameplayMaxResultRttMs => _gameplayMaxResultRttMs;
    public int GameplayPendingActionCount => _pendingGameplayActions.Count;
    public string GameplayTelemetryStatus => _gameplayTelemetryStatus;
    public string LocalSteamId64 => GetLocalSteamId64();
    public string HostAuthorityDebugState => GetHostAuthorityState();
    public string ServerRoleSummary => IsP2PMode ? "Start/End validation only" : "Dedicated simulation";
    public string SteamTransportDecisionReason => DescribeSteamTransportDecision(_matchManifest);
    public string HostSelectionModeSummary => _matchManifest != null && !string.IsNullOrWhiteSpace(_matchManifest.HostSelectionMode)
        ? _matchManifest.HostSelectionMode
        : "-";
    public string HostSelectionMetricVersion => _matchManifest != null && !string.IsNullOrWhiteSpace(_matchManifest.HostSelectionMetricVersion)
        ? _matchManifest.HostSelectionMetricVersion
        : "-";
    public int HostSelectionEpoch => _matchManifest != null ? _matchManifest.HostSelectionEpoch : 0;
    public float HostSelectionScore => _matchManifest != null ? _matchManifest.HostSelectionScore : -1f;
    public string HostCandidateOrderSummary => _matchManifest != null && _matchManifest.HostCandidateOrder != null && _matchManifest.HostCandidateOrder.Count > 0
        ? string.Join(" > ", _matchManifest.HostCandidateOrder)
        : "-";
    public string NetworkStateSummary
    {
        get
        {
            if (!IsP2PMode)
                return "DedicatedServer";

            if (HostActorId <= 0)
                return "WaitingHostElection";

            if (IsHostLocal)
                return IsSteamTransport ? "HostAuthorityLocal(Steam)" : "HostAuthorityLocal(ServerRelay)";

            if (IsSteamTransport)
                return IsSteamTransportConnectedToHost
                    ? "GuestConnectedToHost"
                    : $"GuestWaitingSteamLink({SteamConnectionPhase})";

            return "GuestViaServerRelay";
        }
    }

    private string DescribeSteamTransportDecision(SessionDtos.MatchManifestDto manifest)
    {
        if (manifest == null)
            return "ManifestMissing";

        if (string.IsNullOrWhiteSpace(manifest.NetworkMode))
            return "NetworkModeMissing";

        if (manifest.NetworkMode.IndexOf("steam", StringComparison.OrdinalIgnoreCase) < 0)
            return $"NonSteamMode:{manifest.NetworkMode}";

        var root = AppBootstrap.Instance != null ? AppBootstrap.Instance.Root : null;
        if (root?.Config == null)
            return "ConfigMissing";

        if (!root.Config.EnableSteam)
            return "ConfigEnableSteamOff";

        if (!root.Config.PreferSteamP2PInGame)
            return "PreferSteamP2PInGameOff";

        var steam = root.SteamPlatform;
        if (steam == null)
            return "SteamPlatformMissing";

        if (!steam.Enabled)
            return "SteamDisabled";

        if (!steam.IsInitialized)
            return string.IsNullOrWhiteSpace(steam.LastError)
                ? "SteamNotInitialized"
                : $"SteamNotInitialized:{steam.LastError}";

        if (string.IsNullOrWhiteSpace(steam.SteamId64))
            return "LocalSteamIdMissing";

        if (string.IsNullOrWhiteSpace(manifest.HostSteamId64))
            return "ManifestHostSteamMissing";

        return IsSteamTransport
            ? "SteamTransportSelected"
            : "SteamEligibleButRelaySelected";
    }
    public string NetworkFlowSummary
    {
        get
        {
            if (!IsP2PMode)
                return "Client <-> GameServer";

            if (IsHostLocal)
                return IsSteamTransport
                    ? "Local host -> Steam guests"
                    : "Local host -> Server relay guests";

            if (IsSteamTransport)
                return IsSteamTransportConnectedToHost
                    ? "Guest -> Steam -> Host"
                    : "Guest -> Steam pending (relay fallback)";

            return "Guest -> Server relay -> Host";
        }
    }
    public string TransportDebugStatus
    {
        get
        {
            if (!IsP2PMode)
                return "Disabled";

            if (!IsSteamTransport)
                return IsServerRelayTransport ? "Server relay active" : "Disabled";

            if (IsHostLocal)
                return IsSteamTransportRunning
                    ? $"Hosting ({SteamConnectedPeerCount} peer)"
                    : "Host socket pending";

            if (IsSteamTransportConnectedToHost)
                return "Connected to host";

            return IsSteamTransportRunning ? SteamConnectionPhase : "Waiting transport";
        }
    }

    [Header("P2P Host Ping")]
    [SerializeField, ReadOnly] int _hostPingIntervalMs = 1000;
    [SerializeField, ReadOnly] int _hostPingTimeoutMs = 2000;
    [SerializeField, ReadOnly] int _hostPingMaxMiss = 2;
    [SerializeField, ReadOnly] bool _hostPingRunning;
    [SerializeField, ReadOnly] int _hostPingSeq;
    [SerializeField, ReadOnly] int _hostPingMissCount;
    [SerializeField, ReadOnly] long _hostLastPongAtMs;
    [SerializeField, ReadOnly] long _hostLastRttMs;
    [SerializeField, ReadOnly] long _hostAvgRttMs;
    [SerializeField, ReadOnly] long _hostMaxRttMs;
    [SerializeField, ReadOnly] long _hostMinRttMs = long.MaxValue;
    [SerializeField, ReadOnly] long _hostLastRawRttMs;
    [SerializeField, ReadOnly] long _hostLastProcMs;
    [SerializeField, ReadOnly] string _hostPingStatus = "Idle";

    [Header("Steam Pair RTT")]
    [SerializeField, ReadOnly] int _transportPairPingMs = -1;
    [SerializeField, ReadOnly] float _transportQualityLocal = -1f;
    [SerializeField, ReadOnly] float _transportQualityRemote = -1f;
    [SerializeField, ReadOnly] int _transportPendingReliable;
    [SerializeField, ReadOnly] int _transportPendingUnreliable;
    [SerializeField, ReadOnly] int _transportSentUnackedReliable;

    [Header("Gameplay RTT")]
    [SerializeField, ReadOnly] long _gameplayLastStartRttMs;
    [SerializeField, ReadOnly] long _gameplayAvgStartRttMs;
    [SerializeField, ReadOnly] long _gameplayMaxStartRttMs;
    [SerializeField, ReadOnly] long _gameplayLastResultRttMs;
    [SerializeField, ReadOnly] long _gameplayAvgResultRttMs;
    [SerializeField, ReadOnly] long _gameplayMaxResultRttMs;
    [SerializeField, ReadOnly] string _gameplayTelemetryStatus = "Idle";
    [Header("Retry / Fallback Timeline")]
    [SerializeField, ReadOnly] long _fallbackActivatedAtMs;
    [SerializeField, ReadOnly] long _recoveryObservedAtMs;
    [SerializeField, ReadOnly] string _fallbackReason = "None";
    [SerializeField, ReadOnly] bool _forceGuestRelayForGameplay;

    private const float HostPingEmaAlpha = 0.2f;
    private const float GameplayTelemetryEmaAlpha = 0.2f;
    private const long GameplayTelemetryTimeoutMs = 15000;

    private sealed class PendingGameplayAction
    {
        public int ActorId;
        public int ActionKind;
        public int SlotIndex;
        public int TargetX;
        public int TargetY;
        public long ClientSendTimeMs;
        public long SentAtMs;
        public bool InstantObserved;
    }

    private readonly Dictionary<string, string> _steamIdByUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _uidBySteamId = new(StringComparer.Ordinal);
    private readonly List<PendingGameplayAction> _pendingGameplayActions = new();

    private CancellationTokenSource _hostPingCts;
    private long _hostPingSentCount;
    private long _hostPingRecvCount;
    private int _hostLastPongSeq;
    private string _lastHostStateLogSignature = "";
    private P2PTransportMode _transportMode;
    private SessionDtos.MatchManifestDto _matchManifest;
    private ISteamP2PClientTransport _steamTransport;
    private SteamP2PConnectionStatsSnapshot _transportPairStats;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _steamTransport ??= SteamP2PClientTransportFactory.Create();
        _steamTransport.OnPayloadReceived -= HandleSteamPayloadReceived;
        _steamTransport.OnPayloadReceived += HandleSteamPayloadReceived;
    }

    private void Update()
    {
        P2PDebugConfig.PollRuntimeToggle();
        _steamTransport?.Pump();
        RefreshTransportPairStats();
        RefreshGuestGameplayFallbackState(P2PRelayDiagnosticsPackets.NowMs());
        PurgeExpiredGameplayTelemetry(P2PRelayDiagnosticsPackets.NowMs());
    }

    private void OnDestroy()
    {
        if (_steamTransport != null)
        {
            _steamTransport.OnPayloadReceived -= HandleSteamPayloadReceived;
            _steamTransport.Stop();
        }

        if (_instance == this)
            _instance = null;
    }

    public void ConfigureRelay(string ticketKey)
    {
        ConfigureSession(ticketKey, SessionContext.Instance.LastMatchManifest);
    }

    public void ConfigureSession(string ticketKey, SessionDtos.MatchManifestDto manifest)
    {
        RelayKey = ticketKey ?? "";
        _matchManifest = manifest;

        ApplyManifest(manifest);

        _transportMode = ResolveTransportMode(ticketKey, manifest);
        ResetTransportFallbackTimeline();
        if (IsSteamTransport)
            RelayKey = !string.IsNullOrWhiteSpace(manifest?.MatchId)
                ? $"steam:{manifest.MatchId}"
                : $"steam:{HostSteamId64}";

        P2PTransportDiagnostics.Reset($"{TransportName}:{RelayKey}:{HostUid}");

        Debug.Log(
            $"[P2PPlayerSync] ConfigureSession key={RelayKey} transport={TransportName} " +
            $"manifest={manifest?.MatchId ?? "-"} hostUid={HostUid} hostSteam={HostSteamId64} " +
            $"participants={FormatManifestParticipants(manifest)}");

        HostActorId = 0;
        IsHostLocal = false;

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.ResetForMatchEnd();

        UpdateHostLogWindow();

        if (!IsP2PMode)
        {
            ResetState();
            return;
        }

        SyncHostState();
    }

    public void Reset()
    {
        RelayKey = "";
        ResetState();
    }

    private void ResetState()
    {
        StopHostPingLoop();
        ResetHostPingStats();
        ResetTransportPairStats();
        ResetGameplayTelemetry();
        _transportMode = P2PTransportMode.Disabled;
        HostActorId = 0;
        HostUid = "";
        HostSteamId64 = "";
        HostEpoch = 0;
        IsHostLocal = false;
        _matchManifest = null;
        _steamIdByUid.Clear();
        _uidBySteamId.Clear();
        _steamTransport?.Stop();
        _lastHostStateLogSignature = "";
        ResetTransportFallbackTimeline();
        P2PTransportDiagnostics.Reset("ResetState");

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.ResetForMatchEnd();

        if (P2PContentDirector.HasInstance)
            P2PContentDirector.Instance.ResetMatchState();

        if (P2PHostLogWindow.HasInstance)
            P2PHostLogWindow.Instance.HideAndClear();
    }

    public void RecordGameplayActionSent(int actorId, ActionKind actionKind, int slotIndex, int targetX, int targetY, long clientSendTimeMs)
    {
        if (!IsP2PMode || IsHostLocal || actorId <= 0)
            return;

        long now = P2PRelayDiagnosticsPackets.NowMs();
        PurgeExpiredGameplayTelemetry(now);

        _pendingGameplayActions.Add(new PendingGameplayAction
        {
            ActorId = actorId,
            ActionKind = (int)actionKind,
            SlotIndex = slotIndex,
            TargetX = targetX,
            TargetY = targetY,
            ClientSendTimeMs = clientSendTimeMs,
            SentAtMs = now
        });

        _gameplayTelemetryStatus = "Tracking";
    }

    public void RecordGameplayInstantFeedback(int actorId)
    {
        if (!IsP2PMode || IsHostLocal || actorId <= 0)
            return;

        long now = P2PRelayDiagnosticsPackets.NowMs();
        PurgeExpiredGameplayTelemetry(now);

        int index = FindPendingGameplayActionIndex(actorId, preferSkillLike: true, requireInstantPending: true);
        if (index < 0)
            return;

        var pending = _pendingGameplayActions[index];
        pending.InstantObserved = true;
        _pendingGameplayActions[index] = pending;

        long rtt = Math.Max(0, now - pending.SentAtMs);
        _gameplayLastStartRttMs = rtt;
        if (_gameplayAvgStartRttMs == 0)
            _gameplayAvgStartRttMs = rtt;
        else
            _gameplayAvgStartRttMs = (long)(GameplayTelemetryEmaAlpha * rtt + (1f - GameplayTelemetryEmaAlpha) * _gameplayAvgStartRttMs);

        _gameplayMaxStartRttMs = Math.Max(_gameplayMaxStartRttMs, rtt);
        _gameplayTelemetryStatus = "Start Ack";
    }

    public void RecordGameplayBeatResult(int actorId, int resultActionKind, bool accepted, int fromX, int fromY, int toX, int toY)
    {
        if (!IsP2PMode || IsHostLocal || actorId <= 0)
            return;

        long now = P2PRelayDiagnosticsPackets.NowMs();
        PurgeExpiredGameplayTelemetry(now);

        bool preferSkillLike = resultActionKind == (int)ActionKind.Skill;
        int index = FindPendingGameplayActionIndex(actorId, preferSkillLike, requireInstantPending: false);
        if (index < 0)
            return;

        var pending = _pendingGameplayActions[index];
        _pendingGameplayActions.RemoveAt(index);

        long rtt = Math.Max(0, now - pending.SentAtMs);
        _gameplayLastResultRttMs = rtt;
        if (_gameplayAvgResultRttMs == 0)
            _gameplayAvgResultRttMs = rtt;
        else
            _gameplayAvgResultRttMs = (long)(GameplayTelemetryEmaAlpha * rtt + (1f - GameplayTelemetryEmaAlpha) * _gameplayAvgResultRttMs);

        _gameplayMaxResultRttMs = Math.Max(_gameplayMaxResultRttMs, rtt);
        _gameplayTelemetryStatus = accepted
            ? "Result Ack"
            : $"Result Reject ({fromX},{fromY})->({toX},{toY})";
    }

    public void SyncHostState()
    {
        if (!IsP2PMode)
            return;

        if (IsSteamTransport)
            ResolveHostStateFromManifest();

        RefreshHostState();
    }

    internal void RefreshHostLogWindow()
    {
        UpdateHostLogWindow();
    }

    public void HandleHostChange(SC_HostChange pkt)
    {
        int nextHostActorId = pkt?.HostActorId ?? 0;
        Debug.Log(
            $"[P2PPlayerSync] HostChange prev={HostActorId} next={nextHostActorId} " +
            $"localActor={SessionContext.Instance?.MyActorId ?? 0} transport={TransportName}");

        if (HostActorId != nextHostActorId)
        {
            StopHostPingLoop();
            ResetHostPingStats();
            ResetGameplayTelemetry();
            ResetTransportFallbackTimeline();
        }

        HostActorId = nextHostActorId;

        if (IsSteamTransport)
            ApplySteamRuntimeHostChange(nextHostActorId);

        RefreshHostState();
    }

    public void HandleGuestPayload(CS_P2PPayload pkt)
    {
        if (!IsP2PMode || pkt == null)
            return;

        if (!TryDecodeBase64(pkt.Payload, out var payloadBytes))
            return;

        HandleIncomingGuestPayloadBytes(pkt.SenderActorId, payloadBytes, "", "ServerRelay");
    }

    public void HandleRelayBroadcast(SC_P2PBroadcast pkt)
    {
        if (!IsP2PMode || pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return;

        if (!TryDecodeBase64(pkt.Payload, out var payloadBytes))
            return;

        HandleIncomingHostPayloadBytes(payloadBytes, "ServerRelay");
    }

    public void SendWrappedPacket(IPacket packet)
    {
        if (packet == null || NetworkManager.Instance == null)
            return;

        if (!IsP2PMode)
        {
            NetworkManager.Instance.Send(packet.Write());
            return;
        }

        var segment = packet.Write();
        if (segment.Array == null || segment.Count <= 0)
            return;

        if (IsSteamTransport)
        {
            var payloadBytes = CopySegment(segment);
            if (payloadBytes.Length == 0)
                return;

            if (!IsHostLocal && ShouldForceRelayForGuestGameplay(payloadBytes, out var forcedFallbackReason))
            {
                MarkRelayFallback(forcedFallbackReason);
                P2PTransportDiagnostics.RecordFallback(
                    forcedFallbackReason,
                    FormatPacketProtocol(PeekPacketProtocol(payloadBytes)),
                    FormatBridgeContext());
                SendViaServerRelay(segment);
                return;
            }

            if (TrySendViaSteamTransport(payloadBytes))
                return;

            SendViaServerRelay(segment);
            return;
        }

        SendViaServerRelay(segment);
    }

    public void ReportGuestActionTrace(
        int targetActorId,
        CS_ActionRequest req,
        P2PActionTraceStage stage,
        P2PActionTraceReason reason = P2PActionTraceReason.None,
        long executeBeat = 0,
        int detailValue = 0,
        int resultX = 0,
        int resultY = 0)
    {
        if (!IsP2PMode || targetActorId <= 0 || req == null)
            return;

        SendWrappedPacket(new P2PActionTracePacket
        {
            TargetActorId = targetActorId,
            ActorId = req.ActorId,
            ActionKind = req.ActionKind,
            SlotIndex = req.SlotIndex,
            TargetX = req.TargetX,
            TargetY = req.TargetY,
            ClientSendTimeMs = req.ClientSendTimeMs,
            StageCode = (int)stage,
            ReasonCode = (int)reason,
            DetailValue = detailValue,
            ResultX = resultX,
            ResultY = resultY,
            ExecuteBeat = executeBeat,
            HostObservedMs = P2PRelayDiagnosticsPackets.NowMs()
        });
    }

    public void DispatchLocal(IPacket packet)
    {
        if (packet == null || NetworkManager.Instance == null)
            return;

        var session = NetworkManager.Instance.CurrentSession;

        try
        {
            IsDispatchingLocal = true;
            PacketManager.Instance.HandlePacket(session, packet);
        }
        finally
        {
            IsDispatchingLocal = false;
        }
    }

    private void RefreshHostState()
    {
        if (!IsP2PMode)
        {
            HostActorId = 0;
            IsHostLocal = false;
            _steamTransport?.Stop();
            StopHostPingLoop();
            ResetHostPingStats();
            ResetTransportPairStats();
            ResetGameplayTelemetry();
            if (P2PHostController.HasInstance)
                P2PHostController.Instance.SetHostMode(false);

            UpdateHostLogWindow();
            return;
        }

        IsHostLocal = ResolveLocalHostOwnership(out string ownershipReason);

        if (IsSteamTransport && IsHostLocal && HostActorId <= 0)
        {
            int sessionActorId = SessionContext.Instance?.MyActorId ?? 0;
            if (sessionActorId > 0)
                HostActorId = sessionActorId;
        }

        string hostAuthorityState = GetHostAuthorityState();
        bool hostAuthorityReady = string.Equals(hostAuthorityState, "Ready", StringComparison.Ordinal);

        P2PHostController.Instance.SetHostActorId(HostActorId);
        P2PHostController.Instance.SetHostMode(hostAuthorityReady);
        if (P2PContentDirector.HasInstance)
            P2PContentDirector.Instance.OnHostLocalChanged(hostAuthorityReady);

        if (IsSteamTransport)
            SyncSteamTransport();
        else
            _steamTransport?.Stop();

        RefreshHostPingLoopState();
        UpdateHostLogWindow();
        LogHostStateIfChanged(hostAuthorityState, ownershipReason);
    }

    private void UpdateHostLogWindow()
    {
        if (!P2PDebugConfig.LogOverheadEnabled)
        {
            if (P2PHostLogWindow.HasInstance)
            {
                P2PHostLogWindow.Instance.SetCaptureEnabled(false);
                P2PHostLogWindow.Instance.HideAndClear();
            }
            return;
        }

        if (!IsP2PMode)
        {
            if (P2PHostLogWindow.HasInstance)
            {
                P2PHostLogWindow.Instance.SetCaptureEnabled(false);
                P2PHostLogWindow.Instance.HideAndClear();
            }

            return;
        }

        P2PHostLogWindow.Instance.SetCaptureEnabled(true);
        P2PHostLogWindow.Instance.SetRelayContext(RelayKey, IsP2PMode, IsHostLocal, HostActorId);
    }

    private void RefreshHostPingLoopState()
    {
        if (!HasRemoteHostPing)
        {
            StopHostPingLoop();
            _hostPingStatus = IsP2PMode
                ? (IsHostLocal ? "Local Host" : "Waiting Host")
                : "P2P Off";
            return;
        }

        if (_hostPingRunning)
            return;

        StartHostPingLoop();
    }

    private void StartHostPingLoop()
    {
        if (_hostPingRunning)
            return;

        ResetHostPingStats();
        _hostPingStatus = "Warming Up";
        _hostPingRunning = true;
        _hostPingCts = new CancellationTokenSource();
        _ = HostPingLoopAsync(_hostPingCts.Token);
    }

    private void StopHostPingLoop()
    {
        if (!_hostPingRunning && _hostPingCts == null)
            return;

        _hostPingCts?.Cancel();
        _hostPingCts = null;
        _hostPingRunning = false;
    }

    private void ResetHostPingStats()
    {
        _hostPingSeq = 0;
        _hostLastPongSeq = 0;
        _hostPingMissCount = 0;
        _hostLastPongAtMs = 0;
        _hostLastRttMs = 0;
        _hostAvgRttMs = 0;
        _hostMaxRttMs = 0;
        _hostMinRttMs = long.MaxValue;
        _hostLastRawRttMs = 0;
        _hostLastProcMs = 0;
        _hostPingSentCount = 0;
        _hostPingRecvCount = 0;
        _hostPingStatus = "Idle";
    }

    private void RefreshTransportPairStats()
    {
        if (!IsSteamTransport || _steamTransport == null)
        {
            ResetTransportPairStats();
            return;
        }

        if (!_steamTransport.TryGetConnectionStats(out _transportPairStats))
        {
            ResetTransportPairStats();
            return;
        }

        _transportPairPingMs = _transportPairStats.PingMs;
        _transportQualityLocal = _transportPairStats.ConnectionQualityLocal;
        _transportQualityRemote = _transportPairStats.ConnectionQualityRemote;
        _transportPendingReliable = _transportPairStats.PendingReliable;
        _transportPendingUnreliable = _transportPairStats.PendingUnreliable;
        _transportSentUnackedReliable = _transportPairStats.SentUnackedReliable;
        MarkSteamRecoveryObserved();
    }

    private void ResetTransportPairStats()
    {
        _transportPairStats = default;
        _transportPairPingMs = -1;
        _transportQualityLocal = -1f;
        _transportQualityRemote = -1f;
        _transportPendingReliable = 0;
        _transportPendingUnreliable = 0;
        _transportSentUnackedReliable = 0;
    }

    private void ResetTransportFallbackTimeline()
    {
        _fallbackActivatedAtMs = 0L;
        _recoveryObservedAtMs = 0L;
        _fallbackReason = "None";
        _forceGuestRelayForGameplay = false;
    }

    private void MarkRelayFallback(string reason)
    {
        if (!IsSteamTransport)
            return;

        long nowMs = P2PRelayDiagnosticsPackets.NowMs();
        if (_fallbackActivatedAtMs <= 0)
            _fallbackActivatedAtMs = nowMs;

        _fallbackReason = string.IsNullOrWhiteSpace(reason) ? "SteamFallback" : reason;
    }

    private void MarkSteamRecoveryObserved()
    {
        if (!IsSteamTransport || !IsSteamTransportConnectedToHost || _fallbackActivatedAtMs <= 0)
            return;

        if (_recoveryObservedAtMs <= 0)
            _recoveryObservedAtMs = P2PRelayDiagnosticsPackets.NowMs();
    }

    private void ResetGameplayTelemetry()
    {
        _pendingGameplayActions.Clear();
        _gameplayLastStartRttMs = 0;
        _gameplayAvgStartRttMs = 0;
        _gameplayMaxStartRttMs = 0;
        _gameplayLastResultRttMs = 0;
        _gameplayAvgResultRttMs = 0;
        _gameplayMaxResultRttMs = 0;
        _gameplayTelemetryStatus = "Idle";
        _forceGuestRelayForGameplay = false;
    }

    private void PurgeExpiredGameplayTelemetry(long nowMs)
    {
        if (_pendingGameplayActions.Count <= 0)
            return;

        for (int i = _pendingGameplayActions.Count - 1; i >= 0; i--)
        {
            var pending = _pendingGameplayActions[i];
            if (Math.Max(0, nowMs - pending.SentAtMs) <= GameplayTelemetryTimeoutMs)
                continue;

            _pendingGameplayActions.RemoveAt(i);
            _gameplayTelemetryStatus = "Telemetry Timeout";
        }
    }

    private void RefreshGuestGameplayFallbackState(long nowMs)
    {
        if (!IsSteamTransport || IsHostLocal)
            return;

        if (P2PTransportDiagnostics.HasPendingHostSeenTimeout(out var ageMs, out var summary))
        {
            _forceGuestRelayForGameplay = true;
            _gameplayTelemetryStatus = $"HostSeenTimeout {ageMs}ms";
            MarkRelayFallback("GuestUplinkTimeout");
            P2PTransportDiagnostics.MarkPendingHostSeenTimeout();
            if (P2PDebugConfig.TraceHostFlow)
                Debug.LogWarning($"[P2PRelayClientBridge] Guest uplink unhealthy. forcing server relay. {summary}");
            return;
        }

        if (_hostPingMissCount >= _hostPingMaxMiss)
        {
            _forceGuestRelayForGameplay = true;
            _gameplayTelemetryStatus = "DirectProbeTimeout";
            MarkRelayFallback("GuestDirectProbeTimeout");
            return;
        }

        if (_forceGuestRelayForGameplay
            && _hostLastPongAtMs > 0
            && Math.Max(0, nowMs - _hostLastPongAtMs) <= _hostPingTimeoutMs
            && string.Equals(_hostPingStatus, "Host OK", StringComparison.Ordinal))
        {
            _forceGuestRelayForGameplay = false;
            _gameplayTelemetryStatus = "DirectProbeRecovered";
            MarkSteamRecoveryObserved();
        }
    }

    private int FindPendingGameplayActionIndex(int actorId, bool preferSkillLike, bool requireInstantPending)
    {
        for (int i = 0; i < _pendingGameplayActions.Count; i++)
        {
            var pending = _pendingGameplayActions[i];
            if (pending.ActorId != actorId)
                continue;

            bool isSkillLike = pending.ActionKind == (int)ActionKind.Attack
                               || pending.ActionKind == (int)ActionKind.Skill;
            if (preferSkillLike != isSkillLike)
                continue;

            if (requireInstantPending && pending.InstantObserved)
                continue;

            return i;
        }

        return -1;
    }

    private async Task HostPingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!HasRemoteHostPing || NetworkManager.Instance == null || SessionContext.Instance == null)
            {
                _hostPingStatus = IsP2PMode ? "Waiting Host" : "P2P Off";
                try { await Task.Delay(250, ct); } catch (TaskCanceledException) { break; }
                continue;
            }

            int requesterActorId = SessionContext.Instance.MyActorId;
            if (requesterActorId <= 0)
            {
                _hostPingStatus = "Waiting Actor";
                try { await Task.Delay(250, ct); } catch (TaskCanceledException) { break; }
                continue;
            }

            int seq = Interlocked.Increment(ref _hostPingSeq);
            long now = P2PRelayDiagnosticsPackets.NowMs();
            _hostPingSentCount++;

            SendWrappedPacket(new P2PHostPingRequestPacket
            {
                RequesterActorId = requesterActorId,
                TargetHostActorId = HostActorId,
                Seq = seq,
                ClientSendMs = now
            });

            long deadline = now + _hostPingTimeoutMs;
            while (!ct.IsCancellationRequested && P2PRelayDiagnosticsPackets.NowMs() < deadline && _hostLastPongSeq < seq)
                await Task.Yield();

            if (ct.IsCancellationRequested)
                break;

            if (_hostLastPongSeq < seq)
            {
                _hostPingMissCount++;
                _hostPingStatus = _hostPingMissCount >= _hostPingMaxMiss
                    ? "Host Timeout"
                    : $"Host Miss {_hostPingMissCount}/{_hostPingMaxMiss}";
            }
            else
            {
                _hostPingMissCount = 0;
                _hostPingStatus = "Host OK";
            }

            try { await Task.Delay(_hostPingIntervalMs, ct); } catch (TaskCanceledException) { break; }
        }

        _hostPingRunning = false;
    }

    private void HandleSteamPayloadReceived(SteamP2PIncomingPayload payload)
    {
        if (!IsSteamTransport || payload == null || payload.PayloadBytes == null || payload.PayloadBytes.Length == 0)
            return;

        ushort protocol = PeekPacketProtocol(payload.PayloadBytes);
        if (payload.IsFromHost)
        {
            if (P2PDebugConfig.TraceRealtimeTransport)
            {
                Debug.Log(
                    $"[P2PTransport] SteamRecv dir=HostToClient protocol={FormatPacketProtocol(protocol)} bytes={payload.PayloadBytes.Length} " +
                    $"{FormatBridgeContext()}");
            }
            HandleIncomingHostPayloadBytes(payload.PayloadBytes, "SteamHostBroadcast");
            return;
        }

        int senderActorId = ResolveActorIdForSteamId(payload.SenderSteamId64);
        if (P2PDebugConfig.TraceRealtimeTransport)
        {
            Debug.Log(
                $"[P2PTransport] SteamRecv dir=GuestToHost senderSteam={payload.SenderSteamId64} senderActor={senderActorId} " +
                $"protocol={FormatPacketProtocol(protocol)} bytes={payload.PayloadBytes.Length} {FormatBridgeContext()}");
        }
        HandleIncomingGuestPayloadBytes(senderActorId, payload.PayloadBytes, payload.SenderSteamId64, "SteamGuestToHost");
    }

    private void HandleIncomingGuestPayloadBytes(
        int senderActorId,
        byte[] payloadBytes,
        string senderSteamId64 = "",
        string transportSource = "Unknown")
    {
        if (!IsP2PMode || payloadBytes == null || payloadBytes.Length == 0)
            return;

        if (TryHandleGuestDiagnostics(senderActorId, payloadBytes))
            return;

        ushort protocol = PeekPacketProtocol(payloadBytes);
        if (senderActorId <= 0)
        {
            if (TryResolveGuestActorIdFromPayload(payloadBytes, out var fallbackActorId))
            {
                senderActorId = fallbackActorId;
                P2PTransportDiagnostics.RecordActorFallbackRecovered(
                    senderSteamId64,
                    senderActorId,
                    FormatPacketProtocol(protocol));
                if (P2PDebugConfig.TraceHostFlow)
                {
                    Debug.LogWarning(
                        $"[P2PRelayClientBridge] Guest actor resolved from payload fallback. " +
                        $"steam={senderSteamId64} actor={senderActorId} protocol={FormatPacketProtocol(protocol)} {FormatBridgeContext()}");
                }
            }
        }

        if (senderActorId <= 0)
        {
            P2PTransportDiagnostics.RecordActorResolveDrop(
                senderSteamId64,
                FormatPacketProtocol(protocol),
                $"{transportSource} {FormatBridgeContext()}");
            Debug.LogWarning(
                $"[P2PRelayClientBridge] Drop guest payload because sender actor could not be resolved. " +
                $"steam={senderSteamId64} protocol={FormatPacketProtocol(protocol)} bytes={payloadBytes.Length} {FormatBridgeContext()}");
            return;
        }

        P2PTransportDiagnostics.RecordIncoming(
            transportSource,
            FormatPacketProtocol(protocol),
            senderActorId,
            string.IsNullOrWhiteSpace(senderSteamId64)
                ? FormatBridgeContext()
                : $"steam={senderSteamId64} {FormatBridgeContext()}");

        if (P2PDebugConfig.TraceRealtimeTransport)
        {
            Debug.Log(
                $"[P2PTransport] QueueGuestPayload senderActor={senderActorId} protocol={FormatPacketProtocol(protocol)} " +
                $"bytes={payloadBytes.Length} {FormatBridgeContext()}");
        }

        P2PHostController.Instance.EnqueueGuestActionRequest(new CS_P2PPayload
        {
            SenderActorId = senderActorId,
            Payload = Convert.ToBase64String(payloadBytes)
        });
    }

    private static bool TryResolveGuestActorIdFromPayload(byte[] payloadBytes, out int actorId)
    {
        actorId = 0;
        if (payloadBytes == null || payloadBytes.Length < 4)
            return false;

        ushort protocol = PeekPacketProtocol(payloadBytes);
        if (protocol != (ushort)PacketID.CS_ActionRequest)
            return false;

        try
        {
            var request = new CS_ActionRequest();
            request.Read(new ArraySegment<byte>(payloadBytes));
            actorId = request.ActorId;
            return actorId > 0;
        }
        catch (Exception ex)
        {
            if (P2PDebugConfig.TraceHostFlow)
                Debug.LogWarning($"[P2PRelayClientBridge] Failed to resolve guest actor from payload: {ex.Message}");
            actorId = 0;
            return false;
        }
    }

    private void HandleIncomingHostPayloadBytes(byte[] payloadBytes, string transportSource = "Unknown")
    {
        if (payloadBytes == null || payloadBytes.Length == 0)
            return;

        if (TryHandleInboundDiagnostics(payloadBytes))
            return;

        ushort protocol = PeekPacketProtocol(payloadBytes);
        if (P2PDebugConfig.TraceRealtimeTransport)
        {
            Debug.Log(
                $"[P2PTransport] DispatchHostPayload protocol={FormatPacketProtocol(protocol)} bytes={payloadBytes.Length} " +
                $"{FormatBridgeContext()}");
        }

        P2PTransportDiagnostics.RecordIncoming(
            transportSource,
            FormatPacketProtocol(protocol),
            SessionContext.Instance?.MyActorId ?? 0,
            FormatBridgeContext());

        var session = NetworkManager.Instance != null ? NetworkManager.Instance.CurrentSession : null;

        try
        {
            PacketManager.Instance.OnRecvPacket(session, new ArraySegment<byte>(payloadBytes));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PRelayClientBridge] Failed to dispatch inbound packet: {ex.Message}");
        }
    }

    private bool TryHandleGuestDiagnostics(int senderActorId, byte[] bytes)
    {
        if (!IsHostLocal || bytes == null || bytes.Length < 4)
            return false;

        try
        {
            if (P2PRelayDiagnosticsPackets.PeekProtocol(bytes) != P2PRelayDiagnosticsPackets.HostPingRequestProtocol)
                return false;

            var request = new P2PHostPingRequestPacket();
            request.Read(new ArraySegment<byte>(bytes));

            if (request.TargetHostActorId > 0 && HostActorId > 0 && request.TargetHostActorId != HostActorId)
                return true;

            long hostRecvMs = P2PRelayDiagnosticsPackets.NowMs();
            SendWrappedPacket(new P2PHostPingPongPacket
            {
                TargetActorId = senderActorId > 0 ? senderActorId : request.RequesterActorId,
                HostActorId = HostActorId,
                Seq = request.Seq,
                ClientSendMs = request.ClientSendMs,
                HostRecvMs = hostRecvMs,
                HostSendMs = P2PRelayDiagnosticsPackets.NowMs()
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PRelayClientBridge] Failed to handle host ping request: {ex.Message}");
            return false;
        }
    }

    private bool TryHandleInboundDiagnostics(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4)
            return false;

        ushort protocol = P2PRelayDiagnosticsPackets.PeekProtocol(bytes);
        if (protocol == P2PRelayDiagnosticsPackets.HostPingPongProtocol)
        {
            var pong = new P2PHostPingPongPacket();
            pong.Read(new ArraySegment<byte>(bytes));
            HandleHostPingPong(pong);
            return true;
        }

        if (protocol == P2PRelayDiagnosticsPackets.ActionTraceProtocol)
        {
            var trace = new P2PActionTracePacket();
            trace.Read(new ArraySegment<byte>(bytes));
            HandleActionTracePacket(trace);
            return true;
        }

        return false;
    }

    private void HandleHostPingPong(P2PHostPingPongPacket pong)
    {
        if (pong == null || SessionContext.Instance == null || SessionContext.Instance.MyActorId <= 0)
            return;

        if (pong.TargetActorId != SessionContext.Instance.MyActorId)
            return;

        if (!HasRemoteHostPing)
            return;

        if (pong.HostActorId != HostActorId)
            return;

        if (pong.Seq < _hostLastPongSeq)
            return;

        long localRecvMs = P2PRelayDiagnosticsPackets.NowMs();
        long procMs = Math.Max(0, pong.HostSendMs - pong.HostRecvMs);
        long rawRtt = Math.Max(0, localRecvMs - pong.ClientSendMs);
        long rtt = Math.Max(0, rawRtt - procMs);

        _hostLastPongSeq = Math.Max(_hostLastPongSeq, pong.Seq);
        _hostLastPongAtMs = localRecvMs;
        _hostLastRawRttMs = rawRtt;
        _hostLastProcMs = procMs;
        _hostLastRttMs = rtt;
        _hostPingRecvCount++;

        if (_hostAvgRttMs == 0)
            _hostAvgRttMs = rtt;
        else
            _hostAvgRttMs = (long)(HostPingEmaAlpha * rtt + (1f - HostPingEmaAlpha) * _hostAvgRttMs);

        _hostMaxRttMs = Math.Max(_hostMaxRttMs, rtt);
        _hostMinRttMs = Math.Min(_hostMinRttMs, rtt);
        _hostPingStatus = "Host OK";
    }

    private void HandleActionTracePacket(P2PActionTracePacket trace)
    {
        if (trace == null || SessionContext.Instance == null || SessionContext.Instance.MyActorId <= 0)
            return;

        if (trace.TargetActorId != SessionContext.Instance.MyActorId)
            return;

        P2PTransportDiagnostics.RecordActionTrace(trace);
    }

    private void ApplyManifest(SessionDtos.MatchManifestDto manifest)
    {
        HostUid = manifest?.HostUid ?? "";
        HostSteamId64 = manifest?.HostSteamId64 ?? "";
        HostEpoch = manifest?.HostEpoch ?? 0;

        _steamIdByUid.Clear();
        _uidBySteamId.Clear();

        if (manifest?.Participants == null)
            return;

        foreach (var participant in manifest.Participants)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.Uid))
                continue;

            string steamId64 = participant.SteamId64 ?? "";
            _steamIdByUid[participant.Uid] = steamId64;
            if (!string.IsNullOrWhiteSpace(steamId64))
                _uidBySteamId[steamId64] = participant.Uid;
        }

        LogManifestDiagnostics(manifest);
    }

    private string GetHostAuthorityState()
    {
        if (!IsP2PMode)
            return "P2PDisabled";

        if (!IsHostLocal)
            return "Guest";

        if (HostActorId <= 0)
            return "HostActorMissing";

        int sessionActorId = SessionContext.Instance?.MyActorId ?? 0;
        if (sessionActorId <= 0)
            return "LocalActorMissing";

        if (sessionActorId != HostActorId)
            return $"HostActorMismatch local={sessionActorId}";

        var gs = ClientGameState.Instance;
        if (gs == null)
            return "ClientGameStateMissing";

        if (gs.MyActorId != sessionActorId)
            return $"GameStateActorMismatch gs={gs.MyActorId}";

        if (!gs.TryGetEntity(sessionActorId, out var entity))
            return "LocalPlayerEntityMissing";

        if (entity.EntityType != (int)EntityType.Player)
            return $"LocalEntityTypeMismatch type={entity.EntityType}";

        return "Ready";
    }

    private void LogHostStateIfChanged(string hostAuthorityState, string ownershipReason)
    {
        int localActor = SessionContext.Instance?.MyActorId ?? 0;
        string signature = $"{IsP2PMode}|{TransportName}|{IsHostLocal}|{HostActorId}|{HostUid}|{HostSteamId64}|{localActor}|{hostAuthorityState}|{ownershipReason}|{FormatLocalPlayerEntities()}";
        if (string.Equals(signature, _lastHostStateLogSignature, StringComparison.Ordinal))
            return;

        _lastHostStateLogSignature = signature;
        Debug.Log(
            $"[P2PPlayerSync] HostState role={(IsHostLocal ? "Host" : "Guest")} " +
            $"hostActor={HostActorId} localActor={localActor} authority={hostAuthorityState} hostUid={HostUid} " +
            $"transport={TransportName} state={TransportDebugStatus} ownership={ownershipReason} " +
            $"players={FormatLocalPlayerEntities()}");
    }

    private static string FormatManifestParticipants(SessionDtos.MatchManifestDto manifest)
    {
        if (manifest?.Participants == null || manifest.Participants.Count == 0)
            return "none";

        return string.Join(",", manifest.Participants.Select(p =>
        {
            if (p == null)
                return "null";

            string steam = string.IsNullOrWhiteSpace(p.SteamId64) ? "-" : p.SteamId64;
            string uid = string.IsNullOrWhiteSpace(p.Uid) ? "-" : p.Uid;
            return $"{p.ActorId}:{uid}/{steam}";
        }));
    }

    private static string FormatLocalPlayerEntities()
    {
        var gs = ClientGameState.Instance;
        if (gs == null)
            return "gs-null";

        var entries = gs.EnumerateEntities()
            .Where(e => e.EntityType == (int)EntityType.Player)
            .OrderBy(e => e.EntityId)
            .Select(e =>
            {
                string uid = gs.TryGetPlayerUid(e.EntityId, out var resolvedUid) && !string.IsNullOrWhiteSpace(resolvedUid)
                    ? resolvedUid
                    : "-";
                return $"{e.EntityId}:{uid}@({e.X},{e.Y}) hp={e.Hp} app={e.AppearanceId}";
            })
            .ToArray();

        return entries.Length == 0 ? "none" : string.Join(",", entries);
    }

    private void ApplySteamRuntimeHostChange(int hostActorId)
    {
        if (!TryResolveSteamHostIdentity(hostActorId, out var nextHostUid, out var nextHostSteamId64))
        {
            if (P2PDebugConfig.TraceHostFlow)
                Debug.LogWarning($"[P2PRelayClientBridge] Steam host identity unresolved actor={hostActorId}");
            return;
        }

        bool changed =
            !string.Equals(HostUid, nextHostUid, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(HostSteamId64, nextHostSteamId64, StringComparison.Ordinal);

        HostUid = nextHostUid;
        HostSteamId64 = nextHostSteamId64;
        if (changed)
            HostEpoch = Math.Max(1, HostEpoch + 1);

        if (_matchManifest != null)
        {
            _matchManifest.HostUid = HostUid;
            _matchManifest.HostSteamId64 = HostSteamId64;
            _matchManifest.HostEpoch = HostEpoch;
        }

        if (P2PDebugConfig.TraceHostFlow)
        {
            Debug.Log(
                $"[P2PRelayClientBridge] Steam host changed actor={hostActorId} uid={HostUid} steam={HostSteamId64} epoch={HostEpoch}");
        }
    }

    private P2PTransportMode ResolveTransportMode(string ticketKey, SessionDtos.MatchManifestDto manifest)
    {
        if (ShouldUseSteamTransport(manifest))
            return P2PTransportMode.SteamP2P;

        bool relayTicket = !string.IsNullOrWhiteSpace(ticketKey)
                           && ticketKey.StartsWith("p2p:", StringComparison.OrdinalIgnoreCase);
        return relayTicket ? P2PTransportMode.ServerRelay : P2PTransportMode.Disabled;
    }

    private bool ShouldUseSteamTransport(SessionDtos.MatchManifestDto manifest)
    {
        if (manifest == null)
            return false;

        if (string.IsNullOrWhiteSpace(manifest.NetworkMode)
            || manifest.NetworkMode.IndexOf("steam", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        var root = AppBootstrap.Instance != null ? AppBootstrap.Instance.Root : null;
        if (root?.Config == null || !root.Config.EnableSteam || !root.Config.PreferSteamP2PInGame)
            return false;

        if (root.SteamPlatform == null || !root.SteamPlatform.IsInitialized)
            return false;

        return !string.IsNullOrWhiteSpace(root.SteamPlatform.SteamId64)
               && !string.IsNullOrWhiteSpace(manifest.HostSteamId64);
    }

    private void ResolveHostStateFromManifest()
    {
        if (_matchManifest == null)
            return;

        HostUid = _matchManifest.HostUid ?? "";
        HostSteamId64 = _matchManifest.HostSteamId64 ?? "";
        HostEpoch = _matchManifest.HostEpoch;

        int resolvedActorId = TryGetManifestParticipantActorId(HostUid);
        if (resolvedActorId <= 0)
            resolvedActorId = ResolveActorIdForUid(HostUid);
        if (resolvedActorId <= 0
            && !string.IsNullOrWhiteSpace(HostUid)
            && string.Equals(HostUid, SessionContext.Instance.Uid, StringComparison.OrdinalIgnoreCase)
            && SessionContext.Instance.MyActorId > 0)
        {
            resolvedActorId = SessionContext.Instance.MyActorId;
        }

        HostActorId = resolvedActorId;
        if (P2PDebugConfig.LogOverheadEnabled)
        {
            Debug.Log(
                $"[P2PPlayerSync] ResolveHostStateFromManifest hostUid={HostUid} hostSteam={HostSteamId64} " +
                $"resolvedActor={resolvedActorId} sessionUid={SessionContext.Instance?.Uid ?? "-"} " +
                $"sessionActor={SessionContext.Instance?.MyActorId ?? 0} roster={FormatRosterActors()}");
        }
    }

    private bool ResolveLocalHostOwnership()
        => ResolveLocalHostOwnership(out _);

    private bool ResolveLocalHostOwnership(out string reason)
    {
        if (IsSteamTransport)
        {
            if (!string.IsNullOrWhiteSpace(HostUid)
                && string.Equals(HostUid, SessionContext.Instance.Uid, StringComparison.OrdinalIgnoreCase))
            {
                reason = "ManifestHostUidMatchesSessionUid";
                return true;
            }

            string localSteamId64 = GetLocalSteamId64();
            if (!string.IsNullOrWhiteSpace(HostSteamId64)
                && !string.IsNullOrWhiteSpace(localSteamId64)
                && string.Equals(HostSteamId64, localSteamId64, StringComparison.Ordinal))
            {
                reason = "ManifestHostSteamMatchesLocalSteam";
                return true;
            }

            if (string.IsNullOrWhiteSpace(HostUid) && string.IsNullOrWhiteSpace(HostSteamId64))
            {
                reason = "ManifestHostMissingPending";
                return false;
            }

            reason =
                $"SteamNotLocal hostUid={HostUid} sessionUid={SessionContext.Instance?.Uid ?? "-"} " +
                $"hostSteam={HostSteamId64} localSteam={GetLocalSteamId64()} hosting={_steamTransport?.IsHosting ?? false}";
            return false;
        }

        if (HostActorId > 0 && SessionContext.Instance.MyActorId == HostActorId)
        {
            reason = "ActorMatchesHostActor";
            return true;
        }

        reason = $"ActorMismatch hostActor={HostActorId} localActor={SessionContext.Instance?.MyActorId ?? 0}";
        return false;
    }

    private void SyncSteamTransport()
    {
        if (_steamTransport == null)
            return;

        var config = BuildSteamTransportConfig();
        if (config == null)
        {
            if (P2PDebugConfig.TraceRealtimeTransport)
                Debug.LogWarning($"[P2PTransport] Steam config unavailable. {FormatBridgeContext()}");
            MarkRelayFallback("SteamConfigUnavailable");
            _steamTransport.Stop();
            return;
        }

        if (P2PDebugConfig.TraceRealtimeTransport)
        {
            Debug.Log(
                $"[P2PTransport] SyncSteamTransport match={config.MatchId} isLocalHost={config.IsLocalHost} " +
                $"hostSteam={config.HostSteamId64} localSteam={config.LocalSteamId64} participants={string.Join(",", config.AllowedParticipantSteamId64s ?? Array.Empty<string>())}");
        }

        _steamTransport.Configure(config);
        _steamTransport.EnsureRunning();
    }

    private SteamP2PTransportConfig BuildSteamTransportConfig()
    {
        if (_matchManifest == null)
            return null;

        string localSteamId64 = GetLocalSteamId64();
        if (string.IsNullOrWhiteSpace(localSteamId64))
            return null;

        string[] participants = (_matchManifest.Participants ?? new List<SessionDtos.MatchParticipantDto>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SteamId64))
            .Select(x => x.SteamId64)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new SteamP2PTransportConfig
        {
            MatchId = _matchManifest.MatchId ?? "",
            RoomId = _matchManifest.RoomId ?? "",
            HostUid = HostUid ?? "",
            HostSteamId64 = HostSteamId64 ?? "",
            LocalSteamId64 = localSteamId64,
            HostEpoch = HostEpoch,
            IsLocalHost = ResolveLocalHostOwnership(),
            AllowedParticipantSteamId64s = participants
        };
    }

    private string GetLocalSteamId64()
    {
        return AppBootstrap.Instance?.Root?.SteamPlatform?.SteamId64 ?? "";
    }

    private int TryGetManifestParticipantActorId(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid) || _matchManifest?.Participants == null)
            return 0;

        var participant = _matchManifest.Participants.FirstOrDefault(x =>
            x != null
            && x.ActorId > 0
            && string.Equals(x.Uid, uid, StringComparison.OrdinalIgnoreCase));
        return participant?.ActorId ?? 0;
    }

    private bool TryGetManifestParticipantByActorId(int actorId, out SessionDtos.MatchParticipantDto participant)
    {
        participant = null;
        if (actorId <= 0 || _matchManifest?.Participants == null)
            return false;

        participant = _matchManifest.Participants.FirstOrDefault(x => x != null && x.ActorId == actorId);
        return participant != null;
    }

    private int ResolveActorIdForUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid) || ClientGameState.Instance == null)
            return 0;

        int resolvedActorId = 0;
        List<int> duplicateActors = null;
        foreach (var entry in ClientGameState.Instance.EnumeratePlayerRoster())
        {
            if (!string.Equals(entry.Uid, uid, StringComparison.OrdinalIgnoreCase))
                continue;

            if (resolvedActorId == 0)
            {
                resolvedActorId = entry.ActorId;
                continue;
            }

            duplicateActors ??= new List<int> { resolvedActorId };
            duplicateActors.Add(entry.ActorId);
        }

        if (duplicateActors != null)
        {
            Debug.LogWarning(
                $"[P2PPlayerSync] ResolveActorIdForUid ambiguous uid={uid} actors={string.Join(",", duplicateActors.OrderBy(x => x))}");
        }

        return resolvedActorId;
    }

    private int ResolveActorIdForSteamId(string steamId64)
    {
        if (string.IsNullOrWhiteSpace(steamId64))
            return 0;

        if (_uidBySteamId.TryGetValue(steamId64, out var uid))
        {
            int actorId = ResolveActorIdForUid(uid);
            if (P2PDebugConfig.TraceRealtimeTransport)
            {
                Debug.Log(
                    $"[P2PPlayerSync] ResolveActorIdForSteamId steam={steamId64} uid={uid} actor={actorId} roster={FormatRosterActors()}");
            }
            return actorId;
        }

        string localSteamId64 = GetLocalSteamId64();
        if (!string.IsNullOrWhiteSpace(localSteamId64)
            && string.Equals(localSteamId64, steamId64, StringComparison.Ordinal)
            && SessionContext.Instance.MyActorId > 0)
        {
            return SessionContext.Instance.MyActorId;
        }

        if (P2PDebugConfig.TraceRealtimeTransport)
        {
            Debug.LogWarning(
                $"[P2PPlayerSync] ResolveActorIdForSteamId miss steam={steamId64} localSteam={localSteamId64} roster={FormatRosterActors()}");
        }

        return 0;
    }

    private bool TryResolveSteamHostIdentity(int hostActorId, out string hostUid, out string hostSteamId64)
    {
        hostUid = "";
        hostSteamId64 = "";

        if (hostActorId <= 0)
            return false;

        if (TryGetManifestParticipantByActorId(hostActorId, out var manifestParticipant))
        {
            hostUid = manifestParticipant.Uid ?? "";
            hostSteamId64 = manifestParticipant.SteamId64 ?? "";
            if (!string.IsNullOrWhiteSpace(hostUid) && !string.IsNullOrWhiteSpace(hostSteamId64))
                return true;
        }

        if (ClientGameState.Instance != null
            && ClientGameState.Instance.TryGetPlayerUid(hostActorId, out hostUid)
            && !string.IsNullOrWhiteSpace(hostUid))
        {
            if (!_steamIdByUid.TryGetValue(hostUid, out hostSteamId64))
                hostSteamId64 = "";

            if (!string.IsNullOrWhiteSpace(hostSteamId64))
                return true;
        }

        if (hostActorId == SessionContext.Instance.MyActorId
            && !string.IsNullOrWhiteSpace(SessionContext.Instance.Uid))
        {
            hostUid = SessionContext.Instance.Uid;
            hostSteamId64 = GetLocalSteamId64();
            return !string.IsNullOrWhiteSpace(hostSteamId64);
        }

        return false;
    }

    private static bool TryDecodeBase64(string payload, out byte[] payloadBytes)
    {
        payloadBytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            payloadBytes = Convert.FromBase64String(payload);
            return payloadBytes.Length > 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PRelayClientBridge] Failed to decode payload: {ex.Message}");
            return false;
        }
    }

    private static byte[] CopySegment(ArraySegment<byte> segment)
    {
        if (segment.Array == null || segment.Count <= 0)
            return Array.Empty<byte>();

        var bytes = new byte[segment.Count];
        Buffer.BlockCopy(segment.Array, segment.Offset, bytes, 0, segment.Count);
        return bytes;
    }

    private bool ShouldForceRelayForGuestGameplay(byte[] payloadBytes, out string reason)
    {
        reason = "";
        if (!_forceGuestRelayForGameplay || payloadBytes == null || payloadBytes.Length < 4)
            return false;

        if (PeekPacketProtocol(payloadBytes) != (ushort)PacketID.CS_ActionRequest)
            return false;

        reason = string.IsNullOrWhiteSpace(_fallbackReason) || string.Equals(_fallbackReason, "None", StringComparison.Ordinal)
            ? "GuestUplinkFallbackLocked"
            : _fallbackReason;
        return true;
    }

    private bool TrySendViaSteamTransport(byte[] payloadBytes)
    {
        if (!IsSteamTransport || payloadBytes == null || payloadBytes.Length == 0)
            return false;

        ushort protocol = PeekPacketProtocol(payloadBytes);
        if (IsHostLocal)
        {
            int sent = _steamTransport?.SendHostToGuests(payloadBytes) ?? 0;
            if (P2PDebugConfig.TraceRealtimeTransport)
            {
                Debug.Log(
                    $"[P2PTransport] SendSteam route=HostToGuests protocol={FormatPacketProtocol(protocol)} bytes={payloadBytes.Length} " +
                    $"sentPeers={sent} connectedPeers={SteamConnectedPeerCount} {FormatBridgeContext()}");
            }
            if (sent > 0)
            {
                P2PTransportDiagnostics.RecordOutgoing(
                    "SteamHostToGuests",
                    FormatPacketProtocol(protocol),
                    true,
                    $"sentPeers={sent} connectedPeers={SteamConnectedPeerCount} phase={SteamConnectionPhase} route={SteamRouteHint}",
                    payloadBytes);
                MarkSteamRecoveryObserved();
                return true;
            }

            if (P2PDebugConfig.TraceHostFlow)
                Debug.LogWarning("[P2PRelayClientBridge] Steam host broadcast had no connected guest. Falling back to relay.");
            MarkRelayFallback("HostBroadcastNoConnectedSteamPeer");
            P2PTransportDiagnostics.RecordOutgoing(
                "SteamHostToGuests",
                FormatPacketProtocol(protocol),
                false,
                $"connectedPeers={SteamConnectedPeerCount} phase={SteamConnectionPhase}",
                payloadBytes);
            P2PTransportDiagnostics.RecordFallback(
                "HostBroadcastNoConnectedSteamPeer",
                FormatPacketProtocol(protocol),
                FormatBridgeContext());
            return false;
        }

        bool success = _steamTransport?.SendGuestToHost(payloadBytes) ?? false;
        if (P2PDebugConfig.TraceRealtimeTransport)
        {
            Debug.Log(
                $"[P2PTransport] SendSteam route=GuestToHost protocol={FormatPacketProtocol(protocol)} bytes={payloadBytes.Length} " +
                $"success={success} connectedToHost={IsSteamTransportConnectedToHost} hostSteam={HostSteamId64} {FormatBridgeContext()}");
        }

        if (success)
        {
            P2PTransportDiagnostics.RecordOutgoing(
                "SteamGuestToHost",
                FormatPacketProtocol(protocol),
                true,
                $"phase={SteamConnectionPhase} connectedToHost={IsSteamTransportConnectedToHost} route={SteamRouteHint}",
                payloadBytes);
            MarkSteamRecoveryObserved();
            return true;
        }

        if (P2PDebugConfig.TraceHostFlow)
            Debug.LogWarning($"[P2PRelayClientBridge] Steam send to host failed host={HostSteamId64}. Falling back to relay.");
        string fallbackReason = ResolveSteamFallbackReason();
        MarkRelayFallback(fallbackReason);
        P2PTransportDiagnostics.RecordOutgoing(
            "SteamGuestToHost",
            FormatPacketProtocol(protocol),
            false,
            $"phase={SteamConnectionPhase} connectedToHost={IsSteamTransportConnectedToHost} route={SteamRouteHint}",
            payloadBytes);
        P2PTransportDiagnostics.RecordFallback(
            fallbackReason,
            FormatPacketProtocol(protocol),
            FormatBridgeContext());
        return false;
    }

    private string ResolveSteamFallbackReason()
    {
        if (_steamTransport == null)
            return "TransportMissing";

        if (!IsSteamTransportConnectedToHost)
            return $"SteamGuestNotConnected:{SteamConnectionPhase}";

        if (!string.IsNullOrWhiteSpace(_steamTransport.LastError))
            return $"SteamSendFailed:{_steamTransport.LastError}";

        if (!string.IsNullOrWhiteSpace(_steamTransport.LastDisconnectReason))
            return $"SteamDisconnected:{_steamTransport.LastDisconnectReason}";

        return "SteamSendFailed";
    }

    private void SendViaServerRelay(ArraySegment<byte> segment)
    {
        if (NetworkManager.Instance == null || segment.Array == null || segment.Count <= 0)
            return;

        ushort protocol = PeekPacketProtocol(segment);
        var payload = Convert.ToBase64String(segment.Array, segment.Offset, segment.Count);
        if (P2PDebugConfig.TraceRealtimeTransport)
        {
            Debug.Log(
                $"[P2PTransport] SendRelay protocol={FormatPacketProtocol(protocol)} bytes={segment.Count} senderActor={SessionContext.Instance?.MyActorId ?? 0} " +
                $"{FormatBridgeContext()}");
        }
        P2PTransportDiagnostics.RecordOutgoing(
            "ServerRelay",
            FormatPacketProtocol(protocol),
            true,
            $"senderActor={SessionContext.Instance?.MyActorId ?? 0} {FormatBridgeContext()}",
            CopySegment(segment));
        NetworkManager.Instance.Send(new CS_P2PPayload
        {
            SenderActorId = SessionContext.Instance.MyActorId,
            Payload = payload
        }.Write());
    }

    private void LogManifestDiagnostics(SessionDtos.MatchManifestDto manifest)
    {
        if (!P2PDebugConfig.LogOverheadEnabled && manifest == null)
            return;

        string participants = FormatManifestParticipants(manifest);
        Debug.Log(
            $"[P2PPlayerSync] ManifestApplied match={manifest?.MatchId ?? "-"} room={manifest?.RoomId ?? "-"} " +
            $"network={manifest?.NetworkMode ?? "-"} hostUid={manifest?.HostUid ?? "-"} hostSteam={manifest?.HostSteamId64 ?? "-"} " +
            $"participants={participants}");

        if (manifest?.Participants == null)
            return;

        foreach (var uidGroup in manifest.Participants
                     .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Uid))
                     .GroupBy(x => x.Uid, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Select(x => x.ActorId).Distinct().Count() > 1))
        {
            Debug.LogWarning(
                $"[P2PPlayerSync] Manifest duplicate uid={uidGroup.Key} actors={string.Join(",", uidGroup.Select(x => x.ActorId).OrderBy(x => x))}");
        }

        foreach (var actorGroup in manifest.Participants
                     .Where(x => x != null && x.ActorId > 0)
                     .GroupBy(x => x.ActorId)
                     .Where(g => g.Select(x => x.Uid ?? "").Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
        {
            Debug.LogWarning(
                $"[P2PPlayerSync] Manifest actor mapped to multiple uids actor={actorGroup.Key} " +
                $"uids={string.Join(",", actorGroup.Select(x => x.Uid ?? "-").Distinct(StringComparer.OrdinalIgnoreCase))}");
        }
    }

    private string FormatBridgeContext()
    {
        return
            $"ctx(role={(IsHostLocal ? "Host" : "Guest")} localUid={SessionContext.Instance?.Uid ?? "-"} " +
            $"localActor={SessionContext.Instance?.MyActorId ?? 0} hostUid={HostUid} hostActor={HostActorId} " +
            $"hostSteam={HostSteamId64} localSteam={GetLocalSteamId64()} relay={RelayKey})";
    }

    private string FormatRosterActors()
    {
        var gs = ClientGameState.Instance;
        if (gs == null)
            return "gs-null";

        var entries = gs.EnumeratePlayerRoster()
            .OrderBy(x => x.ActorId)
            .Select(x => $"{x.ActorId}:{(string.IsNullOrWhiteSpace(x.Uid) ? "-" : x.Uid)}")
            .ToArray();
        return entries.Length == 0 ? "none" : string.Join(",", entries);
    }

    private static ushort PeekPacketProtocol(byte[] payloadBytes)
    {
        if (payloadBytes == null || payloadBytes.Length < 4)
            return 0;

        return BitConverter.ToUInt16(payloadBytes, 2);
    }

    private static ushort PeekPacketProtocol(ArraySegment<byte> segment)
    {
        if (segment.Array == null || segment.Count < 4)
            return 0;

        return BitConverter.ToUInt16(segment.Array, segment.Offset + 2);
    }

    private static string FormatPacketProtocol(ushort protocol)
    {
        if (protocol == 0)
            return "Unknown(0)";

        if (Enum.IsDefined(typeof(PacketID), (int)protocol))
            return $"{(PacketID)protocol}({protocol})";

        if (protocol == P2PRelayDiagnosticsPackets.HostPingRequestProtocol)
            return $"P2PHostPingRequest({protocol})";

        if (protocol == P2PRelayDiagnosticsPackets.HostPingPongProtocol)
            return $"P2PHostPingPong({protocol})";

        if (protocol == P2PRelayDiagnosticsPackets.ActionTraceProtocol)
            return $"P2PActionTrace({protocol})";

        return $"Unknown({protocol})";
    }
}

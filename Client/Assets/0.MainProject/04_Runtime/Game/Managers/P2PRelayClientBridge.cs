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
    public string HostPingStatus => _hostPingStatus;
    public bool HasRemoteHostPing => IsP2PMode && !IsHostLocal && HostActorId > 0;
    public string TransportName => IsSteamTransport ? (_steamTransport?.TransportName ?? "SteamP2P") : (IsServerRelayTransport ? "ServerRelay" : "Disabled");
    public bool IsSteamTransportRunning => _steamTransport?.IsRunning ?? false;
    public bool IsSteamTransportConnectedToHost => _steamTransport?.IsConnectedToHost ?? false;
    public int SteamConnectedPeerCount => _steamTransport?.ConnectedPeerCount ?? 0;
    public string TransportLastError => _steamTransport?.LastError ?? "";
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

            return IsSteamTransportRunning ? "Connecting to host" : "Waiting transport";
        }
    }

    [Header("P2P Host Ping")]
    [SerializeField, ReadOnly] int _hostPingIntervalMs = 2000;
    [SerializeField, ReadOnly] int _hostPingTimeoutMs = 6000;
    [SerializeField, ReadOnly] int _hostPingMaxMiss = 3;
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

    private const float HostPingEmaAlpha = 0.2f;

    private readonly Dictionary<string, string> _steamIdByUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _uidBySteamId = new(StringComparer.Ordinal);

    private CancellationTokenSource _hostPingCts;
    private long _hostPingSentCount;
    private long _hostPingRecvCount;
    private int _hostLastPongSeq;
    private P2PTransportMode _transportMode;
    private SessionDtos.MatchManifestDto _matchManifest;
    private ISteamP2PClientTransport _steamTransport;

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
        _steamTransport?.Pump();
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
        if (IsSteamTransport)
            RelayKey = !string.IsNullOrWhiteSpace(manifest?.MatchId)
                ? $"steam:{manifest.MatchId}"
                : $"steam:{HostSteamId64}";

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

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.ResetForMatchEnd();

        if (P2PContentDirector.HasInstance)
            P2PContentDirector.Instance.ResetMatchState();

        if (P2PHostLogWindow.HasInstance)
            P2PHostLogWindow.Instance.HideAndClear();
    }

    public void SyncHostState()
    {
        if (!IsP2PMode)
            return;

        if (IsSteamTransport)
            ResolveHostStateFromManifest();

        RefreshHostState();
    }

    public void HandleHostChange(SC_HostChange pkt)
    {
        int nextHostActorId = pkt?.HostActorId ?? 0;
        if (HostActorId != nextHostActorId)
        {
            StopHostPingLoop();
            ResetHostPingStats();
        }

        HostActorId = nextHostActorId;

        if (IsSteamTransport)
            ApplySteamRuntimeHostChange(nextHostActorId);

        RefreshHostState();
    }

    public void HandleGuestPayload(CS_P2PPayload pkt)
    {
        if (!IsServerRelayTransport || pkt == null)
            return;

        if (!TryDecodeBase64(pkt.Payload, out var payloadBytes))
            return;

        HandleIncomingGuestPayloadBytes(pkt.SenderActorId, payloadBytes);
    }

    public void HandleRelayBroadcast(SC_P2PBroadcast pkt)
    {
        if (!IsServerRelayTransport || pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return;

        if (!TryDecodeBase64(pkt.Payload, out var payloadBytes))
            return;

        HandleIncomingHostPayloadBytes(payloadBytes);
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

            if (IsHostLocal)
                _steamTransport?.SendHostToGuests(payloadBytes);
            else if (!(_steamTransport?.SendGuestToHost(payloadBytes) ?? false) && P2PDebugConfig.TraceHostFlow)
                Debug.LogWarning($"[P2PRelayClientBridge] Steam send to host failed host={HostSteamId64}");

            return;
        }

        var payload = Convert.ToBase64String(segment.Array, segment.Offset, segment.Count);
        NetworkManager.Instance.Send(new CS_P2PPayload
        {
            SenderActorId = SessionContext.Instance.MyActorId,
            Payload = payload
        }.Write());
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
            if (P2PHostController.HasInstance)
                P2PHostController.Instance.SetHostMode(false);

            UpdateHostLogWindow();
            return;
        }

        IsHostLocal = ResolveLocalHostOwnership();

        if (IsSteamTransport && IsHostLocal && HostActorId <= 0)
        {
            HostActorId = ClientGameState.Instance?.MyActorId ?? SessionContext.Instance?.MyActorId ?? 1;
        }

        bool hostAuthorityReady = IsHostLocal && HostActorId > 0;

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
    }

    private void UpdateHostLogWindow()
    {
        if (!IsP2PMode)
        {
            if (P2PHostLogWindow.HasInstance)
                P2PHostLogWindow.Instance.HideAndClear();

            return;
        }

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

        if (payload.IsFromHost)
        {
            HandleIncomingHostPayloadBytes(payload.PayloadBytes);
            return;
        }

        int senderActorId = ResolveActorIdForSteamId(payload.SenderSteamId64);
        HandleIncomingGuestPayloadBytes(senderActorId, payload.PayloadBytes);
    }

    private void HandleIncomingGuestPayloadBytes(int senderActorId, byte[] payloadBytes)
    {
        if (!IsP2PMode || payloadBytes == null || payloadBytes.Length == 0)
            return;

        if (TryHandleGuestDiagnostics(senderActorId, payloadBytes))
            return;

        if (senderActorId <= 0)
        {
            if (P2PDebugConfig.TraceHostFlow)
                Debug.LogWarning("[P2PRelayClientBridge] Drop guest payload because sender actor could not be resolved.");
            return;
        }

        P2PHostController.Instance.EnqueueGuestActionRequest(new CS_P2PPayload
        {
            SenderActorId = senderActorId,
            Payload = Convert.ToBase64String(payloadBytes)
        });
    }

    private void HandleIncomingHostPayloadBytes(byte[] payloadBytes)
    {
        if (payloadBytes == null || payloadBytes.Length == 0)
            return;

        if (TryHandleInboundDiagnostics(payloadBytes))
            return;

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

        if (P2PRelayDiagnosticsPackets.PeekProtocol(bytes) != P2PRelayDiagnosticsPackets.HostPingPongProtocol)
            return false;

        var pong = new P2PHostPingPongPacket();
        pong.Read(new ArraySegment<byte>(bytes));
        HandleHostPingPong(pong);
        return true;
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

        int resolvedActorId = ResolveActorIdForUid(HostUid);
        if (resolvedActorId <= 0
            && !string.IsNullOrWhiteSpace(HostUid)
            && string.Equals(HostUid, SessionContext.Instance.Uid, StringComparison.OrdinalIgnoreCase)
            && SessionContext.Instance.MyActorId > 0)
        {
            resolvedActorId = SessionContext.Instance.MyActorId;
        }

        HostActorId = resolvedActorId;
    }

    private bool ResolveLocalHostOwnership()
    {
        if (IsSteamTransport)
        {
            if (_steamTransport != null && _steamTransport.IsHosting)
                return true;

            if (!string.IsNullOrWhiteSpace(HostUid)
                && string.Equals(HostUid, SessionContext.Instance.Uid, StringComparison.OrdinalIgnoreCase))
                return true;

            string localSteamId64 = GetLocalSteamId64();
            if (!string.IsNullOrWhiteSpace(HostSteamId64)
                && !string.IsNullOrWhiteSpace(localSteamId64)
                && string.Equals(HostSteamId64, localSteamId64, StringComparison.Ordinal))
                return true;

            if (string.IsNullOrWhiteSpace(HostUid) && string.IsNullOrWhiteSpace(HostSteamId64))
                return true; // Assume host in Steam P2P if no host is specified (local testing)
        }

        return HostActorId > 0 && SessionContext.Instance.MyActorId == HostActorId;
    }

    private void SyncSteamTransport()
    {
        if (_steamTransport == null)
            return;

        var config = BuildSteamTransportConfig();
        if (config == null)
        {
            _steamTransport.Stop();
            return;
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

    private int ResolveActorIdForUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid) || ClientGameState.Instance == null)
            return 0;

        foreach (var entry in ClientGameState.Instance.EnumeratePlayerRoster())
        {
            if (string.Equals(entry.Uid, uid, StringComparison.OrdinalIgnoreCase))
                return entry.ActorId;
        }

        return 0;
    }

    private int ResolveActorIdForSteamId(string steamId64)
    {
        if (string.IsNullOrWhiteSpace(steamId64))
            return 0;

        if (_uidBySteamId.TryGetValue(steamId64, out var uid))
            return ResolveActorIdForUid(uid);

        string localSteamId64 = GetLocalSteamId64();
        if (!string.IsNullOrWhiteSpace(localSteamId64)
            && string.Equals(localSteamId64, steamId64, StringComparison.Ordinal)
            && SessionContext.Instance.MyActorId > 0)
        {
            return SessionContext.Instance.MyActorId;
        }

        return 0;
    }

    private bool TryResolveSteamHostIdentity(int hostActorId, out string hostUid, out string hostSteamId64)
    {
        hostUid = "";
        hostSteamId64 = "";

        if (hostActorId <= 0)
            return false;

        if (hostActorId == SessionContext.Instance.MyActorId
            && !string.IsNullOrWhiteSpace(SessionContext.Instance.Uid))
        {
            hostUid = SessionContext.Instance.Uid;
            hostSteamId64 = GetLocalSteamId64();
            return !string.IsNullOrWhiteSpace(hostSteamId64);
        }

        if (ClientGameState.Instance == null
            || !ClientGameState.Instance.TryGetPlayerUid(hostActorId, out hostUid)
            || string.IsNullOrWhiteSpace(hostUid))
        {
            return false;
        }

        if (!_steamIdByUid.TryGetValue(hostUid, out hostSteamId64))
            hostSteamId64 = "";

        return !string.IsNullOrWhiteSpace(hostSteamId64);
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
}

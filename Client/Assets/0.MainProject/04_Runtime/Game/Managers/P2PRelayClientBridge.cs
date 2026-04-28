using ServerCore;
using System;
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

    public bool IsRelayMode { get; private set; }
    public bool IsHostLocal { get; private set; }
    public int HostActorId { get; private set; }
    public string RelayKey { get; private set; } = "";
    public bool IsDispatchingLocal { get; private set; }
    public long HostLastRttMs => _hostLastRttMs;
    public long HostAvgRttMs => _hostAvgRttMs;
    public long HostMaxRttMs => _hostMaxRttMs;
    public long HostMinRttMs => _hostMinRttMs == long.MaxValue ? 0 : _hostMinRttMs;
    public string HostPingStatus => _hostPingStatus;
    public bool HasRemoteHostPing => IsRelayMode && !IsHostLocal && HostActorId > 0;

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
    private CancellationTokenSource _hostPingCts;
    private long _hostPingSentCount;
    private long _hostPingRecvCount;
    private int _hostLastPongSeq;

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

    public void ConfigureRelay(string ticketKey)
    {
        RelayKey = ticketKey ?? "";
        IsRelayMode = !string.IsNullOrWhiteSpace(RelayKey) &&
                      RelayKey.StartsWith("p2p:", StringComparison.OrdinalIgnoreCase);

        HostActorId = 0;
        IsHostLocal = false;

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.ResetForMatchEnd();

        UpdateHostLogWindow();

        if (!IsRelayMode)
            ResetState();
        else
            RefreshHostPingLoopState();
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
        IsRelayMode = false;
        HostActorId = 0;
        IsHostLocal = false;

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.ResetForMatchEnd();

        if (P2PContentDirector.HasInstance)
            P2PContentDirector.Instance.ResetMatchState();

        if (P2PHostLogWindow.HasInstance)
            P2PHostLogWindow.Instance.HideAndClear();
    }

    public void SyncHostState()
    {
        if (!IsRelayMode)
            return;

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
        RefreshHostState();
    }

    public void HandleGuestPayload(CS_P2PPayload pkt)
    {
        if (!IsRelayMode || pkt == null)
            return;

        if (TryHandleGuestDiagnostics(pkt))
            return;

        P2PHostController.Instance.EnqueueGuestActionRequest(pkt);
    }

    public void HandleRelayBroadcast(SC_P2PBroadcast pkt)
    {
        if (pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return;

        try
        {
            var bytes = Convert.FromBase64String(pkt.Payload);
            if (TryHandleRelayDiagnostics(bytes))
                return;

            var session = NetworkManager.Instance != null ? NetworkManager.Instance.CurrentSession : null;
            if (session == null)
                return;

            PacketManager.Instance.OnRecvPacket(session, new ArraySegment<byte>(bytes));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PRelayClientBridge] Failed to decode broadcast: {ex.Message}");
        }
    }

    public void SendWrappedPacket(IPacket packet)
    {
        if (packet == null || NetworkManager.Instance == null)
            return;

        if (!IsRelayMode)
        {
            NetworkManager.Instance.Send(packet.Write());
            return;
        }

        var payload = Encode(packet);
        if (string.IsNullOrEmpty(payload))
            return;

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
        if (session == null)
            return;

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
        if (!IsRelayMode)
        {
            HostActorId = 0;
            IsHostLocal = false;
            StopHostPingLoop();
            ResetHostPingStats();
            if (P2PHostController.HasInstance)
                P2PHostController.Instance.SetHostMode(false);

            UpdateHostLogWindow();
            return;
        }

        IsHostLocal = HostActorId > 0 && SessionContext.Instance.MyActorId == HostActorId;
        P2PHostController.Instance.SetHostActorId(HostActorId);
        P2PHostController.Instance.SetHostMode(IsHostLocal);
        if (P2PContentDirector.HasInstance)
            P2PContentDirector.Instance.OnHostLocalChanged(IsHostLocal);
        RefreshHostPingLoopState();
        UpdateHostLogWindow();
    }

    private void UpdateHostLogWindow()
    {
        if (!IsRelayMode)
        {
            if (P2PHostLogWindow.HasInstance)
                P2PHostLogWindow.Instance.HideAndClear();

            return;
        }

        P2PHostLogWindow.Instance.SetRelayContext(RelayKey, IsRelayMode, IsHostLocal, HostActorId);
    }

    private static string Encode(IPacket pkt)
    {
        var segment = pkt.Write();
        if (segment.Array == null || segment.Count <= 0)
            return "";

        return Convert.ToBase64String(segment.Array, segment.Offset, segment.Count);
    }

    private void RefreshHostPingLoopState()
    {
        if (!HasRemoteHostPing)
        {
            StopHostPingLoop();
            _hostPingStatus = IsRelayMode
                ? (IsHostLocal ? "Local Host" : "Waiting Host")
                : "Relay Off";
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
                _hostPingStatus = IsRelayMode ? "Waiting Host" : "Relay Off";
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

    private bool TryHandleGuestDiagnostics(CS_P2PPayload pkt)
    {
        if (!IsHostLocal || pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return false;

        try
        {
            var bytes = Convert.FromBase64String(pkt.Payload);
            if (P2PRelayDiagnosticsPackets.PeekProtocol(bytes) != P2PRelayDiagnosticsPackets.HostPingRequestProtocol)
                return false;

            var request = new P2PHostPingRequestPacket();
            request.Read(new ArraySegment<byte>(bytes));

            if (request.TargetHostActorId > 0 && HostActorId > 0 && request.TargetHostActorId != HostActorId)
                return true;

            long hostRecvMs = P2PRelayDiagnosticsPackets.NowMs();
            SendWrappedPacket(new P2PHostPingPongPacket
            {
                TargetActorId = pkt.SenderActorId > 0 ? pkt.SenderActorId : request.RequesterActorId,
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

    private bool TryHandleRelayDiagnostics(byte[] bytes)
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
}

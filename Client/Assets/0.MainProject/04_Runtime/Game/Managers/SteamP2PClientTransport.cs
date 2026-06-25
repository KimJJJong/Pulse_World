using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if RHYTHM_USE_FACEPUNCH_STEAMWORKS
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
#endif

public enum P2PTransportMode
{
    Disabled = 0,
    ServerRelay = 1,
    SteamP2P = 2
}

public sealed class SteamP2PTransportConfig
{
    public string MatchId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public string HostUid { get; set; } = "";
    public string HostSteamId64 { get; set; } = "";
    public string LocalSteamId64 { get; set; } = "";
    public int HostEpoch { get; set; }
    public bool IsLocalHost { get; set; }
    public IReadOnlyList<string> AllowedParticipantSteamId64s { get; set; } = Array.Empty<string>();
}

public sealed class SteamP2PIncomingPayload
{
    public string SenderSteamId64 { get; set; } = "";
    public byte[] PayloadBytes { get; set; } = Array.Empty<byte>();
    public bool IsFromHost { get; set; }
}

public struct SteamP2PConnectionStatsSnapshot
{
    public bool IsAvailable { get; set; }
    public int PingMs { get; set; }
    public float OutPacketsPerSec { get; set; }
    public float OutBytesPerSec { get; set; }
    public float InPacketsPerSec { get; set; }
    public float InBytesPerSec { get; set; }
    public float ConnectionQualityLocal { get; set; }
    public float ConnectionQualityRemote { get; set; }
    public int PendingUnreliable { get; set; }
    public int PendingReliable { get; set; }
    public int SentUnackedReliable { get; set; }
    public string TransportDetail { get; set; }
    public string DetailedStatus { get; set; }
    public string RouteHint { get; set; }
}

public interface ISteamP2PClientTransport
{
    string TransportName { get; }
    bool IsHosting { get; }
    bool IsConnectedToHost { get; }
    bool IsRunning { get; }
    int ConnectedPeerCount { get; }
    string LastError { get; }
    string ConnectionPhase { get; }
    string DetailedStatusHint { get; }
    string RouteHint { get; }
    string LastDisconnectReason { get; }
    long InitialConnectAttemptAtMs { get; }
    long LastConnectAttemptAtMs { get; }
    long LastConnectedAtMs { get; }
    long NextReconnectAtMs { get; }
    int ConnectAttemptCount { get; }
    int RetryCount { get; }
    int CurrentRetryBackoffMs { get; }
    event Action<SteamP2PIncomingPayload> OnPayloadReceived;

    void Configure(SteamP2PTransportConfig config);
    void EnsureRunning();
    void Pump();
    void Stop();
    bool SendGuestToHost(byte[] payload);
    int SendHostToGuests(byte[] payload);
    bool TryGetConnectionStats(out SteamP2PConnectionStatsSnapshot snapshot);
}

public static class SteamP2PClientTransportFactory
{
    public static ISteamP2PClientTransport Create()
    {
#if RHYTHM_USE_FACEPUNCH_STEAMWORKS
        return new FacepunchSteamP2PClientTransport();
#else
        return new NullSteamP2PClientTransport();
#endif
    }
}

public sealed class NullSteamP2PClientTransport : ISteamP2PClientTransport
{
    public string TransportName => "SteamP2P(Disabled)";
    public bool IsHosting => false;
    public bool IsConnectedToHost => false;
    public bool IsRunning => false;
    public int ConnectedPeerCount => 0;
    public string LastError { get; private set; } = "Facepunch.Steamworks plugin is unavailable.";
    public string ConnectionPhase => "Unavailable";
    public string DetailedStatusHint => "";
    public string RouteHint => "Unknown";
    public string LastDisconnectReason => "";
    public long InitialConnectAttemptAtMs => 0L;
    public long LastConnectAttemptAtMs => 0L;
    public long LastConnectedAtMs => 0L;
    public long NextReconnectAtMs => 0L;
    public int ConnectAttemptCount => 0;
    public int RetryCount => 0;
    public int CurrentRetryBackoffMs => 0;

    public event Action<SteamP2PIncomingPayload> OnPayloadReceived
    {
        add { }
        remove { }
    }

    public void Configure(SteamP2PTransportConfig config)
    {
        LastError = "Facepunch.Steamworks plugin is unavailable.";
    }

    public void EnsureRunning()
    {
    }

    public void Pump()
    {
    }

    public void Stop()
    {
    }

    public bool SendGuestToHost(byte[] payload) => false;
    public int SendHostToGuests(byte[] payload) => 0;
    public bool TryGetConnectionStats(out SteamP2PConnectionStatsSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }
}

#if RHYTHM_USE_FACEPUNCH_STEAMWORKS
internal sealed class FacepunchSteamP2PClientTransport : ISteamP2PClientTransport
{
    private const int VirtualPort = 0;
    private const int PumpBatchSize = 32;
    private static readonly int[] ReconnectBackoffMs = { 150, 300, 600, 1200, 1500 };
    // Report long handshakes, but do not force-close them here.
    // V1 was stable without app-level handshake recycling, so V2 keeps the report
    // while leaving final connection arbitration to Steam callbacks.
    private const int ConnectAttemptTimeoutMs = 5000;
    private const int CloseReasonCode = 4900;

    private SteamP2PTransportConfig _config = new();
    private string _configFingerprint = "";
    private string _lastStateSignature = "";
    private long _nextGuestReconnectAtMs;
    private long _initialConnectAttemptAtMs;
    private long _lastConnectAttemptAtMs;
    private long _lastConnectedAtMs;
    private int _connectAttemptCount;
    private int _retryCount;
    private int _currentRetryBackoffMs;
    private int _lastSlowAttemptReported;
    private string _connectionPhase = "Idle";
    private string _detailedStatusHint = "";
    private string _routeHint = "Unknown";
    private string _lastDisconnectReason = "";
    private RelaySocketHost _hostSocket;
    private RelayGuestConnection _guestConnection;

    public string TransportName => "SteamP2P";
    public bool IsHosting => _hostSocket != null;
    public bool IsConnectedToHost => _guestConnection != null && _guestConnection.Connected;
    public bool IsRunning => IsHosting || _guestConnection != null;
    public int ConnectedPeerCount => _hostSocket != null
        ? _hostSocket.Connected.Count
        : (_guestConnection != null && _guestConnection.Connected ? 1 : 0);
    public string LastError { get; private set; } = "";
    public string ConnectionPhase => _connectionPhase ?? "Idle";
    public string DetailedStatusHint => _detailedStatusHint ?? "";
    public string RouteHint => _routeHint ?? "Unknown";
    public string LastDisconnectReason => _lastDisconnectReason ?? "";
    public long InitialConnectAttemptAtMs => _initialConnectAttemptAtMs;
    public long LastConnectAttemptAtMs => _lastConnectAttemptAtMs;
    public long LastConnectedAtMs => _lastConnectedAtMs;
    public long NextReconnectAtMs => _nextGuestReconnectAtMs;
    public int ConnectAttemptCount => _connectAttemptCount;
    public int RetryCount => _retryCount;
    public int CurrentRetryBackoffMs => _currentRetryBackoffMs;

    public event Action<SteamP2PIncomingPayload> OnPayloadReceived;

    public void Configure(SteamP2PTransportConfig config)
    {
        config ??= new SteamP2PTransportConfig();

        string nextFingerprint = BuildFingerprint(config);
        bool changed = !string.Equals(_configFingerprint, nextFingerprint, StringComparison.Ordinal);
        _config = CloneConfig(config);
        _configFingerprint = nextFingerprint;

        Debug.Log(
            $"[SteamP2P] Configure match={_config.MatchId} localHost={_config.IsLocalHost} " +
            $"hostSteam={_config.HostSteamId64} localSteam={_config.LocalSteamId64} " +
            $"participants={string.Join(",", _config.AllowedParticipantSteamId64s ?? Array.Empty<string>())}");

        if (changed)
            Stop();

        LogStateIfChanged("Configure");
    }

    public void EnsureRunning()
    {
        if (!IsConfigUsable())
        {
            _connectionPhase = "ConfigUnavailable";
            Stop();
            return;
        }

        if (_config.IsLocalHost)
        {
            if (_guestConnection != null)
                CloseGuestConnection("SwitchToHost");

            if (_hostSocket == null)
                StartHostSocket();
            _connectionPhase = _hostSocket != null ? "Hosting" : "HostSocketPending";
            LogStateIfChanged("EnsureRunning");
            return;
        }

        if (_hostSocket != null)
            CloseHostSocket();

        if (_guestConnection == null && P2PRelayDiagnosticsPackets.NowMs() >= _nextGuestReconnectAtMs)
            StartGuestConnection();

        LogStateIfChanged("EnsureRunning");
    }

    public void Pump()
    {
        try
        {
            _hostSocket?.Receive(PumpBatchSize, true);
            _guestConnection?.Receive(PumpBatchSize, true);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning($"[SteamP2P] Pump failed: {ex.Message}");
        }

        long nowMs = P2PRelayDiagnosticsPackets.NowMs();
        RefreshConnectionDiagnostics();

        if (!_config.IsLocalHost)
        {
            if (_guestConnection != null
                && !_guestConnection.Connected
                && _lastConnectAttemptAtMs > 0)
            {
                long pendingMs = Math.Max(0L, nowMs - _lastConnectAttemptAtMs);
                if (_connectAttemptCount > 0
                    && _connectAttemptCount > _lastSlowAttemptReported
                    && pendingMs >= ConnectAttemptTimeoutMs)
                {
                    _lastSlowAttemptReported = _connectAttemptCount;
                    P2PTransportDiagnostics.RecordSteamAttempt(
                        "Pending",
                        _connectAttemptCount,
                        $"pending={pendingMs}ms route={_routeHint} detail={_detailedStatusHint}");
                }
            }

            if (_guestConnection == null && IsConfigUsable() && nowMs >= _nextGuestReconnectAtMs)
                StartGuestConnection();
        }

        LogStateIfChanged("Pump");
    }

    public void Stop()
    {
        CloseGuestConnection("Stop");
        CloseHostSocket();
        _nextGuestReconnectAtMs = 0;
        ResetGuestConnectTimeline();
        LogStateIfChanged("Stop");
    }

    public bool TryGetConnectionStats(out SteamP2PConnectionStatsSnapshot snapshot)
    {
        snapshot = default;

        try
        {
            if (_guestConnection != null && _guestConnection.Connected)
            {
                snapshot = BuildSnapshot(_guestConnection.Connection);
                return snapshot.IsAvailable;
            }

            if (_hostSocket != null && _hostSocket.Connected != null && _hostSocket.Connected.Count > 0)
            {
                snapshot = BuildHostAggregateSnapshot(_hostSocket.Connected);
                return snapshot.IsAvailable;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }

        return false;
    }

    public bool SendGuestToHost(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
            return false;

        if (_guestConnection == null || !_guestConnection.Connected)
            return false;

        var result = _guestConnection.Connection.SendMessage(payload, SendType.Reliable, 0);
        if (result != Result.OK)
        {
            LastError = $"Guest send failed: {result}";
            Debug.LogWarning($"[SteamP2P] {LastError}");
            return false;
        }

        return true;
    }

    public int SendHostToGuests(byte[] payload)
    {
        if (payload == null || payload.Length == 0 || _hostSocket == null)
            return 0;

        int sent = 0;
        foreach (var connection in _hostSocket.Connected.ToList())
        {
            var result = connection.SendMessage(payload, SendType.Reliable, 0);
            if (result == Result.OK)
            {
                sent++;
                continue;
            }

            LastError = $"Host broadcast failed to {connection.Id}: {result}";
            Debug.LogWarning($"[SteamP2P] {LastError}");
        }

        return sent;
    }

    internal void RaiseIncomingFromGuest(string senderSteamId64, byte[] payloadBytes)
    {
        if (payloadBytes == null || payloadBytes.Length == 0)
            return;

        OnPayloadReceived?.Invoke(new SteamP2PIncomingPayload
        {
            SenderSteamId64 = senderSteamId64 ?? "",
            PayloadBytes = payloadBytes,
            IsFromHost = false
        });
    }

    internal void RaiseIncomingFromHost(byte[] payloadBytes)
    {
        if (payloadBytes == null || payloadBytes.Length == 0)
            return;

        OnPayloadReceived?.Invoke(new SteamP2PIncomingPayload
        {
            SenderSteamId64 = _config.HostSteamId64 ?? "",
            PayloadBytes = payloadBytes,
            IsFromHost = true
        });
    }

    private void OnGuestConnectionClosed(RelayGuestConnection connection, ConnectionInfo info)
    {
        if (!ReferenceEquals(_guestConnection, connection))
            return;

        CaptureConnectionDiagnostics(connection.Connection);
        _guestConnection = null;
        ScheduleGuestReconnect(BuildDisconnectReason(info));
        P2PTransportDiagnostics.RecordSteamAttempt(
            "Disconnected",
            _connectAttemptCount,
            $"reason={BuildDisconnectReason(info)} route={_routeHint} detail={_detailedStatusHint}");
        Debug.LogWarning($"[SteamP2P] Guest disconnected from host. state={info.State} reason={info.EndReason}");
        LogStateIfChanged("GuestDisconnected");
    }

    private void OnGuestConnected(RelayGuestConnection connection, ConnectionInfo info)
    {
        if (!ReferenceEquals(_guestConnection, connection))
            return;

        _lastConnectedAtMs = P2PRelayDiagnosticsPackets.NowMs();
        _retryCount = 0;
        _currentRetryBackoffMs = 0;
        _lastDisconnectReason = "";
        _connectionPhase = "Connected";
        CaptureConnectionDiagnostics(connection.Connection);
        P2PTransportDiagnostics.RecordSteamAttempt(
            "Connected",
            _connectAttemptCount,
            $"route={_routeHint} detail={_detailedStatusHint}");
        LogStateIfChanged("GuestConnected");
    }

    private void OnGuestConnecting(RelayGuestConnection connection, ConnectionInfo info)
    {
        if (!ReferenceEquals(_guestConnection, connection))
            return;

        _connectionPhase = $"Connecting#{Math.Max(1, _connectAttemptCount)}";
        CaptureConnectionDiagnostics(connection.Connection);
        P2PTransportDiagnostics.RecordSteamAttempt(
            "Connecting",
            _connectAttemptCount,
            $"route={_routeHint} detail={_detailedStatusHint}");
        LogStateIfChanged("GuestConnecting");
    }

    private void OnHostPeerConnected(Connection connection, ConnectionInfo info)
    {
        _connectionPhase = "Hosting";
        CaptureConnectionDiagnostics(connection);
        LogStateIfChanged("PeerConnected");
    }

    private void OnHostPeerDisconnected(Connection connection, ConnectionInfo info)
    {
        CaptureConnectionDiagnostics(connection);
        Debug.LogWarning($"[SteamP2P] Peer disconnected. conn={connection.Id} state={info.State} reason={info.EndReason}");
        LogStateIfChanged("PeerDisconnected");
    }

    private bool IsConfigUsable()
    {
        if (string.IsNullOrWhiteSpace(_config.LocalSteamId64))
            return false;

        if (_config.IsLocalHost)
            return true;

        return !string.IsNullOrWhiteSpace(_config.HostSteamId64);
    }

    private void StartHostSocket()
    {
        try
        {
            var socket = SteamNetworkingSockets.CreateRelaySocket<RelaySocketHost>(VirtualPort);
            socket.Owner = this;
            socket.LocalSteamId64 = _config.LocalSteamId64 ?? "";
            socket.AllowedParticipantSteamIds = new HashSet<string>(
                (_config.AllowedParticipantSteamId64s ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.Ordinal);
            _hostSocket = socket;
            LastError = "";
            _connectionPhase = "Hosting";
            Debug.Log($"[SteamP2P] Host relay socket ready. match={_config.MatchId} host={_config.LocalSteamId64}");
            LogStateIfChanged("StartHostSocket");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _connectionPhase = "HostSocketFailed";
            Debug.LogWarning($"[SteamP2P] Failed to create host relay socket: {ex.Message}");
        }
    }

    private void StartGuestConnection()
    {
        try
        {
            if (!ulong.TryParse(_config.HostSteamId64, out ulong hostSteamId) || hostSteamId == 0)
            {
                LastError = $"Invalid host SteamID64 '{_config.HostSteamId64}'.";
                _connectionPhase = "HostSteamIdMissing";
                return;
            }

            long nowMs = P2PRelayDiagnosticsPackets.NowMs();
            if (_initialConnectAttemptAtMs <= 0)
                _initialConnectAttemptAtMs = nowMs;
            _lastConnectAttemptAtMs = nowMs;
            _connectAttemptCount++;
            _connectionPhase = $"Connecting#{_connectAttemptCount}";
            _nextGuestReconnectAtMs = 0;
            P2PTransportDiagnostics.RecordSteamAttempt(
                "Start",
                _connectAttemptCount,
                $"host={_config.HostSteamId64} local={_config.LocalSteamId64} retry={_retryCount}");

            var conn = SteamNetworkingSockets.ConnectRelay<RelayGuestConnection>((SteamId)hostSteamId, VirtualPort);
            conn.Owner = this;
            conn.ExpectedHostSteamId64 = _config.HostSteamId64 ?? "";
            conn.ConnectionName = $"RhythmRPG:{_config.MatchId}";
            _guestConnection = conn;
            LastError = "";
            Debug.Log($"[SteamP2P] Connecting to host={_config.HostSteamId64} match={_config.MatchId}");
            LogStateIfChanged("StartGuestConnection");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _guestConnection = null;
            ScheduleGuestReconnect($"ConnectStartFailed:{ex.GetType().Name}");
            P2PTransportDiagnostics.RecordSteamAttempt(
                "StartFailed",
                _connectAttemptCount,
                $"err={ex.GetType().Name}:{ex.Message}");
            Debug.LogWarning($"[SteamP2P] Failed to connect relay guest: {ex.Message}");
        }
    }

    private void CloseHostSocket()
    {
        if (_hostSocket == null)
            return;

        try
        {
            _hostSocket.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SteamP2P] Failed to close host socket: {ex.Message}");
        }
        finally
        {
            _hostSocket = null;
            LogStateIfChanged("CloseHostSocket");
        }
    }

    private void CloseGuestConnection(string reason, bool scheduleReconnect = false)
    {
        if (_guestConnection == null)
        {
            if (scheduleReconnect)
                ScheduleGuestReconnect(reason);
            return;
        }

        try
        {
            _guestConnection.Close(false, CloseReasonCode, reason ?? "Close");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SteamP2P] Failed to close guest connection: {ex.Message}");
        }
        finally
        {
            _guestConnection = null;
            if (scheduleReconnect)
                ScheduleGuestReconnect(reason);
            else if (!string.IsNullOrWhiteSpace(reason))
                _connectionPhase = $"Closed:{reason}";
            LogStateIfChanged("CloseGuestConnection");
        }
    }

    private void ScheduleGuestReconnect(string reason)
    {
        _retryCount++;
        _currentRetryBackoffMs = ResolveReconnectBackoffMs(_retryCount);
        _nextGuestReconnectAtMs = P2PRelayDiagnosticsPackets.NowMs() + _currentRetryBackoffMs;
        _lastDisconnectReason = reason ?? "";
        _connectionPhase = $"RetryPending#{_retryCount}";
    }

    private void ResetGuestConnectTimeline()
    {
        _initialConnectAttemptAtMs = 0L;
        _lastConnectAttemptAtMs = 0L;
        _lastConnectedAtMs = 0L;
        _connectAttemptCount = 0;
        _retryCount = 0;
        _currentRetryBackoffMs = 0;
        _lastSlowAttemptReported = 0;
        _connectionPhase = "Idle";
        _detailedStatusHint = "";
        _routeHint = "Unknown";
        _lastDisconnectReason = "";
    }

    private void RefreshConnectionDiagnostics()
    {
        if (_guestConnection != null)
        {
            CaptureConnectionDiagnostics(_guestConnection.Connection);
            return;
        }

        if (_hostSocket != null && _hostSocket.Connected != null && _hostSocket.Connected.Count > 0)
        {
            var firstConnection = _hostSocket.Connected.FirstOrDefault();
            if (firstConnection.Id != 0)
                CaptureConnectionDiagnostics(firstConnection);
            return;
        }

        if (_config.IsLocalHost)
        {
            _detailedStatusHint = "";
            _routeHint = "Unknown";
            return;
        }

        if (_nextGuestReconnectAtMs > P2PRelayDiagnosticsPackets.NowMs())
            _connectionPhase = $"RetryPending#{Math.Max(1, _retryCount)}";
    }

    private void CaptureConnectionDiagnostics(Connection connection)
    {
        try
        {
            string detail = connection.DetailedStatus() ?? "";
            _detailedStatusHint = CompactDetailedStatus(detail);
            _routeHint = ResolveRouteHint(detail);
        }
        catch
        {
            _detailedStatusHint = "";
            _routeHint = "Unknown";
        }
    }

    private static int ResolveReconnectBackoffMs(int retryCount)
    {
        if (retryCount <= 0)
            return 0;

        int index = Math.Min(retryCount - 1, ReconnectBackoffMs.Length - 1);
        return ReconnectBackoffMs[index];
    }

    private static string BuildDisconnectReason(ConnectionInfo info)
    {
        return $"State={info.State},Reason={info.EndReason}";
    }

    private static string CompactDetailedStatus(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return "";

        var parts = detail
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(2)
            .ToArray();
        if (parts.Length <= 0)
            return "";

        string compact = string.Join(" | ", parts);
        return compact.Length > 180 ? compact.Substring(0, 180) + "..." : compact;
    }

    private static string ResolveRouteHint(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return "Unknown";

        string normalized = detail.ToLowerInvariant();
        if (normalized.Contains("steam datagram relay")
            || normalized.Contains(" sdr")
            || normalized.Contains("relay"))
        {
            return "SDRLikely";
        }

        if (normalized.Contains("ice")
            || normalized.Contains("direct")
            || normalized.Contains("p2p"))
        {
            return "DirectLikely";
        }

        return "Unknown";
    }

    private void LogStateIfChanged(string source)
    {
        string signature =
            $"{source}|hosting={IsHosting}|connected={IsConnectedToHost}|running={IsRunning}|peers={ConnectedPeerCount}|" +
            $"localHost={_config.IsLocalHost}|host={_config.HostSteamId64}|local={_config.LocalSteamId64}|" +
            $"phase={_connectionPhase}|retry={_retryCount}|err={LastError}|detail={_detailedStatusHint}";
        if (string.Equals(signature, _lastStateSignature, StringComparison.Ordinal))
            return;

        _lastStateSignature = signature;
        Debug.Log(
            $"[SteamP2P] State source={source} hosting={IsHosting} connectedToHost={IsConnectedToHost} running={IsRunning} " +
            $"peers={ConnectedPeerCount} localHost={_config.IsLocalHost} hostSteam={_config.HostSteamId64} " +
            $"localSteam={_config.LocalSteamId64} phase={_connectionPhase} attempts={_connectAttemptCount} retries={_retryCount} " +
            $"nextReconnectAt={_nextGuestReconnectAtMs} routeHint={_routeHint} detail={_detailedStatusHint} err={LastError}");
    }

    private static SteamP2PTransportConfig CloneConfig(SteamP2PTransportConfig src)
    {
        return new SteamP2PTransportConfig
        {
            MatchId = src.MatchId ?? "",
            RoomId = src.RoomId ?? "",
            HostUid = src.HostUid ?? "",
            HostSteamId64 = src.HostSteamId64 ?? "",
            LocalSteamId64 = src.LocalSteamId64 ?? "",
            HostEpoch = src.HostEpoch,
            IsLocalHost = src.IsLocalHost,
            AllowedParticipantSteamId64s = (src.AllowedParticipantSteamId64s ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static string BuildFingerprint(SteamP2PTransportConfig config)
    {
        string participants = string.Join("|",
            (config.AllowedParticipantSteamId64s ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal));

        return string.Join("::",
            config.MatchId ?? "",
            config.HostUid ?? "",
            config.HostSteamId64 ?? "",
            config.LocalSteamId64 ?? "",
            config.HostEpoch.ToString(),
            config.IsLocalHost ? "1" : "0",
            participants);
    }

    private static SteamP2PConnectionStatsSnapshot BuildSnapshot(Connection connection)
    {
        var status = connection.QuickStatus();
        string detailedStatus = "";
        try
        {
            detailedStatus = connection.DetailedStatus() ?? "";
        }
        catch
        {
            detailedStatus = "";
        }

        return new SteamP2PConnectionStatsSnapshot
        {
            IsAvailable = true,
            PingMs = status.Ping,
            OutPacketsPerSec = status.OutPacketsPerSec,
            OutBytesPerSec = status.OutBytesPerSec,
            InPacketsPerSec = status.InPacketsPerSec,
            InBytesPerSec = status.InBytesPerSec,
            ConnectionQualityLocal = status.ConnectionQualityLocal,
            ConnectionQualityRemote = status.ConnectionQualityRemote,
            PendingUnreliable = status.PendingUnreliable,
            PendingReliable = status.PendingReliable,
            SentUnackedReliable = status.SentUnackedReliable,
            TransportDetail = CompactDetailedStatus(detailedStatus),
            DetailedStatus = detailedStatus,
            RouteHint = ResolveRouteHint(detailedStatus)
        };
    }

    private static SteamP2PConnectionStatsSnapshot BuildHostAggregateSnapshot(IEnumerable<Connection> connections)
    {
        if (connections == null)
            return default;

        int count = 0;
        int pingMs = 0;
        float outPacketsPerSec = 0f;
        float outBytesPerSec = 0f;
        float inPacketsPerSec = 0f;
        float inBytesPerSec = 0f;
        float localQuality = 1f;
        float remoteQuality = 1f;
        int pendingUnreliable = 0;
        int pendingReliable = 0;
        int sentUnackedReliable = 0;
        string detailedStatus = "";
        string routeHint = "Unknown";

        foreach (var connection in connections)
        {
            var status = connection.QuickStatus();
            count++;
            pingMs = Math.Max(pingMs, status.Ping);
            outPacketsPerSec += status.OutPacketsPerSec;
            outBytesPerSec += status.OutBytesPerSec;
            inPacketsPerSec += status.InPacketsPerSec;
            inBytesPerSec += status.InBytesPerSec;
            localQuality = Math.Min(localQuality, status.ConnectionQualityLocal);
            remoteQuality = Math.Min(remoteQuality, status.ConnectionQualityRemote);
            pendingUnreliable = Math.Max(pendingUnreliable, status.PendingUnreliable);
            pendingReliable = Math.Max(pendingReliable, status.PendingReliable);
            sentUnackedReliable = Math.Max(sentUnackedReliable, status.SentUnackedReliable);

            if (string.IsNullOrWhiteSpace(detailedStatus))
            {
                try
                {
                    detailedStatus = connection.DetailedStatus() ?? "";
                    routeHint = ResolveRouteHint(detailedStatus);
                }
                catch
                {
                    detailedStatus = "";
                    routeHint = "Unknown";
                }
            }
        }

        return new SteamP2PConnectionStatsSnapshot
        {
            IsAvailable = count > 0,
            PingMs = pingMs,
            OutPacketsPerSec = outPacketsPerSec,
            OutBytesPerSec = outBytesPerSec,
            InPacketsPerSec = inPacketsPerSec,
            InBytesPerSec = inBytesPerSec,
            ConnectionQualityLocal = localQuality,
            ConnectionQualityRemote = remoteQuality,
            PendingUnreliable = pendingUnreliable,
            PendingReliable = pendingReliable,
            SentUnackedReliable = sentUnackedReliable,
            TransportDetail = $"Host aggregate over {count} peer(s)",
            DetailedStatus = detailedStatus,
            RouteHint = routeHint
        };
    }

    private static byte[] CopyPayload(IntPtr data, int size)
    {
        if (data == IntPtr.Zero || size <= 0)
            return Array.Empty<byte>();

        var bytes = new byte[size];
        Marshal.Copy(data, bytes, 0, size);
        return bytes;
    }

    private sealed class RelaySocketHost : SocketManager
    {
        public FacepunchSteamP2PClientTransport Owner { get; set; }
        public string LocalSteamId64 { get; set; } = "";
        public HashSet<string> AllowedParticipantSteamIds { get; set; } = new(StringComparer.Ordinal);

        public override void OnConnecting(Connection connection, ConnectionInfo info)
        {
            string steamId64 = ExtractSteamId64(info.Identity);
            bool isAllowed = !string.IsNullOrWhiteSpace(steamId64)
                             && !string.Equals(steamId64, LocalSteamId64, StringComparison.Ordinal)
                             && AllowedParticipantSteamIds.Contains(steamId64);

            Debug.Log(
                $"[SteamP2P] HostOnConnecting steam={steamId64} allowed={isAllowed} conn={connection.Id} " +
                $"allowedSet={string.Join(",", AllowedParticipantSteamIds.OrderBy(x => x, StringComparer.Ordinal))}");

            if (!isAllowed)
            {
                connection.Close(false, CloseReasonCode, "Unauthorized");
                return;
            }

            connection.Accept();
        }

        public override void OnDisconnected(Connection connection, ConnectionInfo info)
        {
            Owner?.OnHostPeerDisconnected(connection, info);
        }

        public override void OnConnected(Connection connection, ConnectionInfo info)
        {
            Owner?.OnHostPeerConnected(connection, info);
        }

        public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            Owner?.RaiseIncomingFromGuest(ExtractSteamId64(identity), CopyPayload(data, size));
        }
    }

    private sealed class RelayGuestConnection : ConnectionManager
    {
        public FacepunchSteamP2PClientTransport Owner { get; set; }
        public string ExpectedHostSteamId64 { get; set; } = "";

        public override void OnConnecting(ConnectionInfo info)
        {
            Owner?.OnGuestConnecting(this, info);
        }

        public override void OnConnected(ConnectionInfo info)
        {
            Owner?.OnGuestConnected(this, info);
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
            Owner?.OnGuestConnectionClosed(this, info);
        }

        public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            Owner?.RaiseIncomingFromHost(CopyPayload(data, size));
        }
    }

    private static string ExtractSteamId64(NetIdentity identity)
    {
        return identity.IsSteamId ? identity.SteamId.ToString() : "";
    }
}
#endif

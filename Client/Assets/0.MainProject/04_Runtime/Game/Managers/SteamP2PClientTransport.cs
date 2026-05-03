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
}

public interface ISteamP2PClientTransport
{
    string TransportName { get; }
    bool IsHosting { get; }
    bool IsConnectedToHost { get; }
    bool IsRunning { get; }
    int ConnectedPeerCount { get; }
    string LastError { get; }
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
    private const int ReconnectDelayMs = 1500;
    private const int CloseReasonCode = 4900;

    private SteamP2PTransportConfig _config = new();
    private string _configFingerprint = "";
    private string _lastStateSignature = "";
    private long _nextGuestReconnectAtMs;
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
            Stop();
            return;
        }

        if (_config.IsLocalHost)
        {
            if (_guestConnection != null)
                CloseGuestConnection("SwitchToHost");

            if (_hostSocket == null)
                StartHostSocket();
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

        if (!_config.IsLocalHost && _guestConnection == null && IsConfigUsable() && P2PRelayDiagnosticsPackets.NowMs() >= _nextGuestReconnectAtMs)
            StartGuestConnection();

        LogStateIfChanged("Pump");
    }

    public void Stop()
    {
        CloseGuestConnection("Stop");
        CloseHostSocket();
        _nextGuestReconnectAtMs = 0;
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

        _guestConnection = null;
        _nextGuestReconnectAtMs = P2PRelayDiagnosticsPackets.NowMs() + ReconnectDelayMs;
        Debug.LogWarning($"[SteamP2P] Guest disconnected from host. state={info.State} reason={info.EndReason}");
        LogStateIfChanged("GuestDisconnected");
    }

    private void OnHostPeerDisconnected(Connection connection, ConnectionInfo info)
    {
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
            Debug.Log($"[SteamP2P] Host relay socket ready. match={_config.MatchId} host={_config.LocalSteamId64}");
            LogStateIfChanged("StartHostSocket");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning($"[SteamP2P] Failed to create host relay socket: {ex.Message}");
            _nextGuestReconnectAtMs = P2PRelayDiagnosticsPackets.NowMs() + ReconnectDelayMs;
        }
    }

    private void StartGuestConnection()
    {
        try
        {
            if (!ulong.TryParse(_config.HostSteamId64, out ulong hostSteamId) || hostSteamId == 0)
            {
                LastError = $"Invalid host SteamID64 '{_config.HostSteamId64}'.";
                return;
            }

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
            _nextGuestReconnectAtMs = P2PRelayDiagnosticsPackets.NowMs() + ReconnectDelayMs;
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

    private void CloseGuestConnection(string reason)
    {
        if (_guestConnection == null)
            return;

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
            LogStateIfChanged("CloseGuestConnection");
        }
    }

    private void LogStateIfChanged(string source)
    {
        string signature =
            $"{source}|hosting={IsHosting}|connected={IsConnectedToHost}|running={IsRunning}|peers={ConnectedPeerCount}|" +
            $"localHost={_config.IsLocalHost}|host={_config.HostSteamId64}|local={_config.LocalSteamId64}|err={LastError}";
        if (string.Equals(signature, _lastStateSignature, StringComparison.Ordinal))
            return;

        _lastStateSignature = signature;
        Debug.Log(
            $"[SteamP2P] State source={source} hosting={IsHosting} connectedToHost={IsConnectedToHost} running={IsRunning} " +
            $"peers={ConnectedPeerCount} localHost={_config.IsLocalHost} hostSteam={_config.HostSteamId64} " +
            $"localSteam={_config.LocalSteamId64} nextReconnectAt={_nextGuestReconnectAtMs} err={LastError}");
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
            TransportDetail = ""
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
            TransportDetail = $"Host aggregate over {count} peer(s)"
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

        public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            Owner?.RaiseIncomingFromGuest(ExtractSteamId64(identity), CopyPayload(data, size));
        }
    }

    private sealed class RelayGuestConnection : ConnectionManager
    {
        public FacepunchSteamP2PClientTransport Owner { get; set; }
        public string ExpectedHostSteamId64 { get; set; } = "";

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

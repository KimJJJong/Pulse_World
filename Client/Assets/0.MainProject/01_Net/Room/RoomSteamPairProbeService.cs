using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if RHYTHM_USE_FACEPUNCH_STEAMWORKS
using Steamworks;
using Steamworks.Data;
#endif

public sealed class RoomSteamPairProbeConfig
{
    public string RoomId { get; set; } = "";
    public string LocalSteamId64 { get; set; } = "";
    public bool LocalSteamReady { get; set; }
    public IReadOnlyList<MemberTransportState> Members { get; set; } = Array.Empty<MemberTransportState>();
}

public interface IRoomSteamPairProbeService
{
    void Configure(RoomSteamPairProbeConfig config);
    void Pump();
    void Stop();
    List<MeasuredSteamPairState> SnapshotMeasurements();
}

public static class RoomSteamPairProbeServiceFactory
{
    public static IRoomSteamPairProbeService Create()
    {
#if RHYTHM_USE_FACEPUNCH_STEAMWORKS
        return new FacepunchRoomSteamPairProbeService();
#else
        return new NullRoomSteamPairProbeService();
#endif
    }
}

public sealed class NullRoomSteamPairProbeService : IRoomSteamPairProbeService
{
    public void Configure(RoomSteamPairProbeConfig config)
    {
    }

    public void Pump()
    {
    }

    public void Stop()
    {
    }

    public List<MeasuredSteamPairState> SnapshotMeasurements()
    {
        return new List<MeasuredSteamPairState>();
    }
}

#if RHYTHM_USE_FACEPUNCH_STEAMWORKS
internal sealed class FacepunchRoomSteamPairProbeService : IRoomSteamPairProbeService
{
    private const int VirtualPort = 17;
    private const int PumpBatchSize = 8;
    private static readonly int[] ReconnectBackoffMs = { 150, 300, 600, 1200, 1500 };
    private const int ConnectAttemptTimeoutMs = 900;
    private const int CloseReasonCode = 4917;
    private const string MeasurementSource = "steam_quick_status_pair_ping";

    private RoomSteamPairProbeConfig _config = new();
    private ProbeHostSocket _hostSocket;
    private readonly Dictionary<string, ProbeGuestConnection> _guestConnections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _nextReconnectAtMsBySteamId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _retryCountBySteamId = new(StringComparer.Ordinal);
    private string _socketScopeKey = "";
    private long _suspendedUntilMs;
    private long _lastRecoverablePumpWarningAtMs;

    public void Configure(RoomSteamPairProbeConfig config)
    {
        var nextConfig = CloneConfig(config);
        string nextScopeKey = BuildSocketScopeKey(nextConfig);
        if (!string.Equals(_socketScopeKey, nextScopeKey, StringComparison.Ordinal))
        {
            Stop(logCloseWarnings: false);
            _suspendedUntilMs = 0;
            _lastRecoverablePumpWarningAtMs = 0;
            _socketScopeKey = nextScopeKey;
        }

        _config = nextConfig;
        if (!IsConfigUsable())
            Stop();
    }

    public void Pump()
    {
        if (!IsConfigUsable())
        {
            Stop();
            return;
        }

        long nowMs = NowMs();
        if (_suspendedUntilMs > nowMs)
            return;

        try
        {
            EnsureHostSocket();
            SyncHostAllowedPeers();
            SyncGuestConnections();
            _hostSocket?.Receive(PumpBatchSize, true);

            foreach (var connection in _guestConnections.Values.ToList())
                connection.Receive(PumpBatchSize, true);

            EvaluateGuestConnectTimeouts();
        }
        catch (Exception ex) when (IsRecoverableSteamSocketException(ex))
        {
            ResetAfterRecoverableSteamSocketFailure("Pump", ex);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoomSteamPairProbe] Pump failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        Stop(logCloseWarnings: true);
    }

    private void Stop(bool logCloseWarnings)
    {
        foreach (var connection in _guestConnections.Values.ToList())
            CloseGuestConnection(connection.PeerSteamId64, "Stop", logCloseWarnings: logCloseWarnings);

        _guestConnections.Clear();
        _nextReconnectAtMsBySteamId.Clear();
        _retryCountBySteamId.Clear();

        if (_hostSocket == null)
            return;

        try
        {
            _hostSocket.Close();
        }
        catch (Exception ex)
        {
            if (logCloseWarnings && !IsRecoverableSteamSocketException(ex))
                Debug.LogWarning($"[RoomSteamPairProbe] Failed to close host socket: {ex.Message}");
        }
        finally
        {
            _hostSocket = null;
        }
    }

    public List<MeasuredSteamPairState> SnapshotMeasurements()
    {
        var peers = BuildTargetPeers();
        if (peers.Count <= 0)
            return new List<MeasuredSteamPairState>();

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var results = new List<MeasuredSteamPairState>(peers.Count);

        foreach (var peer in peers.Values.OrderBy(x => x.Uid, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (TryBuildMeasurementFromGuest(peer, nowMs, out var measurement)
                    || TryBuildMeasurementFromHost(peer, nowMs, out measurement))
                {
                    results.Add(measurement);
                }
            }
            catch (Exception ex) when (IsRecoverableSteamSocketException(ex))
            {
                ResetAfterRecoverableSteamSocketFailure("Snapshot", ex);
                break;
            }
        }

        return results;
    }

    private void OnGuestDisconnected(ProbeGuestConnection connection, ConnectionInfo info)
    {
        if (connection == null || string.IsNullOrWhiteSpace(connection.PeerSteamId64))
            return;

        _guestConnections.Remove(connection.PeerSteamId64);
        ScheduleReconnect(connection.PeerSteamId64);
    }

    private void OnGuestConnected(ProbeGuestConnection connection, ConnectionInfo info)
    {
        if (connection == null || string.IsNullOrWhiteSpace(connection.PeerSteamId64))
            return;

        _retryCountBySteamId[connection.PeerSteamId64] = 0;
    }

    private static RoomSteamPairProbeConfig CloneConfig(RoomSteamPairProbeConfig config)
    {
        config ??= new RoomSteamPairProbeConfig();
        return new RoomSteamPairProbeConfig
        {
            RoomId = config.RoomId ?? "",
            LocalSteamId64 = config.LocalSteamId64 ?? "",
            LocalSteamReady = config.LocalSteamReady,
            Members = CloneMembers(config.Members)
        };
    }

    private static IReadOnlyList<MemberTransportState> CloneMembers(IReadOnlyList<MemberTransportState> members)
    {
        if (members == null || members.Count <= 0)
            return Array.Empty<MemberTransportState>();

        var clones = new List<MemberTransportState>(members.Count);
        for (int i = 0; i < members.Count; i++)
        {
            var src = members[i];
            if (src == null)
                continue;

            clones.Add(new MemberTransportState
            {
                uid = src.uid ?? "",
                name = src.name ?? "",
                steamId64 = src.steamId64 ?? "",
                clientVersion = src.clientVersion ?? "",
                hostProbeRttMs = src.hostProbeRttMs,
                hostProbeReportedAtMs = src.hostProbeReportedAtMs,
                steamEnabled = src.steamEnabled,
                steamInitialized = src.steamInitialized,
                steamLobbyJoined = src.steamLobbyJoined,
                steamReady = src.steamReady,
                currentServerRttMs = src.currentServerRttMs,
                currentServerLossPct = src.currentServerLossPct,
                currentServerJitterMs = src.currentServerJitterMs,
                avgFrameMs = src.avgFrameMs,
                p95FrameMs = src.p95FrameMs,
                sendQueueDepth = src.sendQueueDepth,
                measuredSteamPairs = src.measuredSteamPairs != null
                    ? new List<MeasuredSteamPairState>(src.measuredSteamPairs)
                    : null,
                hostSelectionReportedAtMs = src.hostSelectionReportedAtMs
            });
        }

        return clones;
    }

    private bool IsConfigUsable()
    {
        return _config.LocalSteamReady && !string.IsNullOrWhiteSpace(_config.LocalSteamId64);
    }

    private static string BuildSocketScopeKey(RoomSteamPairProbeConfig config)
    {
        if (config == null)
            return "";

        return $"{config.RoomId ?? ""}|{config.LocalSteamId64 ?? ""}|{config.LocalSteamReady}";
    }

    private void EnsureHostSocket()
    {
        if (_hostSocket != null)
            return;

        var socket = SteamNetworkingSockets.CreateRelaySocket<ProbeHostSocket>(VirtualPort);
        socket.Owner = this;
        socket.LocalSteamId64 = _config.LocalSteamId64;
        _hostSocket = socket;
    }

    private void SyncHostAllowedPeers()
    {
        if (_hostSocket == null)
            return;

        _hostSocket.AllowedPeerSteamIds = new HashSet<string>(
            BuildTargetPeers().Keys,
            StringComparer.Ordinal);
    }

    private void SyncGuestConnections()
    {
        var targets = BuildTargetPeers();
        foreach (var existing in _guestConnections.Keys.ToList())
        {
            if (targets.ContainsKey(existing))
                continue;

            CloseGuestConnection(existing, "PeerRemoved");
        }

        long nowMs = NowMs();
        foreach (var pair in targets)
        {
            if (_guestConnections.ContainsKey(pair.Key))
                continue;

            if (_nextReconnectAtMsBySteamId.TryGetValue(pair.Key, out long nextReconnectAtMs)
                && nextReconnectAtMs > nowMs)
                continue;

            StartGuestConnection(pair.Value);
        }
    }

    private Dictionary<string, ProbePeerState> BuildTargetPeers()
    {
        var result = new Dictionary<string, ProbePeerState>(StringComparer.Ordinal);
        foreach (var member in _config.Members ?? Array.Empty<MemberTransportState>())
        {
            if (member == null
                || !member.steamReady
                || string.IsNullOrWhiteSpace(member.uid)
                || string.IsNullOrWhiteSpace(member.steamId64)
                || string.Equals(member.steamId64, _config.LocalSteamId64, StringComparison.Ordinal))
            {
                continue;
            }

            result[member.steamId64] = new ProbePeerState
            {
                Uid = member.uid,
                SteamId64 = member.steamId64
            };
        }

        return result;
    }

    private void StartGuestConnection(ProbePeerState peer)
    {
        if (peer == null || string.IsNullOrWhiteSpace(peer.SteamId64))
            return;

        try
        {
            if (!ulong.TryParse(peer.SteamId64, out ulong peerSteamId) || peerSteamId == 0)
                return;

            var connection = SteamNetworkingSockets.ConnectRelay<ProbeGuestConnection>((SteamId)peerSteamId, VirtualPort);
            connection.Owner = this;
            connection.PeerUid = peer.Uid ?? "";
            connection.PeerSteamId64 = peer.SteamId64 ?? "";
            connection.ConnectionName = $"RoomProbe:{_config.RoomId}:{_config.LocalSteamId64}->{peer.SteamId64}";
            connection.ConnectAttemptAtMs = NowMs();
            _guestConnections[peer.SteamId64] = connection;
            _nextReconnectAtMsBySteamId.Remove(peer.SteamId64);
        }
        catch (Exception ex)
        {
            ScheduleReconnect(peer.SteamId64);
            Debug.LogWarning($"[RoomSteamPairProbe] Failed to connect to peer={peer.SteamId64}: {ex.Message}");
        }
    }

    private void CloseGuestConnection(string peerSteamId64, string reason, bool scheduleReconnect = false, bool logCloseWarnings = true)
    {
        if (string.IsNullOrWhiteSpace(peerSteamId64))
            return;

        if (!_guestConnections.TryGetValue(peerSteamId64, out var connection) || connection == null)
        {
            if (scheduleReconnect)
                ScheduleReconnect(peerSteamId64);
            return;
        }

        try
        {
            connection.Close(false, CloseReasonCode, reason ?? "Close");
        }
        catch (Exception ex)
        {
            if (logCloseWarnings && !IsRecoverableSteamSocketException(ex))
                Debug.LogWarning($"[RoomSteamPairProbe] Failed to close guest connection {peerSteamId64}: {ex.Message}");
        }
        finally
        {
            _guestConnections.Remove(peerSteamId64);
            if (scheduleReconnect)
                ScheduleReconnect(peerSteamId64);
        }
    }

    private bool TryBuildMeasurementFromGuest(ProbePeerState peer, long nowMs, out MeasuredSteamPairState measurement)
    {
        measurement = null;
        if (peer == null
            || string.IsNullOrWhiteSpace(peer.SteamId64)
            || !_guestConnections.TryGetValue(peer.SteamId64, out var connection)
            || connection == null
            || !connection.Connected)
        {
            return false;
        }

        return TryBuildMeasurement(peer, connection.Connection, nowMs, out measurement);
    }

    private bool TryBuildMeasurementFromHost(ProbePeerState peer, long nowMs, out MeasuredSteamPairState measurement)
    {
        measurement = null;
        if (peer == null || _hostSocket == null || string.IsNullOrWhiteSpace(peer.SteamId64))
            return false;

        if (!_hostSocket.ActiveConnectionsBySteamId.TryGetValue(peer.SteamId64, out var connection))
            return false;

        return TryBuildMeasurement(peer, connection, nowMs, out measurement);
    }

    private static bool TryBuildMeasurement(ProbePeerState peer, Connection connection, long nowMs, out MeasuredSteamPairState measurement)
    {
        measurement = null;
        var status = connection.QuickStatus();
        if (status.Ping <= 0)
            return false;

        string detail = "";
        try
        {
            detail = connection.DetailedStatus() ?? "";
        }
        catch
        {
            detail = "";
        }

        measurement = new MeasuredSteamPairState
        {
            peerUid = peer.Uid ?? "",
            peerSteamId64 = peer.SteamId64 ?? "",
            rttMs = status.Ping,
            connectionQualityLocal = status.ConnectionQualityLocal,
            connectionQualityRemote = status.ConnectionQualityRemote,
            connected = true,
            reportedAtMs = nowMs,
            source = string.IsNullOrWhiteSpace(detail)
                ? MeasurementSource
                : $"{MeasurementSource}:{ResolveRouteHint(detail)}"
        };
        return true;
    }

    private void EvaluateGuestConnectTimeouts()
    {
        long nowMs = NowMs();
        foreach (var pair in _guestConnections.ToList())
        {
            var connection = pair.Value;
            if (connection == null || connection.Connected)
                continue;

            if (connection.ConnectAttemptAtMs <= 0)
                continue;

            if (Math.Max(0L, nowMs - connection.ConnectAttemptAtMs) < ConnectAttemptTimeoutMs)
                continue;

            CloseGuestConnection(pair.Key, "ConnectAttemptTimeout", scheduleReconnect: true);
        }
    }

    private void ScheduleReconnect(string peerSteamId64)
    {
        if (string.IsNullOrWhiteSpace(peerSteamId64))
            return;

        int retryCount = 1;
        if (_retryCountBySteamId.TryGetValue(peerSteamId64, out var existingRetryCount))
            retryCount = existingRetryCount + 1;

        _retryCountBySteamId[peerSteamId64] = retryCount;
        _nextReconnectAtMsBySteamId[peerSteamId64] = NowMs() + ResolveReconnectBackoffMs(retryCount);
    }

    private static int ResolveReconnectBackoffMs(int retryCount)
    {
        if (retryCount <= 0)
            return 0;

        int index = Math.Min(retryCount - 1, ReconnectBackoffMs.Length - 1);
        return ReconnectBackoffMs[index];
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

    private static long NowMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void ResetAfterRecoverableSteamSocketFailure(string context, Exception ex)
    {
        long nowMs = NowMs();
        if (nowMs - _lastRecoverablePumpWarningAtMs >= 3000)
        {
            _lastRecoverablePumpWarningAtMs = nowMs;
            Debug.LogWarning($"[RoomSteamPairProbe] {context} detected stale Steam socket; resetting probe and retrying shortly. Error: {ex.Message}");
        }

        Stop(logCloseWarnings: false);
        _suspendedUntilMs = nowMs + 1000;
    }

    private static bool IsRecoverableSteamSocketException(Exception ex)
    {
        for (Exception current = ex; current != null; current = current.InnerException)
        {
            string message = current.Message ?? "";
            if (message.IndexOf("Invalid Socket", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("Socket is closed", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("socket has been closed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class ProbePeerState
    {
        public string Uid { get; set; } = "";
        public string SteamId64 { get; set; } = "";
    }

    private sealed class ProbeHostSocket : SocketManager
    {
        public FacepunchRoomSteamPairProbeService Owner { get; set; }
        public string LocalSteamId64 { get; set; } = "";
        public HashSet<string> AllowedPeerSteamIds { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, Connection> ActiveConnectionsBySteamId { get; } = new(StringComparer.Ordinal);

        public override void OnConnecting(Connection connection, ConnectionInfo info)
        {
            string peerSteamId64 = ExtractSteamId64(info.Identity);
            bool isAllowed = !string.IsNullOrWhiteSpace(peerSteamId64)
                             && !string.Equals(peerSteamId64, LocalSteamId64, StringComparison.Ordinal)
                             && AllowedPeerSteamIds.Contains(peerSteamId64);
            if (!isAllowed)
            {
                connection.Close(false, CloseReasonCode, "Unauthorized");
                return;
            }

            connection.Accept();
        }

        public override void OnConnected(Connection connection, ConnectionInfo info)
        {
            string peerSteamId64 = ExtractSteamId64(info.Identity);
            if (!string.IsNullOrWhiteSpace(peerSteamId64))
                ActiveConnectionsBySteamId[peerSteamId64] = connection;
        }

        public override void OnDisconnected(Connection connection, ConnectionInfo info)
        {
            string peerSteamId64 = ExtractSteamId64(info.Identity);
            if (!string.IsNullOrWhiteSpace(peerSteamId64))
                ActiveConnectionsBySteamId.Remove(peerSteamId64);
        }
    }

    private sealed class ProbeGuestConnection : ConnectionManager
    {
        public FacepunchRoomSteamPairProbeService Owner { get; set; }
        public string PeerUid { get; set; } = "";
        public string PeerSteamId64 { get; set; } = "";
        public long ConnectAttemptAtMs { get; set; }

        public override void OnConnected(ConnectionInfo info)
        {
            Owner?.OnGuestConnected(this, info);
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
            Owner?.OnGuestDisconnected(this, info);
        }
    }

    private static string ExtractSteamId64(NetIdentity identity)
    {
        return identity.IsSteamId ? identity.SteamId.ToString() : "";
    }
}
#endif

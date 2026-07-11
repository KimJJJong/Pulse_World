using GameServer.Content.Game.Entity;
using GameServer.Content.Map;
using GameServer.InGame.Director.Data;
using GameServer.InGame.Manager.Entity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Util;

public sealed class P2PRelayRoom : RoomBase
{
    private enum RoomPhase { Waiting, Running, Ended }
    private const string ServerGuardMode = "StartEndValidationOnly";
    private const int PlayerStateInitTimeoutMs = 1500;

    public string RelayId { get; }
    public string MapId { get; private set; }

    private readonly ILogger _logger;

    private RoomPhase _phase = RoomPhase.Waiting;
    private Map2D? _map;
    private StageScenario? _stage;
    private long _roomStartTimeMs;
    private long _songStartAtMs;
    private int _hostActorId;
    private bool _relayInitialized;
    private readonly Dictionary<int, (int x, int y)> _playerSpawns = new();
    private string _preferredHostUid = "";
    private List<string> _hostCandidateOrder = new();
    private readonly Dictionary<string, int> _preferredActorIdByUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _preferredSeatByUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _preferredDisplayNameByUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly P2PRelayMetrics _metrics;

    private const int RelayTickRate = 480;

    public P2PRelayRoom(string relayId, string mapId, int maxPlayers = 8, ILogger? logger = null)
        : base(maxPlayers, 1)
    {
        RelayId = relayId;
        MapId = mapId;
        _logger = logger ?? NullLogger.Instance;
        _metrics = new P2PRelayMetrics(RelayId, MapId);
    }

    protected override SessionBase? GetSession() => null;
    protected override bool IsRoomRunning() => _phase == RoomPhase.Running;
    protected override bool CheckRoomEnded() => _phase == RoomPhase.Ended;
    protected override string RoomLogKind => "GameP2P";
    protected override string RoomLogId => RelayId;

    public override void Update()
    {
        // P2P mode never runs server-authoritative gameplay simulation.
        // GameServer only brokers room lifecycle, host election, and end-of-match validation.
        PumpQueuedActions();
    }

    protected override void UpdateSessionWorldId(ClientSession s)
    {
        s.CurrentWorldId = RelayId;
    }

    protected override void MaybeEndIfEmpty()
    {
        bool empty;
        lock (_lock)
            empty = _players.Count == 0;

        if (!empty)
        {
            Enqueue(ReevaluateHost);
            return;
        }

        bool shouldRecordFinalMetrics = false;
        lock (_lock)
        {
            if (_phase != RoomPhase.Ended)
            {
                _phase = RoomPhase.Ended;
                shouldRecordFinalMetrics = true;
            }
        }

        if (shouldRecordFinalMetrics)
            P2PRelayManager.RecordCompletedMetrics(GetMetricsSnapshot());

        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_end reason=empty roomType=GameP2P world={RelayId} map={MapId}");
        _logger.LogInformation("[P2PRelayRoom] Destroyed (empty) relayId={RelayId}", RelayId);
        P2PRelayManager.Remove(RelayId);
    }

    public IEnumerable<(string uid, int actorId, bool connected)> GetPlayersSnapshot()
    {
        lock (_lock)
        {
            foreach (var p in _players.Values)
                yield return (p.Uid, p.ActorId, p.Conn != null && p.Conn.IsConnected);
        }
    }

    public P2PRelayMetricsSnapshot GetMetricsSnapshot() => _metrics.Snapshot();

    public void UpdateHostPreferences(string preferredHostUid, IEnumerable<GameServer.Infrastructure.Api.Dto.GameMatchParticipantResponse>? participants)
    {
        var normalizedParticipants = NormalizeManifestParticipants(preferredHostUid, participants);
        var normalizedOrder = normalizedParticipants.Select(x => x.Uid).ToList();
        var preferredActorIds = normalizedParticipants
            .Where(x => x.ActorId > 0)
            .GroupBy(x => x.Uid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().ActorId, StringComparer.OrdinalIgnoreCase);
        var preferredSeats = normalizedParticipants
            .Where(x => x.ActorId > 0)
            .GroupBy(x => x.Uid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => Math.Max(0, x.First().ActorId - 1), StringComparer.OrdinalIgnoreCase);
        var preferredDisplayNames = normalizedParticipants
            .GroupBy(x => x.Uid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => NormalizePlayerDisplayName(x.First().DisplayName, x.Key), StringComparer.OrdinalIgnoreCase);
        bool changed;

        lock (_lock)
        {
            changed =
                !string.Equals(_preferredHostUid, preferredHostUid ?? "", StringComparison.OrdinalIgnoreCase)
                || !_hostCandidateOrder.SequenceEqual(normalizedOrder, StringComparer.OrdinalIgnoreCase)
                || !DictionaryEqual(_preferredActorIdByUid, preferredActorIds)
                || !DictionaryEqual(_preferredSeatByUid, preferredSeats)
                || !DictionaryEqual(_preferredDisplayNameByUid, preferredDisplayNames);

            _preferredHostUid = preferredHostUid ?? "";
            _hostCandidateOrder = normalizedOrder;
            _preferredActorIdByUid.Clear();
            _preferredSeatByUid.Clear();
            _preferredDisplayNameByUid.Clear();
            foreach (var pair in preferredActorIds)
                _preferredActorIdByUid[pair.Key] = pair.Value;
            foreach (var pair in preferredSeats)
                _preferredSeatByUid[pair.Key] = pair.Value;
            foreach (var pair in preferredDisplayNames)
                _preferredDisplayNameByUid[pair.Key] = pair.Value;
        }

        if (!changed)
            return;

        _logger.LogInformation(
            "[P2PRelayRoom] Host preference updated relayId={RelayId} preferredHost={PreferredHost} order={Order}",
            RelayId,
            _preferredHostUid,
            string.Join(",", normalizedOrder));
        Enqueue(ReevaluateHost);
    }

    public override bool BindOrReattach(ClientSession s, out int actorId)
    {
        actorId = -1;
        if (s == null)
        {
            LogRoomBindFail(null, "null_session");
            return false;
        }

        if (!s.HasAuth || string.IsNullOrEmpty(s.Uid))
        {
            LogRoomBindFail(s, "not_authenticated");
            return false;
        }

        bool isNew = false;
        int seat = -1;
        int playerCount = -1;
        RoomPlayer? p = null;

        lock (_lock)
        {
            if (CheckRoomEnded())
            {
                LogRoomBindFail(s, "room_ended", _players.Count);
                return false;
            }

            var key = (s.Uid, s.Epoch);
            if (_players.TryGetValue(key, out var existing))
            {
                p = existing;
                actorId = p.ActorId;
                seat = p.SeatIndex;

                p.Attach(s);
                _byActor[actorId] = s;

                s.ActorId = actorId;
                s.SeatIndex = seat;
                UpdateSessionWorldId(s);

                _broadcastDirty = true;
                isNew = false;
                playerCount = _players.Count;
            }
            else
            {
                if (_players.Count >= _maxPlayers)
                {
                    LogRoomBindFail(s, "room_full", _players.Count);
                    return false;
                }

                if (_freeSeats.Count == 0 && _players.Count < _maxPlayers)
                {
                    LogManager.Instance.LogError("P2PRelayRoom",
                        $"FreeSeats empty but room not full. players={_players.Count} max={_maxPlayers}");
                    LogRoomBindFail(s, "seat_pool_corrupt", _players.Count);
                    return false;
                }

                if (_freeSeats.Count == 0)
                {
                    LogRoomBindFail(s, "no_free_seat", _players.Count);
                    return false;
                }

                actorId = ResolvePreferredActorIdUnsafe(s.Uid);
                if (actorId <= 0 || _byActor.ContainsKey(actorId))
                    actorId = ReserveNextActorIdUnsafe();
                else
                    _nextActorId = Math.Max(_nextActorId, actorId + 1);

                int preferredSeat = ResolvePreferredSeatIndexUnsafe(s.Uid);
                if (!TryReserveSeatUnsafe(preferredSeat, out seat))
                {
                    LogRoomBindFail(s, "preferred_seat_unavailable", _players.Count);
                    return false;
                }

                p = new RoomPlayer(s.Uid, s.Epoch, actorId, seat);
                p.Attach(s);

                _players[key] = p;
                _byActor[actorId] = s;

                s.ActorId = actorId;
                s.SeatIndex = seat;
                UpdateSessionWorldId(s);

                _broadcastDirty = true;
                isNew = true;
                playerCount = _players.Count;
            }
        }

        LogRoomBind(s, actorId, seat, isNew, playerCount);
        OnPlayerBound(p, isNew);
        return true;
    }

    private (int x, int y) GetOrAssignSpawn(RoomPlayer player)
    {
        lock (_lock)
        {
            if (_playerSpawns.TryGetValue(player.ActorId, out var spawn))
                return spawn;

            var next = _map != null ? _map.GetSpawnPointRandom() : (0, 0);
            if (next.Item1 < 0 || next.Item2 < 0)
                next = (0, 0);

            _playerSpawns[player.ActorId] = next;
            return next;
        }
    }

    protected override void OnPlayerBound(RoomPlayer p, bool isNew)
    {
        Enqueue(() =>
        {
            _logger.LogInformation(
                "[P2PRelayRoom] Player bound relayId={RelayId} uid={Uid} epoch={Epoch} actor={ActorId} seat={SeatIndex} isNew={IsNew} connId={ConnId} preferredHost={PreferredHost} order={Order}",
                RelayId,
                p.Uid,
                p.Epoch,
                p.ActorId,
                p.SeatIndex,
                isNew,
                p.Conn?.ConnId ?? "-",
                _preferredHostUid,
                string.Join(",", _hostCandidateOrder));

            EnsureStarted();
            ReevaluateHost();

            if (p.Conn != null)
                SendInitPacketToPlayer(p.Conn, isNew);
        });
    }

    public override void DetachIfMatch(string uid, long epoch, string connId)
    {
        base.DetachIfMatch(uid, epoch, connId);
        Enqueue(ReevaluateHost);
    }

    public override void RemovePlayer(string uid, long epoch)
    {
        base.RemovePlayer(uid, epoch);
        Enqueue(ReevaluateHost);
    }

    private void EnsureStarted()
    {
        bool shouldInit = false;
        lock (_lock)
        {
            if (_relayInitialized || _phase == RoomPhase.Ended)
                return;

            _relayInitialized = true;
            _phase = RoomPhase.Running;
            shouldInit = true;
        }

        if (!shouldInit)
            return;

        if (!MapDatabase.TryGet(MapId, out var map) || map == null)
        {
            map = new Map2D(1, 1) { MapId = MapId };
            map.Set(0, 0, TileKind.Floor);
            map.SetSpawnPoint(0, 0);
            _logger.LogWarning("[P2PRelayRoom] Map missing. Fallback 1x1 map created. mapId={MapId}", MapId);
        }
        _map = map;

        _stage = StageDataManager.Get(MapId) ?? new StageScenario
        {
            MapId = MapId,
            Description = "P2P relay fallback stage",
            RhythmSettings = new RhythmSettingsData
            {
                Bpm = 120,
                BaseBeatDivision = 1,
                ActionWindowMs = 100,
                StartDelayMs = 2000
            }
        };

        _roomStartTimeMs = AppRef.ServerTimeMs();
        _songStartAtMs = _roomStartTimeMs + Math.Max(1000, _stage.RhythmSettings?.StartDelayMs ?? 2000);

        EntityDataManager.Instance.Load();

        _logger.LogInformation(
            "[P2PRelayRoom] Started relayId={RelayId} mapId={MapId} songStart={SongStart} host={Host} serverRole={ServerRole}",
            RelayId,
            MapId,
            _songStartAtMs,
            _hostActorId,
            ServerGuardMode);
    }

    private void ReevaluateHost()
    {
        int nextHost = 0;
        string selectionReason = "retain";
        lock (_lock)
        {
            if (_phase == RoomPhase.Ended)
                return;

            int preferredConnectedHost = GetConnectedActorIdByUidUnsafe(_preferredHostUid);
            bool currentConnected = _hostActorId > 0 &&
                _byActor.TryGetValue(_hostActorId, out var cur) &&
                cur != null &&
                cur.IsConnected;

            if (preferredConnectedHost > 0)
            {
                nextHost = preferredConnectedHost;
                selectionReason = currentConnected && _hostActorId != preferredConnectedHost
                    ? "promote_preferred"
                    : "preferred";
            }
            else if (currentConnected)
            {
                nextHost = _hostActorId;
            }
            else
            {
                if (_hostActorId <= 0 && !string.IsNullOrWhiteSpace(_preferredHostUid))
                {
                    nextHost = 0;
                    selectionReason = "await_preferred_initial";
                }
                else
                {
                    nextHost = TrySelectFailoverConnectedHostUnsafe();
                    selectionReason = nextHost > 0 ? "failover" : "no_connected_candidate";
                }
            }

            if (nextHost == _hostActorId)
                return;

            _hostActorId = nextHost;
        }

        Broadcast(new SC_HostChange { HostActorId = _hostActorId });
        _logger.LogInformation(
            "[P2PRelayRoom] Host changed relayId={RelayId} host={Host} preferredHost={PreferredHost} reason={Reason} players={Players}",
            RelayId,
            _hostActorId,
            _preferredHostUid,
            selectionReason,
            string.Join(",", GetPlayersSnapshot().Select(x => $"{x.actorId}:{x.uid}:{(x.connected ? "on" : "off")}")));
    }

    private int TrySelectPreferredConnectedHostUnsafe()
    {
        if (_hostCandidateOrder.Count == 0)
            return 0;

        foreach (var candidateUid in _hostCandidateOrder)
        {
            if (string.IsNullOrWhiteSpace(candidateUid))
                continue;

            var match = _players.Values.FirstOrDefault(p =>
                p.Conn != null &&
                p.Conn.IsConnected &&
                string.Equals(p.Uid, candidateUid, StringComparison.OrdinalIgnoreCase));

            if (match != null)
                return match.ActorId;
        }

        return 0;
    }

    private int TrySelectFailoverConnectedHostUnsafe()
    {
        if (_hostCandidateOrder.Count > 0)
        {
            foreach (var candidateUid in _hostCandidateOrder)
            {
                if (string.IsNullOrWhiteSpace(candidateUid)
                    || string.Equals(candidateUid, _preferredHostUid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int actorId = GetConnectedActorIdByUidUnsafe(candidateUid);
                if (actorId > 0)
                    return actorId;
            }
        }

        return TrySelectFallbackConnectedHostUnsafe();
    }

    private int TrySelectFallbackConnectedHostUnsafe()
    {
        foreach (var p in _players.Values.OrderBy(p => p.SeatIndex).ThenBy(p => p.ActorId))
        {
            if (p.Conn != null && p.Conn.IsConnected)
                return p.ActorId;
        }

        return 0;
    }

    private static List<string> NormalizeHostCandidateOrder(string preferredHostUid, IEnumerable<string>? hostCandidateOrder)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(preferredHostUid) && seen.Add(preferredHostUid))
            normalized.Add(preferredHostUid);

        if (hostCandidateOrder == null)
            return normalized;

        foreach (var uid in hostCandidateOrder)
        {
            if (string.IsNullOrWhiteSpace(uid) || !seen.Add(uid))
                continue;

            normalized.Add(uid);
        }

        return normalized;
    }

    private static List<GameServer.Infrastructure.Api.Dto.GameMatchParticipantResponse> NormalizeManifestParticipants(
        string preferredHostUid,
        IEnumerable<GameServer.Infrastructure.Api.Dto.GameMatchParticipantResponse>? participants)
    {
        var normalized = new List<GameServer.Infrastructure.Api.Dto.GameMatchParticipantResponse>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (participants != null)
        {
            foreach (var participant in participants)
            {
                if (participant == null || string.IsNullOrWhiteSpace(participant.Uid) || !seen.Add(participant.Uid))
                    continue;

                normalized.Add(participant);
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredHostUid) && seen.Add(preferredHostUid))
        {
            normalized.Insert(0, new GameServer.Infrastructure.Api.Dto.GameMatchParticipantResponse
            {
                Uid = preferredHostUid
            });
        }

        return normalized;
    }

    private static bool DictionaryEqual(
        Dictionary<string, int> left,
        Dictionary<string, int> right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null || right == null || left.Count != right.Count)
            return false;

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue) || rightValue != pair.Value)
                return false;
        }

        return true;
    }

    private static bool DictionaryEqual(
        Dictionary<string, string> left,
        Dictionary<string, string> right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null || right == null || left.Count != right.Count)
            return false;

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue)
                || !string.Equals(pair.Value ?? "", rightValue ?? "", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private string ResolvePlayerDisplayNameUnsafe(string uid)
    {
        string displayName = "";
        if (!string.IsNullOrWhiteSpace(uid) && _preferredDisplayNameByUid.TryGetValue(uid, out var preferredName))
            displayName = NormalizePlayerDisplayName(preferredName, uid);

        return displayName;
    }

    private static string NormalizePlayerDisplayName(string displayName, string uid)
    {
        string clean = (displayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return "";

        if (!string.IsNullOrWhiteSpace(uid) && string.Equals(clean, uid, StringComparison.OrdinalIgnoreCase))
            return "";

        if (string.Equals(clean, "Guest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(clean, "Unknown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(clean, "NullName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(clean, "-", StringComparison.Ordinal))
        {
            return "";
        }

        return clean;
    }

    private int ResolvePreferredActorIdUnsafe(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return 0;

        return _preferredActorIdByUid.TryGetValue(uid, out var actorId)
            ? actorId
            : 0;
    }

    private int ResolvePreferredSeatIndexUnsafe(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return -1;

        return _preferredSeatByUid.TryGetValue(uid, out var seat)
            ? seat
            : -1;
    }

    private int ReserveNextActorIdUnsafe()
    {
        while (_byActor.ContainsKey(_nextActorId))
            _nextActorId++;

        return _nextActorId++;
    }

    private bool TryReserveSeatUnsafe(int preferredSeat, out int seat)
    {
        if (preferredSeat >= 0 && TryRemoveSeatUnsafe(preferredSeat))
        {
            seat = preferredSeat;
            return true;
        }

        if (_freeSeats.Count <= 0)
        {
            seat = -1;
            return false;
        }

        seat = _freeSeats.Dequeue();
        return true;
    }

    private bool TryRemoveSeatUnsafe(int seatIndex)
    {
        if (seatIndex < 0 || _freeSeats.Count <= 0)
            return false;

        bool removed = false;
        int count = _freeSeats.Count;
        for (int i = 0; i < count; i++)
        {
            int seat = _freeSeats.Dequeue();
            if (!removed && seat == seatIndex)
            {
                removed = true;
                continue;
            }

            _freeSeats.Enqueue(seat);
        }

        return removed;
    }

    private int GetConnectedActorIdByUidUnsafe(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return 0;

        var match = _players.Values.FirstOrDefault(p =>
            p.Conn != null &&
            p.Conn.IsConnected &&
            string.Equals(p.Uid, uid, StringComparison.OrdinalIgnoreCase));

        return match?.ActorId ?? 0;
    }

    public void SendInitPacketToPlayer(ClientSession s)
        => SendInitPacketToPlayer(s, isNewJoin: false);

    private void SendInitPacketToPlayer(ClientSession s, bool isNewJoin)
    {
        if (s == null)
        {
            _logger.LogWarning("[P2PRelayRoom] Init send skipped relayId={RelayId} reason=null_session", RelayId);
            LogManager.Instance.LogWarning("P2PRelayRoom", $"Init send skipped relayId={RelayId} reason=null_session");
            return;
        }

        if (!s.IsConnected)
        {
            _logger.LogWarning(
                "[P2PRelayRoom] Init send skipped relayId={RelayId} uid={Uid} actor={ActorId} reason=disconnected connId={ConnId}",
                RelayId,
                s.Uid,
                s.ActorId,
                s.ConnId);
            LogManager.Instance.LogWarning(
                "P2PRelayRoom",
                $"Init send skipped relayId={RelayId} uid={s.Uid} actor={s.ActorId} reason=disconnected connId={s.ConnId}");
            return;
        }

        int myActorId = s.ActorId;
        if (myActorId <= 0)
        {
            _logger.LogWarning(
                "[P2PRelayRoom] Init send skipped relayId={RelayId} uid={Uid} actor={ActorId} reason=invalid_actor connId={ConnId}",
                RelayId,
                s.Uid,
                myActorId,
                s.ConnId);
            LogManager.Instance.LogWarning(
                "P2PRelayRoom",
                $"Init send skipped relayId={RelayId} uid={s.Uid} actor={myActorId} reason=invalid_actor connId={s.ConnId}");
            return;
        }

        if (!_relayInitialized)
            EnsureStarted();

        _logger.LogInformation(
            "[P2PRelayRoom] Init send scheduled relayId={RelayId} uid={Uid} actor={ActorId} isNewJoin={IsNewJoin} connId={ConnId}",
            RelayId,
            s.Uid,
            myActorId,
            isNewJoin,
            s.ConnId);
        LogManager.Instance.LogInfo(
            "P2PRelayRoom",
            $"Init send scheduled relayId={RelayId} uid={s.Uid} actor={myActorId} isNewJoin={isNewJoin} connId={s.ConnId}");

        Task.Run(async () =>
        {
            try
            {
                List<RoomPlayer> playersSnapshot;

                lock (_lock)
                {
                    playersSnapshot = _players.Values.ToList();
                }

                var statesTask = LoadPlayerStatesAsync(playersSnapshot);
                var stateLoad = await WaitForPlayerStatesForInitAsync(statesTask, PlayerStateInitTimeoutMs, playersSnapshot.Count);
                var states = stateLoad.States;

                var init = BuildInitPacketForPlayer(myActorId, states);
                _logger.LogInformation(
                    "[P2PRelayRoom] Init payload relayId={RelayId} actor={ActorId} players={PlayerCount} entities={EntityCount} monsters={MonsterCount} objects={ObjectCount} roster={Roster} playerEntities={PlayerEntities}",
                    RelayId,
                    myActorId,
                    init.playerss.Count,
                    init.entitiess.Count,
                    init.entitiess.Count(x => x.EntityType == (int)EntityType.Monster),
                    init.entitiess.Count(x => x.EntityType == (int)EntityType.Object),
                    string.Join(",", init.playerss.Select(x => $"{x.ActorId}:{x.Uid}")),
                    string.Join(",", init.entitiess.Where(x => x.EntityType == (int)EntityType.Player).Select(x => $"{x.EntityId}@({x.X},{x.Y})")));

                if (!s.IsConnected)
                    return;

                s.Send(init.Write());
                _logger.LogInformation(
                    "[P2PRelayRoom] InitMap packet queued relayId={RelayId} actor={ActorId} entities={EntityCount}",
                    RelayId,
                    myActorId,
                    init.entitiess.Count);
                LogManager.Instance.LogInfo(
                    "P2PRelayRoom",
                    $"InitMap packet queued relayId={RelayId} actor={myActorId} entities={init.entitiess.Count}");
                SendSkillSlotsIfAvailable(s, states);

                if (stateLoad.TimedOut)
                    _ = SendLateSkillSlotsWhenReadyAsync(s, statesTask);

                s.Send(new SC_GameBegin
                {
                    matchId = RelayId,
                    startAtMs = _songStartAtMs,
                    startTick = 0
                }.Write());

                s.Send(new SC_BeatSync
                {
                    ServerSendTimeMs = AppRef.ServerTimeMs(),
                    ClientSendTimeMs = 0,
                    SongStartServerTimeMs = _songStartAtMs,
                    Bpm = _stage?.RhythmSettings?.Bpm ?? 120,
                    BaseBeatDivision = _stage?.RhythmSettings?.BaseBeatDivision ?? 1,
                    BeatIndex = 0
                }.Write());

                s.Send(new SC_HostChange
                {
                    HostActorId = _hostActorId
                }.Write());

                _logger.LogInformation(
                    "[P2PRelayRoom] Init sent relayId={RelayId} actor={ActorId} host={Host}",
                    RelayId,
                    myActorId,
                    _hostActorId);

                if (!isNewJoin)
                    return;

                BroadcastCurrentPlayerSpawns(states);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[P2PRelayRoom] Init send failed relayId={RelayId} actor={ActorId}", RelayId, myActorId);
                LogManager.Instance.LogError("P2PRelayRoom", $"Init send failed relayId={RelayId} actor={myActorId} err={ex}");
            }
        });
    }

    private async Task<Dictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?>> LoadPlayerStatesAsync(
        IReadOnlyCollection<RoomPlayer> playersSnapshot)
    {
        var states = new Dictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?>(StringComparer.OrdinalIgnoreCase);

        foreach (var player in playersSnapshot)
        {
            try
            {
                states[player.Uid] = await ServerServices.ApiClient.GetPlayerStateAsync(player.Uid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[P2PRelayRoom] PlayerState load failed uid={Uid}", player.Uid);
                states[player.Uid] = null;
            }
        }

        return states;
    }

    private async Task<(Dictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?> States, bool TimedOut)>
        WaitForPlayerStatesForInitAsync(
            Task<Dictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?>> statesTask,
            int timeoutMs,
            int playerCount)
    {
        var completed = await Task.WhenAny(statesTask, Task.Delay(Math.Max(1, timeoutMs)));
        if (completed == statesTask)
            return (await statesTask, false);

        _logger.LogWarning(
            "[P2PRelayRoom] PlayerState init load timed out relayId={RelayId} players={PlayerCount} timeoutMs={TimeoutMs}. Sending InitMap with defaults.",
            RelayId,
            playerCount,
            timeoutMs);
        LogManager.Instance.LogWarning(
            "P2PRelayRoom",
            $"PlayerState init load timed out relayId={RelayId} players={playerCount} timeoutMs={timeoutMs}. Sending InitMap with defaults.");

        return (new Dictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?>(StringComparer.OrdinalIgnoreCase), true);
    }

    private void SendSkillSlotsIfAvailable(
        ClientSession s,
        IReadOnlyDictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?> states)
    {
        if (s == null || !s.IsConnected)
            return;

        if (!states.TryGetValue(s.Uid, out var myState) || myState == null)
            return;

        var updateSkillsPkt = new SC_UpdateSkillSlots
        {
            NormalAttackSkillId = myState.NormalAttackSkillId ?? ""
        };

        if (myState.ActiveSkillSlots != null)
        {
            foreach (var skill in myState.ActiveSkillSlots)
            {
                updateSkillsPkt.activeSkillSlotss.Add(new SC_UpdateSkillSlots.ActiveSkillSlots
                {
                    SkillId = skill ?? ""
                });
            }
        }

        s.Send(updateSkillsPkt.Write());
    }

    private async Task SendLateSkillSlotsWhenReadyAsync(
        ClientSession s,
        Task<Dictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?>> statesTask)
    {
        try
        {
            var states = await statesTask;
            SendSkillSlotsIfAvailable(s, states);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[P2PRelayRoom] Late skill slot send failed relayId={RelayId} uid={Uid}", RelayId, s?.Uid ?? "");
        }
    }

    private void BroadcastCurrentPlayerSpawns(
        IReadOnlyDictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?> states)
    {
        List<RoomPlayer> playersSnapshot;
        lock (_lock)
        {
            playersSnapshot = _players.Values
                .Where(x => x.Conn != null && x.Conn.IsConnected)
                .OrderBy(x => x.SeatIndex)
                .ThenBy(x => x.ActorId)
                .ToList();
        }

        if (playersSnapshot.Count == 0)
            return;

        foreach (var player in playersSnapshot)
        {
            states.TryGetValue(player.Uid, out var playerState);
            var spawn = GetOrAssignSpawn(player);

            _logger.LogInformation(
                "[P2PRelayRoom] Snapshot spawn relayId={RelayId} actor={ActorId} uid={Uid} pos=({X},{Y}) hp={Hp} appearance={Appearance}",
                RelayId,
                player.ActorId,
                player.Uid,
                spawn.x,
                spawn.y,
                playerState?.TotalHp > 0 ? playerState.TotalHp : 1000,
                playerState?.AppearanceId ?? 0);

            Broadcast(new SC_EntitySpawn
            {
                BeatIndex = 0,
                EntityId = player.ActorId,
                EntityType = (int)EntityType.Player,
                AppearanceId = playerState?.AppearanceId ?? 0,
                X = spawn.x,
                Y = spawn.y,
                Hp = playerState?.TotalHp > 0 ? playerState.TotalHp : 1000
            });
        }

        _logger.LogInformation(
            "[P2PRelayRoom] Player snapshot broadcast relayId={RelayId} count={Count}",
            RelayId,
            playersSnapshot.Count);
    }

    private SC_InitMap BuildInitPacketForPlayer(
        int myActorId,
        IReadOnlyDictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?> states)
    {
        var stageEntityIds = new EntityIdGenerator();
        var usedEntityIds = new HashSet<int>();

        var packet = new SC_InitMap
        {
            ServerTimeMs = AppRef.ServerTimeMs(),
            Revision = 0,
            TickRate = RelayTickRate,
            MapId = _map?.MapId ?? MapId,
            MapWidth = _map?.Width ?? 1,
            MapHeight = _map?.Height ?? 1,
            MapVersion = 0,
            Mode = 2,
            MyActorId = myActorId,
            ActionWindowMs = _stage?.RhythmSettings?.ActionWindowMs ?? 100,
            SongId = _stage?.RhythmSettings?.SongKey ?? "DefaultSong",
            Bpm = _stage?.RhythmSettings?.Bpm ?? 120,
            BaseBeatDivision = _stage?.RhythmSettings?.BaseBeatDivision ?? 1,
            SongStartServerTime = _songStartAtMs
        };

        lock (_lock)
        {
            int guestIndex = 1;
            foreach (var p in _players.Values.OrderBy(p => p.SeatIndex).ThenBy(p => p.ActorId))
            {
                states.TryGetValue(p.Uid, out var pState);
                int hp = pState?.TotalHp > 0 ? pState.TotalHp : 1000;
                int atk = pState?.TotalAtk ?? 0;
                int def = pState?.TotalDef ?? 0;
                int appearanceId = pState?.AppearanceId ?? 0;
                string displayName = ResolvePlayerDisplayNameUnsafe(p.Uid);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = $"Guest_{guestIndex++:00}";

                var spawn = GetOrAssignSpawn(p);
                packet.playerss.Add(new SC_InitMap.Players
                {
                    Uid = p.Uid,
                    ActorId = p.ActorId,
                    Name = displayName
                });

                packet.entitiess.Add(new SC_InitMap.Entities
                {
                    EntityId = p.ActorId,
                    EntityType = (int)EntityType.Player,
                    OwnerSlot = p.SeatIndex,
                    X = spawn.Item1,
                    Y = spawn.Item2,
                    Dir = 0,
                    Hp = hp,
                    AppearanceId = appearanceId
                });
                usedEntityIds.Add(p.ActorId);
            }
        }

        if (_stage != null)
        {
            foreach (var spawn in _stage.InitialSpawns ?? new List<SpawnData>())
            {
                var entityData = EntityDataManager.Instance.Get(spawn.MonsterId);
                packet.entitiess.Add(new SC_InitMap.Entities
                {
                    EntityId = AllocateRelayStageEntityId(stageEntityIds, usedEntityIds, EntityType.Monster),
                    EntityType = (int)EntityType.Monster,
                    OwnerSlot = spawn.GroupId,
                    X = spawn.X,
                    Y = ResolveMapY(spawn.Y, spawn.Z),
                    Dir = 0,
                    Hp = entityData?.MaxHp > 0 ? entityData.MaxHp : 50,
                    AppearanceId = spawn.MonsterId,
                    Rotation = spawn.Rotation
                });
            }

            foreach (var obj in _stage.InitialObjects ?? new List<SpawnObjectData>())
            {
                var entityType = ResolveRelayStageObjectType(obj.EntityType);
                var entityData = EntityDataManager.Instance.Get(obj.EntityId);
                packet.entitiess.Add(new SC_InitMap.Entities
                {
                    EntityId = AllocateRelayStageEntityId(stageEntityIds, usedEntityIds, entityType),
                    EntityType = (int)entityType,
                    OwnerSlot = obj.GroupId,
                    X = obj.X,
                    Y = ResolveMapY(obj.Y, obj.Z),
                    Dir = 0,
                    Hp = entityData?.MaxHp > 0 ? entityData.MaxHp : 10,
                    AppearanceId = obj.EntityId,
                    Rotation = obj.Rotation
                });
            }
        }

        return packet;
    }

    private static int AllocateRelayStageEntityId(EntityIdGenerator generator, HashSet<int> usedEntityIds, EntityType type)
    {
        while (true)
        {
            int entityId = generator.Generate(type);
            if (usedEntityIds.Add(entityId))
                return entityId;
        }
    }

    private static EntityType ResolveRelayStageObjectType(int entityType)
    {
        if (entityType >= byte.MinValue
            && entityType <= byte.MaxValue
            && Enum.IsDefined(typeof(EntityType), (byte)entityType))
        {
            var resolved = (EntityType)entityType;
            if (resolved != EntityType.Player)
                return resolved;
        }

        return EntityType.Object;
    }

    private bool TryValidateMember(ClientSession sender, out RoomPlayer player)
    {
        player = null!;

        if (sender == null || !sender.HasAuth)
        {
            _metrics.RecordRouteReject();
            sender?.Send(new SC_Warn { code = 3101, msg = "P2P_NOT_AUTH" }.Write());
            return false;
        }

        var senderUid = sender.Uid;
        if (string.IsNullOrWhiteSpace(senderUid))
        {
            _metrics.RecordRouteReject();
            sender.Send(new SC_Warn { code = 3103, msg = "P2P_UID_EMPTY" }.Write());
            return false;
        }

        lock (_lock)
        {
            if (!_players.TryGetValue((senderUid, sender.Epoch), out var foundPlayer) || foundPlayer == null)
            {
                _metrics.RecordRouteReject();
                sender.Send(new SC_Warn { code = 3102, msg = "P2P_NOT_MEMBER" }.Write());
                return false;
            }

            player = foundPlayer;
            if (!ReferenceEquals(player.Conn, sender))
            {
                _metrics.RecordRouteReject();
                sender.Send(new SC_Warn { code = 3104, msg = "NOT_CURRENT_CONNECTION" }.Write());
                return false;
            }
        }

        return true;
    }

    public void OnCS_ActionRequest(ClientSession sender, CS_ActionRequest req)
        => OnCS_P2PPayload(sender, new CS_P2PPayload
        {
            SenderActorId = sender?.ActorId ?? -1,
            Payload = EncodePacket(req)
        });

    public void OnCS_CalibHit(ClientSession sender, CS_CalibHit req)
        => OnCS_P2PPayload(sender, new CS_P2PPayload
        {
            SenderActorId = sender?.ActorId ?? -1,
            Payload = EncodePacket(req)
        });

    public void OnCS_P2PPayload(ClientSession sender, CS_P2PPayload pkt)
    {
        if (!TryValidateMember(sender, out var player))
            return;

        int inboundPayloadBytes = EstimatePayloadBytes(pkt.Payload);
        _metrics.RecordRelayRecv(inboundPayloadBytes);
        _metrics.RecordQueuePending(_q.Count);

        Enqueue(() =>
        {
            var started = Stopwatch.GetTimestamp();
            bool success = false;

            try
            {
                if (_phase == RoomPhase.Ended)
                {
                    _metrics.RecordDrop();
                    return;
                }

                if (player.ActorId == _hostActorId)
                {
                    _logger.LogDebug(
                        "[P2PRelayRoom] Host broadcast relayId={RelayId} sender={Sender} protocol={Protocol}",
                        RelayId,
                        player.ActorId,
                        DescribePayloadProtocol(pkt.Payload));

                    var targets = CountBroadcastTargetsExcept(sender);
                    var packet = new SC_P2PBroadcast
                    {
                        Payload = pkt.Payload ?? ""
                    };
                    var segment = packet.Write();
                    BroadcastExcept(segment, sender);
                    _metrics.RecordRelaySend(targets, segment.Count);
                    _metrics.RecordAcceptedPayload();
                    success = true;
                    return;
                }

                RoomPlayer? host;
                lock (_lock)
                {
                    host = _players.Values.FirstOrDefault(x => x.ActorId == _hostActorId && x.Conn != null);
                }

                if (host?.Conn == null)
                {
                    _metrics.RecordDrop();
                    _logger.LogWarning(
                        "[P2PRelayRoom] Guest input dropped. Host not connected relayId={RelayId} sender={Sender} hostActor={HostActor} protocol={Protocol}",
                        RelayId,
                        player.ActorId,
                        _hostActorId,
                        DescribePayloadProtocol(pkt.Payload));
                    return;
                }

                pkt.SenderActorId = player.ActorId;
                var forwarded = pkt.Write();
                host.Conn.Send(forwarded);
                _metrics.RecordRelaySend(1, forwarded.Count);
                _metrics.RecordAcceptedPayload();
                success = true;
                _logger.LogDebug(
                    "[P2PRelayRoom] Guest relay forward relayId={RelayId} sender={Sender} hostActor={HostActor} protocol={Protocol}",
                    RelayId,
                    player.ActorId,
                    _hostActorId,
                    DescribePayloadProtocol(pkt.Payload));
            }
            finally
            {
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (success)
                    _metrics.RecordForwardSuccess((long)Math.Ceiling(elapsed));
                else
                    _metrics.RecordForwardFail((long)Math.Ceiling(elapsed));
            }
        });
    }

    public void OnCS_P2PGameResult(ClientSession sender, CS_P2PGameResult pkt)
    {
        if (!TryValidateMember(sender, out var player))
            return;

        if (player.ActorId != _hostActorId)
        {
            _metrics.RecordResultRejected();
            return;
        }

        long serverPlayTimeMs = Math.Max(0, AppRef.ServerTimeMs() - _roomStartTimeMs);
        if (pkt.IsClear && serverPlayTimeMs < 30000)
        {
            _metrics.RecordResultRejected();
            sender.Send(new SC_Warn { code = 3201, msg = "P2P_CLEAR_TOO_FAST" }.Write());
            _logger.LogWarning(
                "[P2PRelayRoom] Suspicious clear rejected relayId={RelayId} host={Host} reported={Reported} server={Server}",
                RelayId,
                player.ActorId,
                pkt.PlayTimeMs,
                serverPlayTimeMs);
            return;
        }

        if (pkt.PlayTimeMs > 0 && Math.Abs(pkt.PlayTimeMs - serverPlayTimeMs) > 15000)
        {
            _metrics.RecordResultRejected();
            sender.Send(new SC_Warn { code = 3202, msg = "P2P_PLAYTIME_MISMATCH" }.Write());
            _logger.LogWarning(
                "[P2PRelayRoom] PlayTime mismatch relayId={RelayId} host={Host} reported={Reported} server={Server}",
                RelayId,
                player.ActorId,
                pkt.PlayTimeMs,
                serverPlayTimeMs);
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var report = new P2PGameResultReport
                {
                    RoomId = RelayId,
                    MapId = MapId,
                    HostUid = player.Uid,
                    HostActorId = player.ActorId,
                    IsClear = pkt.IsClear,
                    ReportedPlayTimeMs = pkt.PlayTimeMs,
                    VerifiedPlayTimeMs = serverPlayTimeMs,
                    TotalDamage = pkt.TotalDamage,
                    PlayerUids = SnapshotPlayerUids(),
                    SubmittedAtMs = AppRef.ServerTimeMs()
                };

                var ok = await ServerServices.ApiClient.PostAsync("/api/game/result", report);
                if (!ok)
                {
                    _metrics.RecordResultRejected();
                    _logger.LogWarning("[P2PRelayRoom] Result report failed relayId={RelayId}", RelayId);
                    return;
                }

                _metrics.RecordResultAccepted();
                Broadcast(new SC_ReturnToTown());
            }
            catch (Exception ex)
            {
                _metrics.RecordResultRejected();
                _logger.LogError(ex, "[P2PRelayRoom] Result report exception relayId={RelayId}", RelayId);
            }
        });
    }

    private List<string> SnapshotPlayerUids()
    {
        lock (_lock)
        {
            return _players.Values.Select(p => p.Uid).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    private static string EncodePacket(IPacket pkt)
    {
        var segment = pkt.Write();
        if (segment.Array == null || segment.Count <= 0)
            return "";

        return Convert.ToBase64String(segment.Array, segment.Offset, segment.Count);
    }

    private static int ResolveMapY(int legacyY, int unityZ)
    {
        if (unityZ != 0)
            return unityZ;

        return legacyY;
    }

    private sealed class P2PGameResultReport
    {
        public string RoomId { get; set; } = "";
        public string MapId { get; set; } = "";
        public string HostUid { get; set; } = "";
        public int HostActorId { get; set; }
        public bool IsClear { get; set; }
        public long ReportedPlayTimeMs { get; set; }
        public long VerifiedPlayTimeMs { get; set; }
        public int TotalDamage { get; set; }
        public List<string> PlayerUids { get; set; } = new();
        public long SubmittedAtMs { get; set; }
    }

    private int CountBroadcastTargetsExcept(ClientSession? except)
    {
        var targets = GetBroadcastSnapshot();
        if (except == null)
            return targets.Length;

        int count = 0;
        foreach (var target in targets)
        {
            if (!ReferenceEquals(target, except))
                count++;
        }

        return count;
    }

    private static int EstimatePayloadBytes(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return 0;

        try
        {
            return Convert.FromBase64String(payload).Length;
        }
        catch
        {
            return payload.Length;
        }
    }

    private static string DescribePayloadProtocol(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return "-";

        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length < 4)
                return "ShortPayload";

            ushort protocol = BitConverter.ToUInt16(bytes, 2);
            return Enum.IsDefined(typeof(PacketID), (int)protocol)
                ? $"{(PacketID)protocol}({protocol})"
                : protocol.ToString();
        }
        catch
        {
            return "DecodeFailed";
        }
    }
}

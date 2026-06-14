using GameServer.Content.Map;
using GameServer.InGame.Manager.Entity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Util;

public sealed class TownP2PRelayRoom : RoomBase
{
    private enum RoomPhase { Waiting, Running, Ended }

    private const int RelayTickRate = 120;
    private const double TownBpm = 180;
    private const string DefaultTownId = "Town_01";

    public string RelayId { get; }
    public string MapId { get; private set; }

    private readonly ILogger _logger;
    private RoomPhase _phase = RoomPhase.Waiting;
    private Map2D? _map;
    private bool _initialized;
    private long _songStartAtMs;
    private int _hostActorId;
    private string _ownerHostUid = "";
    private readonly Dictionary<int, (int x, int y)> _playerSpawns = new();
    private readonly Dictionary<string, int> _preferredActorIdByUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _preferredSeatByUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _preferredDisplayNameByUid = new(StringComparer.OrdinalIgnoreCase);

    public TownP2PRelayRoom(string relayId, string mapId, int maxPlayers = 16, ILogger? logger = null)
        : base(maxPlayers, 10)
    {
        RelayId = relayId;
        MapId = string.IsNullOrWhiteSpace(mapId) ? DefaultTownId : mapId;
        _logger = logger ?? NullLogger.Instance;
    }

    protected override SessionBase? GetSession() => null;
    protected override bool IsRoomRunning() => _phase == RoomPhase.Running;
    protected override bool CheckRoomEnded() => _phase == RoomPhase.Ended;
    protected override string RoomLogKind => "TownP2P";
    protected override string RoomLogId => RelayId;

    public override void Update()
    {
        PumpQueuedActions();
    }

    protected override void UpdateSessionWorldId(ClientSession s)
    {
        s.CurrentWorldId = RelayId;
    }

    public void UpdateHostPreferences(string ownerHostUid, IEnumerable<GameServer.Infrastructure.Api.Dto.GameMatchParticipantResponse>? participants)
    {
        var normalized = NormalizeParticipants(ownerHostUid, participants);
        lock (_lock)
        {
            _ownerHostUid = ownerHostUid ?? "";
            _preferredActorIdByUid.Clear();
            _preferredSeatByUid.Clear();
            _preferredDisplayNameByUid.Clear();
            foreach (var p in normalized)
            {
                if (string.IsNullOrWhiteSpace(p.Uid) || p.ActorId <= 0)
                    continue;

                _preferredActorIdByUid[p.Uid] = p.ActorId;
                _preferredSeatByUid[p.Uid] = Math.Max(0, p.ActorId - 10);
                _preferredDisplayNameByUid[p.Uid] = NormalizePlayerDisplayName(p.DisplayName, p.Uid);
            }
        }

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
        int seat;
        int playerCount = -1;
        RoomPlayer? player;

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
                player = existing;
                actorId = player.ActorId;
                seat = player.SeatIndex;

                player.Attach(s);
                _byActor[actorId] = s;
                s.ActorId = actorId;
                s.SeatIndex = seat;
                UpdateSessionWorldId(s);
                _broadcastDirty = true;
                playerCount = _players.Count;
            }
            else
            {
                if (_players.Count >= _maxPlayers || _freeSeats.Count == 0)
                {
                    LogRoomBindFail(s, _players.Count >= _maxPlayers ? "room_full" : "no_free_seat", _players.Count);
                    return false;
                }

                actorId = ResolvePreferredActorIdUnsafe(s.Uid);
                if (actorId <= 0 || _byActor.ContainsKey(actorId))
                    actorId = ReserveNextActorIdUnsafe();
                else
                    _nextActorId = Math.Max(_nextActorId, actorId + 1);

                var preferredSeat = ResolvePreferredSeatIndexUnsafe(s.Uid);
                if (!TryReserveSeatUnsafe(preferredSeat, out seat))
                {
                    LogRoomBindFail(s, "preferred_seat_unavailable", _players.Count);
                    return false;
                }

                player = new RoomPlayer(s.Uid, s.Epoch, actorId, seat);
                player.Attach(s);
                _players[key] = player;
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
        OnPlayerBound(player, isNew);
        return true;
    }

    protected override void OnPlayerBound(RoomPlayer p, bool isNew)
    {
        Enqueue(() =>
        {
            EnsureStarted();
            ReevaluateHost();

            if (p.Conn != null)
                SendInitPacketToPlayer(p.Conn, isNew);
        });
    }

    public override void DetachIfMatch(string uid, long epoch, string connId)
    {
        var ownerLeft = IsOwner(uid);
        base.DetachIfMatch(uid, epoch, connId);
        if (ownerLeft)
            Enqueue(CloseBecauseHostLeft);
    }

    public override void RemovePlayer(string uid, long epoch)
    {
        int actorId = 0;
        var ownerLeft = IsOwner(uid);
        lock (_lock)
        {
            if (_players.TryGetValue((uid, epoch), out var p))
                actorId = p.ActorId;
        }

        base.RemovePlayer(uid, epoch);

        if (actorId > 0)
            Broadcast(new SC_EntityDespawn { EntityId = actorId });

        if (ownerLeft)
            Enqueue(CloseBecauseHostLeft);
        else
            Enqueue(ReevaluateHost);
    }

    protected override void MaybeEndIfEmpty()
    {
        bool empty;
        lock (_lock)
            empty = _players.Count == 0;

        if (!empty)
            return;

        lock (_lock)
            _phase = RoomPhase.Ended;

        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_end reason=empty roomType=TownP2P world={RelayId} map={MapId}");
        TownP2PRelayManager.Remove(RelayId);
        _logger.LogInformation("[TownP2PRelayRoom] Destroyed empty relayId={RelayId}", RelayId);
    }

    public void OnCS_TownActionRequest(ClientSession sender, CS_TownActionRequest req)
        => OnCS_P2PPayload(sender, new CS_P2PPayload
        {
            SenderActorId = sender?.ActorId ?? -1,
            Payload = EncodePacket(req)
        });

    public void OnCS_P2PPayload(ClientSession sender, CS_P2PPayload pkt)
    {
        if (!TryValidateMember(sender, out var player))
            return;

        Enqueue(() =>
        {
            if (_phase == RoomPhase.Ended)
                return;

            if (player.ActorId == _hostActorId)
            {
                BroadcastExcept(new SC_P2PBroadcast { Payload = pkt.Payload ?? "" }, sender);
                return;
            }

            RoomPlayer? host;
            lock (_lock)
            {
                host = _players.Values.FirstOrDefault(x => x.ActorId == _hostActorId && x.Conn != null && x.Conn.IsConnected);
            }

            if (host?.Conn == null)
            {
                sender.Send(new SC_Warn { code = 3302, msg = "TOWN_HOST_NOT_CONNECTED" }.Write());
                return;
            }

            pkt.SenderActorId = player.ActorId;
            host.Conn.Send(pkt.Write());
        });
    }

    private void EnsureStarted()
    {
        lock (_lock)
        {
            if (_initialized || _phase == RoomPhase.Ended)
                return;

            _initialized = true;
            _phase = RoomPhase.Running;
        }

        _map = ResolveTownMap(MapId);
        _songStartAtMs = AppRef.ServerTimeMs() + 1000;
        _logger.LogInformation("[TownP2PRelayRoom] Started relayId={RelayId} map={MapId}", RelayId, _map.MapId);
    }

    private void SendInitPacketToPlayer(ClientSession s, bool isNewJoin)
    {
        if (s == null || !s.IsConnected)
            return;

        if (!_initialized)
            EnsureStarted();

        Task.Run(async () =>
        {
            var states = new Dictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?>(StringComparer.OrdinalIgnoreCase);
            List<RoomPlayer> playersSnapshot;
            lock (_lock)
                playersSnapshot = _players.Values.ToList();

            foreach (var p in playersSnapshot)
            {
                try
                {
                    states[p.Uid] = await ServerServices.ApiClient.GetPlayerStateAsync(p.Uid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TownP2PRelayRoom] PlayerState load failed uid={Uid}", p.Uid);
                    states[p.Uid] = null;
                }
            }

            var init = BuildInitPacketForPlayer(s.ActorId, states);
            if (!s.IsConnected)
                return;

            s.Send(init.Write());
            s.Send(new SC_BeatSync
            {
                ServerSendTimeMs = AppRef.ServerTimeMs(),
                ClientSendTimeMs = 0,
                SongStartServerTimeMs = _songStartAtMs,
                Bpm = TownBpm,
                BaseBeatDivision = 1,
                BeatIndex = 0
            }.Write());
            s.Send(new SC_HostChange { HostActorId = _hostActorId }.Write());

            await SendInventoryAsync(s);

            if (isNewJoin)
                BroadcastCurrentPlayerSpawns(states);
        });
    }

    private SC_InitMap BuildInitPacketForPlayer(
        int myActorId,
        IReadOnlyDictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?> states)
    {
        var map = _map ?? ResolveTownMap(MapId);
        var packet = new SC_InitMap
        {
            ServerTimeMs = AppRef.ServerTimeMs(),
            Revision = 0,
            TickRate = RelayTickRate,
            MapId = map.MapId,
            MapWidth = map.Width,
            MapHeight = map.Height,
            MapVersion = 0,
            Mode = 3,
            MyActorId = myActorId,
            ActionWindowMs = 100,
            SongId = "TownP2P",
            Bpm = TownBpm,
            BaseBeatDivision = 1,
            SongStartServerTime = _songStartAtMs
        };

        lock (_lock)
        {
            int guestIndex = 1;
            foreach (var p in _players.Values.OrderBy(x => x.SeatIndex).ThenBy(x => x.ActorId))
            {
                states.TryGetValue(p.Uid, out var state);
                var spawn = GetOrAssignSpawn(p);
                string displayName = ResolvePlayerDisplayNameUnsafe(p.Uid);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = $"Guest_{guestIndex++:00}";
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
                    X = spawn.x,
                    Y = spawn.y,
                    Dir = 0,
                    Hp = state?.TotalHp > 0 ? state.TotalHp : 100,
                    AppearanceId = state?.AppearanceId ?? 0
                });
            }
        }

        return packet;
    }

    private async Task SendInventoryAsync(ClientSession s)
    {
        try
        {
            var invItems = await ServerServices.InventoryManager.LoadInventoryAsync(s.Uid, forceReload: true);
            var invPkt = new SC_Inventory();
            foreach (var item in invItems.OrderBy(x => x.SlotIndex))
            {
                var equipTmpl = ServerServices.ItemTemplates.GetEquipment(item.TemplateId);
                if (equipTmpl != null)
                {
                    invPkt.equipmentss.Add(new SC_Inventory.Equipments
                    {
                        InstanceId = item.InstanceId,
                        TemplateId = item.TemplateId,
                        SlotIndex = item.SlotIndex,
                        EnhancementLevel = item.EnhancementLevel,
                        IsEquipped = item.IsEquipped,
                        BaseStats = Newtonsoft.Json.JsonConvert.SerializeObject(item.BaseStats),
                        RandomOptions = Newtonsoft.Json.JsonConvert.SerializeObject(item.RandomOptions),
                        AcquiredAt = item.AcquiredAt.ToString("O")
                    });
                }
                else
                {
                    invPkt.itemss.Add(new SC_Inventory.Items
                    {
                        InstanceId = item.InstanceId,
                        TemplateId = item.TemplateId,
                        Amount = item.Amount,
                        SlotIndex = item.SlotIndex,
                        AcquiredAt = item.AcquiredAt.ToString("O")
                    });
                }
            }

            if (s.IsConnected)
                s.Send(invPkt.Write());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TownP2PRelayRoom] Inventory send failed uid={Uid}", s.Uid);
        }
    }

    private void BroadcastCurrentPlayerSpawns(
        IReadOnlyDictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?> states)
    {
        List<RoomPlayer> snapshot;
        lock (_lock)
            snapshot = _players.Values.Where(x => x.Conn != null && x.Conn.IsConnected).ToList();

        foreach (var p in snapshot)
        {
            states.TryGetValue(p.Uid, out var state);
            var spawn = GetOrAssignSpawn(p);
            Broadcast(new SC_EntitySpawn
            {
                BeatIndex = 0,
                EntityId = p.ActorId,
                EntityType = (int)EntityType.Player,
                AppearanceId = state?.AppearanceId ?? 0,
                X = spawn.x,
                Y = spawn.y,
                Hp = state?.TotalHp > 0 ? state.TotalHp : 100
            });
        }
    }

    private void ReevaluateHost()
    {
        int nextHost = 0;
        lock (_lock)
        {
            if (_phase == RoomPhase.Ended)
                return;

            nextHost = GetConnectedActorIdByUidUnsafe(_ownerHostUid);
            if (nextHost <= 0 && string.IsNullOrWhiteSpace(_ownerHostUid))
                nextHost = _players.Values.OrderBy(x => x.SeatIndex).FirstOrDefault(x => x.Conn != null && x.Conn.IsConnected)?.ActorId ?? 0;

            if (nextHost == _hostActorId)
                return;

            _hostActorId = nextHost;
        }

        Broadcast(new SC_HostChange { HostActorId = _hostActorId });
    }

    private void CloseBecauseHostLeft()
    {
        lock (_lock)
        {
            if (_phase == RoomPhase.Ended)
                return;
            _phase = RoomPhase.Ended;
        }

        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_end reason=host_left roomType=TownP2P world={RelayId} map={MapId} owner={_ownerHostUid}");
        Broadcast(new SC_Warn { code = 3301, msg = "TOWN_HOST_LEFT" });
        TownP2PRelayManager.Remove(RelayId);
    }

    private bool TryValidateMember(ClientSession sender, out RoomPlayer player)
    {
        player = null!;
        if (sender == null || !sender.HasAuth)
        {
            sender?.Send(new SC_Warn { code = 3303, msg = "TOWN_P2P_NOT_AUTH" }.Write());
            return false;
        }

        lock (_lock)
        {
            if (!_players.TryGetValue((sender.Uid, sender.Epoch), out player))
            {
                sender.Send(new SC_Warn { code = 3304, msg = "TOWN_P2P_NOT_MEMBER" }.Write());
                return false;
            }

            if (!ReferenceEquals(player.Conn, sender))
            {
                sender.Send(new SC_Warn { code = 3305, msg = "TOWN_P2P_NOT_CURRENT_CONNECTION" }.Write());
                return false;
            }
        }

        return true;
    }

    private (int x, int y) GetOrAssignSpawn(RoomPlayer player)
    {
        lock (_lock)
        {
            if (_playerSpawns.TryGetValue(player.ActorId, out var spawn))
                return spawn;

            var map = _map ?? ResolveTownMap(MapId);
            var point = map.GetSpawnPointRandom();
            if (point.Item1 < 0 || point.Item2 < 0)
                point = (0, 0);

            spawn = (point.Item1, point.Item2);
            _playerSpawns[player.ActorId] = spawn;
            return spawn;
        }
    }

    private static Map2D ResolveTownMap(string mapId)
    {
        if (MapDatabase.TryGet(mapId, out var map) && map != null)
            return map;

        if (!string.Equals(mapId, DefaultTownId, StringComparison.OrdinalIgnoreCase) &&
            MapDatabase.TryGet(DefaultTownId, out var fallback) &&
            fallback != null)
        {
            LogManager.Instance.LogWarning("TownP2PRelayRoom", $"Map not found mapId={mapId}. Falling back to {DefaultTownId}.");
            return fallback;
        }

        var tiny = new Map2D(1, 1) { MapId = string.IsNullOrWhiteSpace(mapId) ? DefaultTownId : mapId };
        tiny.Set(0, 0, TileKind.Floor);
        tiny.SetSpawnPoint(0, 0);
        return tiny;
    }

    private bool IsOwner(string uid)
        => !string.IsNullOrWhiteSpace(uid)
           && !string.IsNullOrWhiteSpace(_ownerHostUid)
           && string.Equals(uid, _ownerHostUid, StringComparison.OrdinalIgnoreCase);

    private int GetConnectedActorIdByUidUnsafe(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return 0;

        return _players.Values.FirstOrDefault(p =>
            p.Conn != null &&
            p.Conn.IsConnected &&
            string.Equals(p.Uid, uid, StringComparison.OrdinalIgnoreCase))?.ActorId ?? 0;
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
        => !string.IsNullOrWhiteSpace(uid) && _preferredActorIdByUid.TryGetValue(uid, out var actorId) ? actorId : 0;

    private int ResolvePreferredSeatIndexUnsafe(string uid)
        => !string.IsNullOrWhiteSpace(uid) && _preferredSeatByUid.TryGetValue(uid, out var seat) ? seat : -1;

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

        if (_freeSeats.Count == 0)
        {
            seat = -1;
            return false;
        }

        seat = _freeSeats.Dequeue();
        return true;
    }

    private bool TryRemoveSeatUnsafe(int seatIndex)
    {
        if (seatIndex < 0 || _freeSeats.Count == 0)
            return false;

        var removed = false;
        var count = _freeSeats.Count;
        for (var i = 0; i < count; i++)
        {
            var seat = _freeSeats.Dequeue();
            if (!removed && seat == seatIndex)
            {
                removed = true;
                continue;
            }
            _freeSeats.Enqueue(seat);
        }

        return removed;
    }

    private static List<GameServer.Infrastructure.Api.Dto.GameMatchParticipantResponse> NormalizeParticipants(
        string ownerHostUid,
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

        if (!string.IsNullOrWhiteSpace(ownerHostUid) && seen.Add(ownerHostUid))
            normalized.Insert(0, new GameServer.Infrastructure.Api.Dto.GameMatchParticipantResponse { Uid = ownerHostUid });

        return normalized;
    }

    private static string EncodePacket(IPacket pkt)
    {
        var segment = pkt.Write();
        if (segment.Array == null || segment.Count <= 0)
            return "";

        return Convert.ToBase64String(segment.Array, segment.Offset, segment.Count);
    }
}

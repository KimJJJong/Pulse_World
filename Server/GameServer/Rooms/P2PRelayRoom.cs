using GameServer.Content.Game.Entity;
using GameServer.Content.Map;
using GameServer.InGame.Director.Data;
using GameServer.InGame.Manager.Entity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Util;

public sealed class P2PRelayRoom : RoomBase
{
    private enum RoomPhase { Waiting, Running, Ended }

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

    private const int RelayTickRate = 480;

    public P2PRelayRoom(string relayId, string mapId, int maxPlayers = 8, ILogger? logger = null)
        : base(maxPlayers, 1)
    {
        RelayId = relayId;
        MapId = mapId;
        _logger = logger ?? NullLogger.Instance;
    }

    protected override SessionBase? GetSession() => null;
    protected override bool IsRoomRunning() => _phase == RoomPhase.Running;
    protected override bool CheckRoomEnded() => _phase == RoomPhase.Ended;

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

        lock (_lock)
        {
            _phase = RoomPhase.Ended;
        }

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
            EnsureStarted();
            ReevaluateHost();

            if (p.Conn != null)
                SendInitPacketToPlayer(p.Conn);
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
            "[P2PRelayRoom] Started relayId={RelayId} mapId={MapId} songStart={SongStart} host={Host}",
            RelayId,
            MapId,
            _songStartAtMs,
            _hostActorId);
    }

    private void ReevaluateHost()
    {
        int nextHost = 0;
        lock (_lock)
        {
            if (_phase == RoomPhase.Ended)
                return;

            if (_hostActorId > 0 &&
                _byActor.TryGetValue(_hostActorId, out var cur) &&
                cur != null &&
                cur.IsConnected)
            {
                nextHost = _hostActorId;
            }
            else
            {
                foreach (var p in _players.Values.OrderBy(p => p.SeatIndex).ThenBy(p => p.ActorId))
                {
                    if (p.Conn != null && p.Conn.IsConnected)
                    {
                        nextHost = p.ActorId;
                        break;
                    }
                }
            }

            if (nextHost == _hostActorId)
                return;

            _hostActorId = nextHost;
        }

        Broadcast(new SC_HostChange { HostActorId = _hostActorId });
        _logger.LogInformation("[P2PRelayRoom] Host changed relayId={RelayId} host={Host}", RelayId, _hostActorId);
    }

    public void SendInitPacketToPlayer(ClientSession s)
    {
        if (s == null || !s.IsConnected)
            return;

        int myActorId = s.ActorId;
        if (myActorId < 0)
            return;

        if (!_relayInitialized)
            EnsureStarted();

        Task.Run(async () =>
        {
            var states = new Dictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?>(StringComparer.OrdinalIgnoreCase);
            List<RoomPlayer> playersSnapshot;

            lock (_lock)
            {
                playersSnapshot = _players.Values.ToList();
            }

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

            var init = BuildInitPacketForPlayer(myActorId, states);

            if (states.TryGetValue(s.Uid, out var myState) && myState != null)
            {
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

            s.Send(init.Write());
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
        });
    }

    private SC_InitMap BuildInitPacketForPlayer(
        int myActorId,
        IReadOnlyDictionary<string, GameServer.Infrastructure.Api.Dto.PlayerStateResponse?> states)
    {
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
            foreach (var p in _players.Values.OrderBy(p => p.SeatIndex).ThenBy(p => p.ActorId))
            {
                states.TryGetValue(p.Uid, out var pState);
                int hp = pState?.TotalHp > 0 ? pState.TotalHp : 1000;
                int atk = pState?.TotalAtk ?? 0;
                int def = pState?.TotalDef ?? 0;
                int appearanceId = pState?.AppearanceId ?? 0;

                var spawn = GetOrAssignSpawn(p);
                packet.playerss.Add(new SC_InitMap.Players
                {
                    Uid = p.Uid,
                    ActorId = p.ActorId,
                    Name = p.Uid
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
            }
        }

        if (_stage != null)
        {
            foreach (var spawn in _stage.InitialSpawns ?? new List<SpawnData>())
            {
                var entityData = EntityDataManager.Instance.Get(spawn.MonsterId);
                packet.entitiess.Add(new SC_InitMap.Entities
                {
                    EntityId = spawn.MonsterId,
                    EntityType = (int)EntityType.Monster,
                    OwnerSlot = -1,
                    X = spawn.X,
                    Y = ResolveMapY(spawn.Y, spawn.Z),
                    Dir = 0,
                    Hp = entityData?.MaxHp > 0 ? entityData.MaxHp : 50,
                    AppearanceId = spawn.MonsterId
                });
            }

            foreach (var obj in _stage.InitialObjects ?? new List<SpawnObjectData>())
            {
                packet.entitiess.Add(new SC_InitMap.Entities
                {
                    EntityId = obj.EntityId,
                    EntityType = obj.EntityType,
                    OwnerSlot = obj.GroupId,
                    X = obj.X,
                    Y = ResolveMapY(obj.Y, obj.Z),
                    Dir = 0,
                    Hp = 1,
                    AppearanceId = obj.EntityId
                });
            }
        }

        return packet;
    }

    private bool TryValidateMember(ClientSession sender, out RoomPlayer player)
    {
        player = null!;

        if (sender == null || !sender.HasAuth)
        {
            sender?.Send(new SC_Warn { code = 3101, msg = "P2P_NOT_AUTH" }.Write());
            return false;
        }

        lock (_lock)
        {
            if (!_players.TryGetValue((sender.Uid, sender.Epoch), out player))
            {
                sender.Send(new SC_Warn { code = 3102, msg = "P2P_NOT_MEMBER" }.Write());
                return false;
            }

            if (!ReferenceEquals(player.Conn, sender))
            {
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

        Enqueue(() =>
        {
            if (_phase == RoomPhase.Ended)
                return;

            if (player.ActorId == _hostActorId)
            {
                BroadcastExcept(new SC_P2PBroadcast
                {
                    Payload = pkt.Payload ?? ""
                }, sender);
                return;
            }

            RoomPlayer? host;
            lock (_lock)
            {
                host = _players.Values.FirstOrDefault(x => x.ActorId == _hostActorId && x.Conn != null);
            }

            if (host?.Conn == null)
            {
                _logger.LogWarning("[P2PRelayRoom] Guest input dropped. Host not connected relayId={RelayId} sender={Sender}", RelayId, player.ActorId);
                return;
            }

            pkt.SenderActorId = player.ActorId;
            host.Conn.Send(pkt.Write());
        });
    }

    public void OnCS_P2PGameResult(ClientSession sender, CS_P2PGameResult pkt)
    {
        if (!TryValidateMember(sender, out var player))
            return;

        if (player.ActorId != _hostActorId)
            return;

        long serverPlayTimeMs = Math.Max(0, AppRef.ServerTimeMs() - _roomStartTimeMs);
        if (pkt.IsClear && serverPlayTimeMs < 30000)
        {
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
                    _logger.LogWarning("[P2PRelayRoom] Result report failed relayId={RelayId}", RelayId);
                    return;
                }

                Broadcast(new SC_ReturnToTown());
            }
            catch (Exception ex)
            {
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
}

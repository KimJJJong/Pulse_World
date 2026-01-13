// ===============================
// GameRoom.cs  (slot 제거 + actorId 통일 + SeatIndex 내부 유지)
// ===============================
using Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Util;

using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using GameServer.Content.Map;

public sealed class GameRoom : IGameBroadcaster, IUpdatable
{
    readonly object _lock = new();

    // actorId -> session (최신 연결)
    readonly Dictionary<int, ClientSession?> _byActor = new();

    // (uid, epoch) -> player ref (논리 플레이어)
    readonly Dictionary<(string uid, long epoch), PlayerRef> _players = new();

    // 로딩 완료 체크(UID 기준)
    readonly HashSet<string> _loaded = new();

    // broadcast snapshot
    private ClientSession[] _broadcastSnapshot = Array.Empty<ClientSession>();
    private bool _broadcastDirty = true;

    public string MatchId { get; }
    public int MapId { get; private set; } = 0;
    public int Seed { get; private set; } = 0;

    enum RoomPhase { Waiting, Loading, Countdown, Running, Ended }
    RoomPhase _phase = RoomPhase.Waiting;

    private readonly ILogger _logger;

    // 리듬 + 인게임 세션
    private GameSession? _session;
    private RhythmSystem? _rhythm;
    private RhythmConfig? _rhythmConfig;
    private Map2D? _map;

    // ---- Settings ----
    private readonly int _maxPlayers = 2; // 1v1이면 2
    private readonly Queue<int> _freeSeats = new();
    private int _nextPlayerActorId = 1;

    // monster ids
    private int _nextMonsterId = 100;

    // IServerTime 어댑터
    private sealed class ServerTimeAdapter : IServerTime
    {
        public long NowMs => AppRef.ServerTimeMs();
    }

    public GameRoom(string matchId, ILogger? logger = null, int maxPlayers = 2)
    {
        MatchId = matchId;
        Seed = Environment.TickCount;
        _logger = logger ?? NullLogger.Instance;

        _maxPlayers = Math.Max(1, maxPlayers);

        for (int i = 0; i < _maxPlayers; i++)
            _freeSeats.Enqueue(i);
    }

    // =====================================================
    // Bind / Unbind (actorId 통일)
    // =====================================================
    public bool BindOrReattach(ClientSession s, out int actorId)
    {
        actorId = -1;

        if (s == null || !s.HasAuth || string.IsNullOrEmpty(s.Uid))
            return false;

        lock (_lock)
        {
            if (_phase == RoomPhase.Ended) return false;

            var key = (s.Uid!, s.Epoch);

            // reattach
            if (_players.TryGetValue(key, out var existing))
            {
                actorId = existing.ActorId;

                existing.Attach(s);
                _byActor[actorId] = s;

                s.ActorId = actorId;
                s.SeatIndex = existing.SeatIndex;
                s.CurrentWorldId = MatchId;

                _broadcastDirty = true;
                return true;
            }

            // new join
            if (_players.Count >= _maxPlayers) return false;
            if (_freeSeats.Count == 0) return false;

            actorId = _nextPlayerActorId++;
            int seat = _freeSeats.Dequeue();

            var p = new PlayerRef(s.Uid!, s.Epoch, actorId, seat);
            p.Attach(s);

            _players[key] = p;
            _byActor[actorId] = s;

            s.ActorId = actorId;
            s.SeatIndex = seat;
            s.CurrentWorldId = MatchId;

            _broadcastDirty = true;
            return true;
        }
    }

    public void DetachIfMatch(string uid, long epoch, string connId)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out var p))
                return;

            var cur = p.Conn;
            if (cur == null || cur.ConnId != connId)
                return;

            _byActor[p.ActorId] = null;
            p.Detach();

            _broadcastDirty = true;
        }

        // (정석) grace 붙이려면 여기서 타이머 시작 후 RemovePlayer 호출
    }

    public void RemovePlayer(string uid, long epoch, string reason = "leave")
    {
        PlayerRef? p;

        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out p))
                return;

            _players.Remove((uid, epoch));
            _byActor.Remove(p.ActorId);

            _loaded.Remove(uid);

            _freeSeats.Enqueue(p.SeatIndex);
            _broadcastDirty = true;
        }

        _session?.OnPlayerLeft(p.ActorId);

        GetBroadcastSnapshot();
        if (_broadcastSnapshot.Length <= 0)
            GameManager.Remove(MatchId);
    }

    // =====================================================
    // Loading gate
    // =====================================================
    public bool MarkLoadedAsync(ClientSession s)
    {
        lock (_lock)
        {
            if (s?.Uid == null) return false;

            _loaded.Add(s.Uid);

            // 현재 방의 논리 플레이어가 전부 로딩 완료인지 체크
            foreach (var p in _players.Values)
            {
                if (!_loaded.Contains(p.Uid))
                    return false;
            }

            return _players.Count > 0;
        }
    }

    public IEnumerable<(string uid, int actorId, bool loaded)> GetPlayersSnapshot()
    {
        lock (_lock)
        {
            foreach (var p in _players.Values)
                yield return (p.Uid, p.ActorId, _loaded.Contains(p.Uid));
        }
    }

    // =====================================================
    // Broadcaster helpers
    // =====================================================
    private void Broadcast(ArraySegment<byte> payload)
    {
        var targets = GetBroadcastSnapshot();
        foreach (var t in targets)
            t.Send(payload);
    }

    public void Broadcast(IPacket pkt) => Broadcast(pkt.Write());

    private void SendTo(ClientSession s, ArraySegment<byte> payload) => s?.Send(payload);
    public void SendTo(ClientSession s, IPacket pkt) => SendTo(s, pkt.Write());

    public void SendToActor(int actorId, IPacket pkt)
    {
        ClientSession? target;
        lock (_lock) _byActor.TryGetValue(actorId, out target);
        target?.Send(pkt.Write());
    }

    public ClientSession? GetSessionByActor(int actorId)
    {
        lock (_lock) return _byActor.TryGetValue(actorId, out var s) ? s : null;
    }

    private ClientSession[] GetBroadcastSnapshot()
    {
        lock (_lock)
        {
            if (!_broadcastDirty)
                return _broadcastSnapshot;

            var list = new List<ClientSession>(_byActor.Count);
            foreach (var s in _byActor.Values)
            {
                if (s != null)
                    list.Add(s);
            }

            _broadcastSnapshot = list.ToArray();
            _broadcastDirty = false;
            return _broadcastSnapshot;
        }
    }

    // =====================================================
    // Start
    // =====================================================
    public void ScheduleStart(long startAtMs)
    {
        _phase = RoomPhase.Countdown;
        var delay = Math.Max(0, (int)(startAtMs - AppRef.ServerTimeMs()));

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, AppRef.Cts.Token);
                StartGameplay();
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduleStart failed match={MatchId}", MatchId);
            }
        });
    }

    private void StartGameplay()
    {
        lock (_lock)
        {
            if (_phase == RoomPhase.Running || _phase == RoomPhase.Ended)
                return;
            _phase = RoomPhase.Running;
        }

        // 1) 맵
        _map = MapDatabase.Get("Map");

        // 2) 리듬 설정
        _rhythmConfig = new RhythmConfig
        {
            Bpm = 120,
            BaseBeatDivision = 1,
            ActionWindowMs = 100,
            MaxBeatLookAhead = 2,
        };

        long songStart = AppRef.ServerTimeMs() + 1000;

        var time = new ServerTimeAdapter();
        _rhythm = new RhythmSystem(time, _rhythmConfig, songStart);

        // 3) GameSession 구성
        _session = new GameSession(
            sessionId: 0,
            time: time,
            broadcaster: this,
            rhythm: _rhythm,
            rhythmConfig: _rhythmConfig,
            map: _map
        );

        _rhythm.OnBeat += _session.OnBeat;

        // 4) 엔티티 생성 (players / monsters)
        var players = BuildPlayerEntities_NoSlot();
        var monsters = BuildMonsterEntitiesForPrototype();

        _session.InitGame(players, monsters);

        foreach (var s in GetBroadcastSnapshot())
            _session.SendInitPacketToPlayer(s);

        // 5) BeatSync
        Broadcast(new SC_BeatSync
        {
            ServerSendTimeMs = AppRef.ServerTimeMs(),
            SongStartServerTimeMs = songStart,
            Bpm = _rhythmConfig.Bpm,
            BaseBeatDivision = _rhythmConfig.BaseBeatDivision,
            BeatIndex = _rhythm.GetCurrentBeatIndex(time.NowMs),
        });

        _logger.LogInformation("GameRoom {MatchId} started rhythm gameplay", MatchId);
    }

    private List<MapEntity> BuildPlayerEntities_NoSlot()
    {
        var players = new List<MapEntity>();
        if (_map == null) return players;

        lock (_lock)
        {
            foreach (var p in _players.Values)
            {
                var spawnSet = _map.GetSpawnPointRandom();//GetSpawnPoint(p.SeatIndex);
                var spawn = new GridPos(spawnSet.Item1, spawnSet.Item2);

                var e = new MapEntity(
                    id: p.ActorId, // ★ actorId
                    type: EntityType.Player,
                    initialPos: spawn
                );

                e.SetState("HP", 100);
                e.SetState("Uid", p.Uid);

                players.Add(e);
            }
        }

        return players;
    }

    private List<MapEntity> BuildMonsterEntitiesForPrototype()
    {
        var monsters = new List<MapEntity>
        {
            CreateMonster(x: 10, y: 16, hp: 50)
        };
        return monsters;
    }

    private MapEntity CreateMonster(int x, int y, int hp)
    {
        var id = Interlocked.Increment(ref _nextMonsterId);
        var m = new MapEntity(id, EntityType.Monster, new GridPos(x, y));
        m.SetState("HP", hp);

        // ⚠️ ownerActorId 개념이면 -1 유지
        m.SetState("OwnerActorId", -1);
        return m;
    }

    // =====================================================
    // Work queue / Update
    // =====================================================
    readonly System.Collections.Concurrent.ConcurrentQueue<Action> _q = new();
    int _pumping = 0;

    private void Enqueue(Action a) => _q.Enqueue(a);

    private void PumpQueuedActions()
    {
        if (Interlocked.Exchange(ref _pumping, 1) == 1) return;
        try { while (_q.TryDequeue(out var a)) a(); }
        finally { _pumping = 0; }
    }

    private bool IsRunnableFor(ClientSession s)
    {
        if (_phase != RoomPhase.Running) return false;
        if (s == null || !s.HasAuth) return false;

        // actorId 최신 연결인지 확인(재연결/교체 안전)
        int actorId = s.ActorId;
        if (actorId < 0) return false;

        lock (_lock)
        {
            return _byActor.TryGetValue(actorId, out var cur) && ReferenceEquals(cur, s);
        }
    }

    public void Update()
    {
        if (_phase != RoomPhase.Running) return;

        PumpQueuedActions();

        _rhythm?.Update();
        _session?.Update();
    }

    // =====================================================
    // Packet Routing (actorId 통일)
    // =====================================================
    public void OnCS_ActionRequest(ClientSession s, CS_ActionRequest p)
        => Enqueue(() =>
        {
            if (!IsRunnableFor(s))
            {
                s.Send(new SC_Warn { code = 2001, msg = "ROOM_NOT_RUNNING_OR_NOT_MEMBER" }.Write());
                return;
            }

            if (_session == null)
            {
                s.Send(new SC_Warn { code = 2002, msg = "SESSION_NOT_READY" }.Write());
                return;
            }

            int actorId = s.ActorId;
            if (actorId < 0)
            {
                s.Send(new SC_Warn { code = 2003, msg = "UNKNOWN_ACTOR" }.Write());
                return;
            }

            _session.OnClientActionPacketByActorId(actorId, p);
        });

    public void OnCS_CalibHit(ClientSession s, CS_CalibHit p)
        => Enqueue(() =>
        {
            if (!IsRunnableFor(s))
            {
                s.Send(new SC_Warn { code = 2001, msg = "ROOM_NOT_RUNNING_OR_NOT_MEMBER" }.Write());
                return;
            }

            if (_session == null)
            {
                s.Send(new SC_Warn { code = 2002, msg = "SESSION_NOT_READY" }.Write());
                return;
            }

            int actorId = s.ActorId;
            if (actorId < 0)
            {
                s.Send(new SC_Warn { code = 2003, msg = "UNKNOWN_ACTOR" }.Write());
                return;
            }

            _session.OnClientCalibPacketByActorId(actorId, p);
        });

    // =====================================================
    // PlayerRef
    // =====================================================
    private sealed class PlayerRef
    {
        public string Uid { get; }
        public long Epoch { get; }
        public int ActorId { get; }
        public int SeatIndex { get; }

        public ClientSession? Conn { get; private set; }

        public PlayerRef(string uid, long epoch, int actorId, int seatIndex)
        {
            Uid = uid;
            Epoch = epoch;
            ActorId = actorId;
            SeatIndex = seatIndex;
        }

        public void Attach(ClientSession s) => Conn = s;
        public void Detach() => Conn = null;
    }
}

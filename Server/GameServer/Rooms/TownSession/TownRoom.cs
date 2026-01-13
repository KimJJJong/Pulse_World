using GameServer.Content.Map;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using Util;

public sealed class TownRoom : IGameBroadcaster, IUpdatable
{
    readonly object _lock = new();

    // actorId -> session(연결)
    readonly Dictionary<int, ClientSession?> _byActor = new();

    // (uid,epoch) -> player
    readonly Dictionary<(string uid, long epoch), PlayerRef> _players = new();

    // broadcast snapshot
    private ClientSession[] _broadcastSnapshot = Array.Empty<ClientSession>();
    private bool _broadcastDirty = true;

    public string TownId { get; }

    enum RoomPhase { Waiting, Running, Ended }
    RoomPhase _phase = RoomPhase.Waiting;

    private readonly ILogger _logger;

    private TownSession? _session;
    private RhythmSystem? _rhythm;
    private RhythmConfig? _rhythmConfig;
    private Map2D? _map;

    readonly int _maxPlayers;
    int _nextActorId = 10;

    readonly Queue<int> _freeSeats = new();

    public TownRoom(string townId, int maxPlayers = 64, ILogger? logger = null)
    {
        TownId = townId;
        _logger = logger ?? NullLogger.Instance;

        _maxPlayers = Math.Max(1, maxPlayers);
        for (int i = 0; i < _maxPlayers; i++)
            _freeSeats.Enqueue(i);

        StartTownIfNeeded();
    }

    // =========================
    // Bind / Detach / Remove (actorId 중심)
    // =========================
    public bool BindOrReattach(ClientSession s, out int actorId)
    {
        actorId = -1;
        if (s == null || !s.HasAuth || string.IsNullOrEmpty(s.Uid))
            return false;

        bool isNew = false;
        int seat = -1;

        lock (_lock)
        {
            if (_phase == RoomPhase.Ended) return false;

            var key = (s.Uid, s.Epoch);

            if (_players.TryGetValue(key, out var p))
            {
                // reattach
                actorId = p.ActorId;
                seat = p.SeatIndex;

                p.Attach(s);
                _byActor[actorId] = s;

                s.ActorId = actorId;
                s.SeatIndex = seat;
                s.CurrentWorldId = TownId;

                _broadcastDirty = true;
                isNew = false;
            }
            else
            {
                // new join
                if (_players.Count >= _maxPlayers) return false;
                if (_freeSeats.Count == 0) return false;

                actorId = _nextActorId++;
                seat = _freeSeats.Dequeue();

                var np = new PlayerRef(s.Uid, s.Epoch, actorId, seat);
                np.Attach(s);

                _players[key] = np;
                _byActor[actorId] = s;

                s.ActorId = actorId;
                s.SeatIndex = seat;
                s.CurrentWorldId = TownId;

                _broadcastDirty = true;
                isNew = true;
            }
        }

        StartTownIfNeeded();

        //  여기서부터는 워커 스레드(큐)에서 TownSession/World2D를 건드리기
        Enqueue(() =>
        {
            if (_phase != RoomPhase.Running || _session == null || _map == null)
                return;

            // 신규면 월드에 엔티티 생성/등록
            if (isNew)
            {
                var spawnSet = _map.GetSpawnPointRandom();
                var spawn = new GridPos(spawnSet.Item1, spawnSet.Item2);

                var e = new MapEntity(
                    id: s.ActorId,
                    type: EntityType.Player,
                    initialPos: spawn
                );

                e.SetState("HP", 100);
                e.SetState("Uid", s.Uid);

                _session.OnPlayerJoined(e);

                // (선택) 기존 유저들에게 스폰 브로드캐스트를 따로 하고 싶으면 여기서
                // _broadcaster.Broadcast(new SC_EntitySpawn { ... }.Write());
            }
            else
            {
                // 재연결인데 월드에 없을 수도 있으니 안전망
                _session.EnsurePlayerSpawned(s.ActorId);
            }

            // 이제 init 가드 통과함
            _session.SendInitPacketToPlayer(s);
        });

        return true;
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

            // 연결만 떼기
            _byActor[p.ActorId] = null;
            p.Detach();

            _broadcastDirty = true;

            // (선택) grace 붙이려면 여기서 타이머 시작
        }
    }

    public void RemovePlayer(string uid, long epoch)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out var p))
                return;

            _players.Remove((uid, epoch));
            _byActor.Remove(p.ActorId);

            _freeSeats.Enqueue(p.SeatIndex);

            _broadcastDirty = true;

            Console.WriteLine($"[RemovePlayer] uid:{uid} || epoch : {epoch}");

            Enqueue(() => _session?.OnPlayerLeft(p.ActorId));
        }

        MaybeEndIfEmpty();
    }

    // =========================
    // Broadcaster
    // =========================
    public void Broadcast(IPacket pkt) => Broadcast(pkt.Write());

    private void Broadcast(ArraySegment<byte> payload)
    {
        var targets = GetBroadcastSnapshot();
        foreach (var t in targets)
            t.Send(payload);
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
                if (s != null && s.IsConnected)
                    list.Add(s);
            }

            _broadcastSnapshot = list.ToArray();
            _broadcastDirty = false;
            return _broadcastSnapshot;
        }
    }

    // =========================
    // Start Town
    // =========================
    private void StartTownIfNeeded()
    {
        lock (_lock)
        {
            if (_phase == RoomPhase.Running || _phase == RoomPhase.Ended)
                return;

            _phase = RoomPhase.Running;
        }

        _map = MapDatabase.Get("Town_01");

        _rhythmConfig = new RhythmConfig
        {
            Bpm = 240,
            BaseBeatDivision = 1,
            ActionWindowMs = 100,
            MaxBeatLookAhead = 2,
        };

        long songStart = AppRef.ServerTimeMs() ;


        var time = new ServerTimeAdapter();
        _rhythm = new RhythmSystem(time, _rhythmConfig, songStart );
        _session = new TownSession(
            sessionId: 0, 
            time: time, 
            broadcaster: this,
            rhythm: _rhythm,
            rhythmConfig: _rhythmConfig,
            map: _map);

        _rhythm.OnBeat += _session.OnBeat;


        // 월드에 현재 논리 플레이어(연결 유무 상관없이)로 엔티티 빌드
        var players = BuildPlayerEntities();
        _session.InitTown(players);

        foreach (var cs in GetBroadcastSnapshot())
            _session.SendInitPacketToPlayer(cs);

        Broadcast(new SC_BeatSync
        {
            ServerSendTimeMs = AppRef.ServerTimeMs(),
            SongStartServerTimeMs = songStart,
            Bpm = _rhythmConfig.Bpm,
            BaseBeatDivision = _rhythmConfig.BaseBeatDivision,
            BeatIndex = _rhythm.GetCurrentBeatIndex(time.NowMs),
        });

        _logger.LogInformation("TownRoom {TownId} started", TownId);
    }

    private List<MapEntity> BuildPlayerEntities()
    {
        var players = new List<MapEntity>();
        if (_map == null) return players;
        lock (_lock)
        {
            foreach (var p in _players.Values)
            {
                // 스폰은 seatIndex 기반(내부 전용)
                var spawnSet = _map.GetSpawnPointRandom();//GetSpawnPoint(p.SeatIndex);
                var spawn = new GridPos(spawnSet.Item1, spawnSet.Item2);

                var e = new MapEntity(
                    id: p.ActorId, //  entityId = actorId
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

    // =========================
    // Queue / Update / Packet Routing
    // =========================
    readonly System.Collections.Concurrent.ConcurrentQueue<Action> _q = new();
    int _pumping = 0;

    private void Enqueue(Action a) => _q.Enqueue(a);

    private void PumpQueuedActions()
    {
        if (Interlocked.Exchange(ref _pumping, 1) == 1) return;
        try { while (_q.TryDequeue(out var a)) a(); }
        finally { _pumping = 0; }
    }

    public void Update()
    {
        if (_phase != RoomPhase.Running) return;
        PumpQueuedActions();

        _rhythm?.Update();
        _session?.Update();
    }

    public void OnCS_ActionRequest(ClientSession s, CS_ActionRequest p)
        => Enqueue(() =>
        {
            if (_phase != RoomPhase.Running || _session == null || s == null || !s.HasAuth)
            {
                s.Send(new SC_Warn { code = 3001, msg = "TOWN_NOT_RUNNING_OR_NOT_MEMBER" }.Write());
                return;
            }

            int actorId = s.ActorId;
            if (actorId < 0)
            {
                s.Send(new SC_Warn { code = 3003, msg = "UNKNOWN_ACTOR" }.Write());
                return;
            }

            // 연결이 현재 actorId에 바인딩된 최신 세션인지 확인(재연결 안전)
            lock (_lock)
            {
                if (!_byActor.TryGetValue(actorId, out var cur) || !ReferenceEquals(cur, s))
                {
                    s.Send(new SC_Warn { code = 3004, msg = "NOT_CURRENT_CONNECTION" }.Write());
                    return;
                }
            }

            _session.OnClientActionPacketByActorId(actorId, p);
        });

    public void OnCS_TownActionRequest(ClientSession s, CS_TownActionRequest p)
    => Enqueue(() =>
    {
        if (_phase != RoomPhase.Running || _session == null || s == null || !s.HasAuth)
        {
            s.Send(new SC_Warn { code = 3001, msg = "TOWN_NOT_RUNNING_OR_NOT_MEMBER" }.Write());
            return;
        }

        int actorId = s.ActorId;
        if (actorId < 0)
        {
            s.Send(new SC_Warn { code = 3003, msg = "UNKNOWN_ACTOR" }.Write());
            return;
        }

        // 연결이 현재 actorId에 바인딩된 최신 세션인지 확인(재연결 안전)
        lock (_lock)
        {
            if (!_byActor.TryGetValue(actorId, out var cur) || !ReferenceEquals(cur, s))
            {
                s.Send(new SC_Warn { code = 3004, msg = "NOT_CURRENT_CONNECTION" }.Write());
                return;
            }
        }

        _session.OnClientActionPacketByActorId(actorId, p);
    });

    // =========================
    // Room end
    // =========================
    void MaybeEndIfEmpty()
    {
        bool empty;
        lock (_lock) empty = _players.Count == 0;

        if (!empty) return;

        Console.WriteLine($"[RoomEnd] 일단 인원이 0명이어도 열려 있어야함 : 이부분 이후에 수정 필요");
        return;
        lock (_lock) _phase = RoomPhase.Ended;
        TownManager.Remove(TownId);
    }

    // ---------- nested ----------
    sealed class PlayerRef
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

    private sealed class ServerTimeAdapter : IServerTime
    {
        public long NowMs => AppRef.ServerTimeMs();
    }
}

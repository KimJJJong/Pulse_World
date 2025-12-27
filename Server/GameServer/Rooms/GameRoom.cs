using Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Util;
#region Rhythm 
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;

using GameServer.InGame.System.Rhythm;
using System.Threading;
using GameServer.Content.Map;
using static SC_AllPlayersLoaded;
#endregion

public sealed class GameRoom : IGameBroadcaster
{
    readonly object _lock = new();

    // slot -> session
    readonly Dictionary<int, ClientSession> _slots = new();
    readonly HashSet<string> _loaded = new(); // uid
    // broadcast snapshot
    private ClientSession[] _broadcastSnapshot = Array.Empty<ClientSession>();
    private bool _broadcastDirty = true;

    public string MatchId { get; }
    public int MapId { get; private set; } = 0;
    public int Seed { get; private set; } = 0;

    enum RoomPhase { Waiting, Loading, Countdown, Running, Ended }
    RoomPhase _phase = RoomPhase.Waiting;


    private readonly ILogger _logger;
    #region 리듬 게임 관련 필드

    // 리듬 + 인게임 세션
    private GameSession _session;
    private RhythmSystem _rhythm;
    private RhythmConfig _rhythmConfig;
    private Map2D _map;


    // IServerTime 어댑터
    private sealed class ServerTimeAdapter : IServerTime
    {
        public long NowMs => AppRef.ServerTimeMs();
    }

    #endregion

    public GameRoom(string matchId, ILogger logger = null)
    {
        MatchId = matchId;
        Seed = Environment.TickCount;
        _logger = logger ?? NullLogger.Instance;
        _slots.Clear();
    }

    public bool Bind(int slot, ClientSession s)
    {
        lock (_lock)
        {
            if (slot < 0) return false;

            if (_slots.TryGetValue(slot, out var existing) && existing != null && !ReferenceEquals(existing, s))
                return false; // already Occupying


            _slots[slot] = s;

            s.Slot = slot;

            _broadcastDirty = true;

            return true;
        }
    }
    // broadCastdirty를 만들었으면 Unbind 제대로 써먹고 사용 해야하다이!
    public void Unbind(ClientSession s)
    {
        lock (_lock)
        {
            var slotsToRemove = _slots
                .Where(kv => ReferenceEquals(kv.Value, s))
                .Select(kv => kv.Key)
                .ToArray();


            foreach (var slot in slotsToRemove)
            {
                 _session.OnPlayerLeft(slot);

                _slots[slot] = null;
            }

            _broadcastDirty = true;

            GetBroadcastSnapshot();

            Console.WriteLine($"Length!@#!$!@%!#$!#@ : {_broadcastSnapshot.Length}");
            if (_broadcastSnapshot.Length <= 0)
                RoomManager.Remove(MatchId);
             
        }
    }

    public bool MarkLoadedAsync(ClientSession s)
    {
        lock (_lock)
        {
            if (s.Uid == null)
                return false;

            _loaded.Add(s.Uid);

            // 현재 방에 바인딩된 모든 세션이 로딩 완료인지 체크
            foreach (var cs in _slots.Values)
            {
                if (cs?.Uid == null)
                    return false;

                if (!_loaded.Contains(cs.Uid))
                    return false;
            }

            return true; // 전원 로딩 완료
        }
    }

    public IEnumerable<(string uid, int slot, bool loaded)> GetPlayersSnapshot()
    {
        lock (_lock)
        {
            foreach (var kv in _slots)
            {
                var s = kv.Value;
                if (s != null)
                    yield return (s.Uid!, kv.Key, _loaded.Contains(s.Uid!));
            }
        }
    }

    #region GameRoom Util
    private void Broadcast(ArraySegment<byte> payload)
    {
        var targets = GetBroadcastSnapshot();

        foreach (var t in targets)
            t.Send(payload);
    }
    public void Broadcast(IPacket pkt) => Broadcast(pkt.Write());
    private void SendTo(ClientSession s, ArraySegment<byte> payload) => s?.Send(payload);
    public void SendTo(ClientSession s, IPacket pkt) => SendTo(s, pkt.Write());
    public void SendToSlot(int slot, IPacket pkt)
    {
        ClientSession target;
        lock (_lock) _slots.TryGetValue(slot, out target);
        target?.Send(pkt.Write());
    }
    public ClientSession GetSessionBySlot(int slot)
    {
        lock (_lock) return _slots.TryGetValue(slot, out var s) ? s : null;
    }
    private ClientSession[] GetBroadcastSnapshot()
    {
        lock (_lock)
        {
            if (!_broadcastDirty)
                return _broadcastSnapshot;

            if (_slots.Count == 0)
            {
                _broadcastSnapshot = Array.Empty<ClientSession>();
                _broadcastDirty = false;
                return _broadcastSnapshot;
            }

            var list = new List<ClientSession>(_slots.Count);
            foreach (var s in _slots.Values)
            {
                if (s != null)
                    list.Add(s);
            }

            _broadcastSnapshot = list.ToArray();
            _broadcastDirty = false;
            Console.WriteLine("[GetBroadcastSnapshot] snapshot : Re");

            return _broadcastSnapshot;
        }
    }

    #endregion

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

        // 1) 맵 생성 (실제 구현에 맞게 교체)
        // TODO: MapFactory는 네 프로젝트 스타일에 맞게 구현
        //_map = MapFactory.CreatePrototypeMap();//CreatePrototype(MapId); // 예시: MapId에 따라 다른 맵 생성
        _map = MapDatabase.Get("Map");


        // 3) 리듬 설정
        _rhythmConfig = new RhythmConfig
        {
            Bpm = 60,
            BaseBeatDivision = 1/*4 * 4*/,  // 16분음표 기준
            ActionWindowMs = 100,     // +-80ms 판정 윈도우
            MaxBeatLookAhead = 2
        };

        long songStart = AppRef.ServerTimeMs() /*+ 500*/; // 0.5초 뒤부터 Beat 시작

        var time = new ServerTimeAdapter();
        _rhythm = new RhythmSystem(time, _rhythmConfig, songStart);

        // 4) GameSession 구성
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
        var players = BuildPlayerEntities();
        var monsters = BuildMonsterEntitiesForPrototype();

        _session.InitGame(players, monsters);

        foreach (var s in GetBroadcastSnapshot())
        {

            _session.SendInitPacketToPlayer(s);
        }

        var now = AppRef.ServerTimeMs();
        Console.WriteLine($"[BeatSync] now={now} songStart={songStart} diff={songStart - now}");


        // 6) BeatSync 
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
    private List<MapEntity> BuildPlayerEntities()
    {
        var players = new List<MapEntity>();

        //  slot별로 하나씩 player 생성
        // (프로토: Player EntityId == Slot 사용 가능)
        lock (_lock)
        {
            foreach (var kv in _slots)
            {
                int slot = kv.Key;
                ClientSession s = kv.Value;
                if (s == null || string.IsNullOrEmpty(s.Uid)) continue;

                // 스폰 위치는 slot 기준으로 적당히 배치 TMP
                var spawnSet = _map.GetSpawnPoint(slot);//GetPrototypeSpawnForSlot(slot);
                               
                var spawn = new GridPos(spawnSet.Item1, spawnSet.Item2);
                if(spawn.X <0 || spawn.Y <0) Console.WriteLine($"[BuildPlayerEntites] Player spawnPoint Set Err!"); ;
                
                var e = new MapEntity(
                    id: slot, //  프로토에서는 slot==entityId
                    type: EntityType.Player,
                    initialPos: spawn
                );

                e.SetState("HP", 100);
                e.SetState("Slot", slot);
                e.SetState("OwnerSlot", slot);
                e.SetState("Uid", s.Uid!);

                players.Add(e);
            }
        }

        return players;
    }

    private List<MapEntity> BuildMonsterEntitiesForPrototype()
    {
        var monsters = new List<MapEntity>();

        // 프로토: 몬스터 2마리 예시
        monsters.Add(CreateMonster(x: 10, y: 16, hp: 50));
        // monsters.Add(CreateMonster(x: 10, y: 6, hp: 60));

        return monsters;
    }

    private int _nextMonsterId = 100; // 몬스터는 100 ~ 999
    //private int NextMonsterId() => _nextMonsterId++;

    private MapEntity CreateMonster(int x, int y, int hp)
    {
        var id = Interlocked.Increment(ref _nextMonsterId);
        var m = new MapEntity(id, EntityType.Monster, new GridPos(x, y));
        m.SetState("HP", hp);
        m.SetState("OwnerSlot", -1);
        return m;
    }


    // ====== Packet Serialization ======

    readonly System.Collections.Concurrent.ConcurrentQueue<Action> _q = new();
    int _pumping = 0;
    private void Enqueue(Action a)
    {
        _q.Enqueue(a);
        // Pump(); -> Work Thread에 이양
    }
    private void PumpQueuedActions()
    {
        if (System.Threading.Interlocked.Exchange(ref _pumping, 1) == 1) return;
        try { while (_q.TryDequeue(out var a)) a(); }
        finally { _pumping = 0; }
    }
    private bool IsRunnableFor(ClientSession s)
    {
        if (_phase != RoomPhase.Running) return false;
        // 과한가?
        lock (_lock) return _slots.Values.Contains(s);
    }
    private long _nextBeatSyncAtMs = 0;
    private const int BeatSyncIntervalMs = 5000; // 0.5초
    public void Update()
    {
        if (_phase != RoomPhase.Running) return;


        // 1) 큐 처리 (네트워크에서 들어온 작업들)
        PumpQueuedActions();

        // 2) 리듬 진행
        _rhythm?.Update();
        //if (_rhythm != null)
        //{
        //    long now = AppRef.ServerTimeMs();
        //    if (now >= _nextBeatSyncAtMs)
        //    {
        //        _nextBeatSyncAtMs = now + BeatSyncIntervalMs;

        //        // 룸 전체에 같은 beat 기준 전달
        //        var sync = _rhythm.CreateSyncPacket();
        //        Broadcast(sync); // 또는 Broadcast(sync.Write())
        //    }
        //}
        // 3) 인게임 세션 (AI, 상태 갱신)
        _session?.Update();
    }
    #region 리듬 게임용 입력 패킷 라우팅 (CS_ActionRequest)

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

            int slot = s.Slot;
            if (slot < 0)
            {
                s.Send(new SC_Warn { code = 2003, msg = "UNKNOWN_SLOT" }.Write());
                return;
            }
            //Console.WriteLine($" Session slot = {s.Slot} || ActorId = {p.ActorId}, ActorKind = {p.ActionKind}");
            // 간단 버전: Slot == ActorId 라고 가정
            // 필요하면 GameSession에 slot->actorId 매핑 테이블을 만들어서 ResolveActorIdBySlot(slot)로 가져오자.
            //int actorId = slot/* + 1*/; // 예시: slot 0 -> actorId 1, slot 1 → actorId 2 등

            _session.OnClientActionPacketBySlot(slot, p);
        });

    #endregion



}

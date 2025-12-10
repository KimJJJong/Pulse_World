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
using GameServer.InGame.Manager.Map;
using GameServer.InGame.Manager.Map.Interface;
using GameServer.InGame.System.Rhythm;
#endregion

public sealed class GameRoom : IGameBroadcaster
{
    readonly object _lock = new();
    readonly Dictionary<char, ClientSession> _slots = new() { ['A'] = null, ['B'] = null };
    readonly HashSet<string> _loaded = new();

    public string MatchId { get; }
    public int MapId { get; private set; } = 0;
    public int Seed { get; private set; } = 0;

    enum RoomPhase { Waiting, Loading, Countdown, Running, Ended }
    RoomPhase _phase = RoomPhase.Waiting;

    //GameLogicManager _logic;

    private readonly ILogger _logger;
    #region 리듬 게임 관련 필드

    // 리듬 + 인게임 세션
    private GameSession _session;
    private RhythmSystem _rhythm;
    private RhythmConfig _rhythmConfig;
    private Map2D _map;

    private int _nextEntityId = 1; // MapEntity Id 발급용

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
    }

    public bool Bind(char side, ClientSession s)
    {
        lock (_lock)
        {
            if (!_slots.ContainsKey(side)) return false;
            _slots[side] = s;
            return true;
        }
    }

    public bool MarkLoadedAsync(ClientSession s)
    {
        lock (_lock)
        {
            _loaded.Add(s.Uid!);
            var a = _slots['A']; var b = _slots['B'];
            if (a?.Uid == null || b?.Uid == null) return false;
            return _loaded.Contains(a.Uid) && _loaded.Contains(b.Uid);
        }
    }

    public IEnumerable<(string uid, char side, bool loaded)> GetPlayersSnapshot()
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
        //  전송 시 락 홀드 최소화
        ClientSession a, b;
        lock (_lock) { a = _slots['A']; b = _slots['B']; }
        a?.Send(payload);
        b?.Send(payload);
    }
    public void Broadcast(IPacket pkt) => Broadcast(pkt.Write());
    private void SendTo(ClientSession s, ArraySegment<byte> payload) => s?.Send(payload);
    public void SendTo(ClientSession s, IPacket pkt) => SendTo(s, pkt.Write());
    public void SendToSlot(int slot, IPacket pkt)
    {
        var side = SideSlot.ToSide(slot);
        ClientSession target;
        lock (_lock) _slots.TryGetValue(side, out target);
        target?.Send(pkt.Write());
    }
    public ClientSession GetSessionBySlot(int slot)
    {
        var side = SideSlot.ToSide(slot);
        lock (_lock) return _slots.TryGetValue(side, out var s) ? s : null;
    }
    public int GetPlayerSlot(ClientSession s)
    {
        lock (_lock)
        {
            foreach (var kv in _slots)
                if (ReferenceEquals(kv.Value, s))
                    return SideSlot.ToSlot(kv.Key);
        }
        return -1;
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
        _map = MapFactory.CreatePrototypeMap();//CreatePrototype(MapId); // 예시: MapId에 따라 다른 맵 생성

        // 2) 플레이어 엔티티 생성 (슬롯 A/B 기준)
        var players = new List<MapEntity>();
        var monsters = new List<MapEntity>(); // PVE용 몬스터 있으면 여기 채움

        ClientSession a, b;
        lock (_lock)
        {
            _slots.TryGetValue('A', out a);
            _slots.TryGetValue('B', out b);
        }

        if (a != null)
            players.Add(CreatePlayerEntity(a, slot: 0, x: 5, y: 0));

        if (b != null)
            players.Add(CreatePlayerEntity(b, slot: 1, x: 7, y: 1));

        // 3) 리듬 설정
        _rhythmConfig = new RhythmConfig
        {
            Bpm = 120.0,
            BaseBeatDivision = 4 * 4,  // 16분음표 기준
            ActionWindowMs = 80.0,     // +-80ms 판정 윈도우
            MaxBeatLookAhead = 2
        };

        long songStart = AppRef.ServerTimeMs() + 500; // 0.5초 뒤부터 Beat 시작

        var time = new ServerTimeAdapter();
        _rhythm = new RhythmSystem(time, _rhythmConfig, songStart);

        // 4) GameSession 구성
        _session = new GameSession(
            sessionId: 0,                 // MatchId 해시 등으로 대체 가능
            time: time,
            broadcaster: this,
            rhythm: _rhythm,
            rhythmConfig: _rhythmConfig,
            map: _map
        );

        _rhythm.OnBeat += _session.OnBeat;

        // 5) 엔티티 스폰 + 초기 패킷 송신 (SC_InitGame)
        _session.InitGame(players, monsters);

        _logger.LogInformation("GameRoom {MatchId} started rhythm gameplay", MatchId);

    }

    private MapEntity CreatePlayerEntity(ClientSession s, int slot, int x, int y)
    {

        MapEntity e = new MapEntity(_nextEntityId, EntityType.Player, new GridPos(x, y)); 


        e.SetState("HP", 100);
        e.SetState("Slot", slot);
        e.SetState("Uid", s.Uid!);

        return e;
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
        if (_phase != RoomPhase.Running ) return false;
        // 과한가?
        lock (_lock) return _slots.Values.Contains(s);
    }

    public void Update()
    {
        if(_phase != RoomPhase.Running ) return;


        // 1) 큐 처리 (네트워크에서 들어온 작업들)
        PumpQueuedActions();

        // 2) 리듬 진행
        _rhythm?.Update();

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

            int slot = GetPlayerSlot(s);
            if (slot < 0)
            {
                s.Send(new SC_Warn { code = 2003, msg = "UNKNOWN_SLOT" }.Write());
                return;
            }

            // 간단 버전: Slot == ActorId 라고 가정
            // 필요하면 GameSession에 slot->actorId 매핑 테이블을 만들어서 ResolveActorIdBySlot(slot)로 가져오자.
            //int actorId = slot/* + 1*/; // 예시: slot 0 -> actorId 1, slot 1 → actorId 2 등

            _session.OnClientActionPacketBySlot(slot, p);
        });

    #endregion
    


}

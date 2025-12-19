using GameServer.Content.Map;
using GameServer.Content.Map.Interface;
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using System;
using System.Collections.Generic;

public sealed class GameSession
{
    public int SessionId { get; }

    // 월드 / 맵
    public IGameWorld World { get; }
    public MapWorld2D World2D { get; }
    private readonly Map2D _map;

    // 액션 처리 및 Beat 스케줄링
    public BeatActionManager BeatActions { get; }
    private readonly TelegraphScheduler _telegraph;
    private readonly PatternRunner _patternRunner;
    private readonly FrozenAttackRegistry _frozen = new();


    // 외부 시스템
    private readonly RhythmSystem _rhythm;
    private readonly IServerTime _time;
    private readonly IGameBroadcaster _broadcaster;
    private readonly RhythmConfig _rhythmConfig;

    // 룸 구성 요소
    private readonly List<MapEntity> _players = new();
    private readonly List<MapEntity> _monsters = new();

    // Content
    private readonly Dictionary<int, int> _slotToActorId = new(); // slot -> actorId
    public int GetActorIdBySlot(int slot)
        => _slotToActorId.TryGetValue(slot, out var id) ? id : -1;

    private readonly MonsterAIController _monsterAI;

    public GameSession(
        int sessionId,
        IServerTime time,
        IGameBroadcaster broadcaster,
        RhythmSystem rhythm,
        RhythmConfig rhythmConfig,
        Map2D map) 
    {
        SessionId = sessionId;

        _time = time;
        _broadcaster = broadcaster;
        _rhythm = rhythm;
        _rhythmConfig = rhythmConfig;
        _map = map;


        // 월드 구성
        World2D = new MapWorld2D(map);
        World = World2D;

        // BeatActionManager
        BeatActions = new BeatActionManager(
            time,
            broadcaster,
            rhythm,
            World,
            frozenAttackRegistry :_frozen,
            actionWindowMs: _rhythmConfig.ActionWindowMs,
            maxBeatLookAhead: _rhythmConfig.MaxBeatLookAhead
        );

        //  반드시 telegraph 먼저 생성
        _telegraph = new TelegraphScheduler(broadcaster);

        //  그 다음 runner 생성 (telegraph 필요)
        _patternRunner = new PatternRunner(World, BeatActions, _telegraph, ContentStore.Patterns ,_frozen);

        //  MonsterAI는 runner만 받는 구조
        _monsterAI = new MonsterAIController(_patternRunner);

        // _rhythm.OnBeat += OnBeat;
    }

    // =====================================================
    //  초기화
    // =====================================================

    public void InitGame(IEnumerable<MapEntity> players, IEnumerable<MapEntity> monsters)
    {
        _players.Clear();
        _monsters.Clear();
        _slotToActorId.Clear();

        foreach (var p in players)
        {
            if (!World2D.TrySpawn(p, p.Position))
            {
                Console.WriteLine($"[InitGame] Player spawn failed for slot={p.GetState<int>("Slot")}");
                continue;
            }

            _players.Add(p);

            int slot = p.GetState<int>("Slot");
            _slotToActorId[slot] = p.Id;

            Console.WriteLine($"[InitGame] Player spawned: slot={slot}, actorId={p.Id}");
        }

        foreach (var m in monsters)
        {
            if (World2D.TrySpawn(m, m.Position))
            {
                _monsters.Add(m);

                //  가능하면 MonsterType을 실제 데이터에서 넣어라
                // 지금은 임시로 "Default"        TODO
                _monsterAI.RegisterMonster(m, "Default");
            }
        }
        Console.WriteLine($"[InitGmae] End");
    }

    private SC_InitGame BuildInitPacketForPlayer(int playerSlot)
    {
        Console.WriteLine($"[BuildInitPacketForPlayer] In");
        SC_InitGame packet = new SC_InitGame();

        packet.MapWidth = _map.Width;
        packet.MapHeight = _map.Height;

        packet.MapName ="Map";          ///TMP 파라미터로 빼기
        Console.WriteLine($"[BuildInitPacketForPlayer] mapSet {_map.Width} x {_map.Height}");
        
        // Tile Set을 보내는 부분 del : size over

        packet.playerActorIdss.Clear();
        foreach (MapEntity p in _players)
        {
            Console.WriteLine($"[BuildInitPacketForPlayer] player.id = {p.Id}");

            var pa = new SC_InitGame.PlayerActorIds { ActorId = p.Id };
            packet.playerActorIdss.Add(pa);
        }

        int myActorId = GetActorIdBySlot(playerSlot);
        packet.MyActorId = myActorId;

        Console.WriteLine($"[BuildInitPacketForPlayer] myActorId = {myActorId}");


        packet.spawnEntitiess.Clear();

        foreach (MapEntity p in _players)
        {
            var s = new SC_InitGame.SpawnEntities
            {
                EntityId = p.Id,
                EntityType = (int)p.Type,
                OwnerSlot = p.GetState<int>("Slot"),
                X = p.Position.X,
                Y = p.Position.Y,
                Hp = p.GetState<int>("HP")
            };
            packet.spawnEntitiess.Add(s);
            Console.WriteLine($"[BuildInitPacketForPlayer] Player : id = {p.Id} ||Pos = {p.Position} || Type = {p.Type}");

        }

        foreach (MapEntity m in _monsters)
        {
            var s = new SC_InitGame.SpawnEntities
            {
                EntityId = m.Id,
                EntityType = (int)m.Type,
                OwnerSlot = -1,
                X = m.Position.X,
                Y = m.Position.Y,
                Hp = m.GetState<int>("HP")
            };
            packet.spawnEntitiess.Add(s);
            Console.WriteLine($"[BuildInitPacketForPlayer] Monster : id = {m.Id} ||Pos = {m.Position} || Type = {m.Type}");

        }
        Console.WriteLine($"[BuildInitPacketForPlayer] Out");

        return packet;
    }

    /// <summary>GameRoom에서 각 유저 Session에게 호출되는 함수</summary>
    public void SendInitPacketToPlayer(ClientSession s)
    {
        Console.WriteLine($"[ SendInitPacketToPlayer ] In");
        int slot = s.Slot;

        int actorId = GetActorIdBySlot(slot);
        if (actorId < 0)
        {
            Console.WriteLine($"[InitGame] slot not mapped yet. slot={slot}");
            return;
        }

        if (!World2D.ContainsEntity(actorId))
        {
            Console.WriteLine($"[InitGame] actorId not spawned. slot={slot}, actorId={actorId}");
            return;
        }
        SC_InitGame pkt = BuildInitPacketForPlayer(slot);

        s.Send(pkt.Write());

        Console.WriteLine($"[InitGame] Sent SC_InitGame to slot={slot}, actorId={pkt.MyActorId} || Out");
    }

    // =====================================================
    // 클라이언트 입력 처리
    // =====================================================
    public void OnClientActionPacketBySlot(int slot, CS_ActionRequest req)
    {
        int actorId = GetActorIdBySlot(slot);
        if (actorId < 0)
        {
            Console.WriteLine($"[ActionReq] Invalid slot={slot}");
            return;
        }

        BeatActions.OnClientActionRequest(actorId, req);
    }

    // =====================================================
    // Beat 도래 시 호출
    // =====================================================
    public void OnBeat(long beatIndex)
    {
        _monsterAI.UpdateAI(beatIndex, _monsters, _players);

        //  텔레그래프 먼저
        _telegraph.OnBeat(beatIndex);

        //  그 다음 액션 실행
        BeatActions.OnBeat(beatIndex);
    }

    public void Update()
    {
        // 필요하면 Tick 기반 처리
    }
}

using GameServer.Content.Map;
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using System;
using System.Collections.Generic;

public sealed class GameSession : SessionBase
{
    // 액션 처리 및 Beat 스케줄링
    public BeatActionManager BeatActions { get; }
    private readonly TelegraphScheduler _telegraph;
    private readonly PatternRunner _patternRunner;
    private readonly FrozenAttackRegistry _frozen = new();

    // 외부 시스템 (전투 전용)
    private readonly RhythmSystem _rhythm;
    private readonly RhythmConfig _rhythmConfig;

    // 전투용 엔티티
    private readonly List<MapEntity> _monsters = new();
    private readonly MonsterAIController _monsterAI;

    public GameSession(
        int sessionId,
        IServerTime time,
        IGameBroadcaster broadcaster,
        RhythmSystem rhythm,
        RhythmConfig rhythmConfig,
        Map2D map)
        : base(sessionId, time, broadcaster, map)
    {
        _rhythm = rhythm;
        _rhythmConfig = rhythmConfig;

        // BeatActionManager (전투 판정/예약)
        BeatActions = new BeatActionManager(
            time,
            broadcaster,
            rhythm,
            World,
            frozenAttackRegistry: _frozen,
            actionWindowMs: _rhythmConfig.ActionWindowMs,
            maxBeatLookAhead: _rhythmConfig.MaxBeatLookAhead
        );

        // 반드시 telegraph 먼저 생성
        _telegraph = new TelegraphScheduler(broadcaster);

        // runner 생성
        _patternRunner = new PatternRunner(World, BeatActions, _telegraph, ContentStore.Patterns, _frozen, map);

        // MonsterAI는 runner만 받는 구조
        _monsterAI = new MonsterAIController(_patternRunner);
    }

    // =====================================================
    //  세션별 Cleanup: 전투 스케줄러들 purge + despawn
    // =====================================================
    protected override void CleanupActor(int actorId)
    {
        BeatActions.CancelActor(actorId);      // scheduler purge
        _frozen.RemoveByActor(actorId);        // frozen purge
        _telegraph.RemoveByCaster(actorId);    // telegraph purge
        base.CleanupActor(actorId);            // World2D.Despawn
    }

    // =====================================================
    //  초기화
    // =====================================================
    public void InitGame(IEnumerable<MapEntity> players, IEnumerable<MapEntity> monsters)
    {
        InitPlayers(players);

        _monsters.Clear();

        foreach (var m in monsters)
        {
            if (World2D.TrySpawn(m, m.Position))
            {
                _monsters.Add(m);

                // TODO: MonsterType 실제 데이터로 넣기
                _monsterAI.RegisterMonster(m, "Default");
            }
        }

        Console.WriteLine($"[InitGame] End");
    }

    private SC_InitGame BuildInitPacketForPlayer(int playerSlot)
    {
        Console.WriteLine($"[BuildInitPacketForPlayer] In");
        SC_InitGame packet = new SC_InitGame();

        packet.ActionWindowMs = _rhythmConfig.ActionWindowMs;

        packet.MapWidth = _map.Width;
        packet.MapHeight = _map.Height;

        packet.MapName = "Map"; // TODO: 파라미터로 빼기
        Console.WriteLine($"[BuildInitPacketForPlayer] mapSet {_map.Width} x {_map.Height}");

        packet.playerActorIdss.Clear();
        foreach (MapEntity p in _players)
        {
            Console.WriteLine($"[BuildInitPacketForPlayer] player.id = {p.Id}");
            packet.playerActorIdss.Add(new SC_InitGame.PlayerActorIds { ActorId = p.Id });
        }

        int myActorId = GetActorIdBySlot(playerSlot);
        packet.MyActorId = myActorId;
        Console.WriteLine($"[BuildInitPacketForPlayer] myActorId = {myActorId}");

        packet.spawnEntitiess.Clear();

        foreach (MapEntity p in _players)
        {
            packet.spawnEntitiess.Add(new SC_InitGame.SpawnEntities
            {
                EntityId = p.Id,
                EntityType = (int)p.Type,
                OwnerSlot = p.GetState<int>("Slot"),
                X = p.Position.X,
                Y = p.Position.Y,
                Hp = p.GetState<int>("HP")
            });

            Console.WriteLine($"[BuildInitPacketForPlayer] Player : id={p.Id} Pos={p.Position} Type={p.Type}");
        }

        foreach (MapEntity m in _monsters)
        {
            packet.spawnEntitiess.Add(new SC_InitGame.SpawnEntities
            {
                EntityId = m.Id,
                EntityType = (int)m.Type,
                OwnerSlot = -1,
                X = m.Position.X,
                Y = m.Position.Y,
                Hp = m.GetState<int>("HP")
            });

            Console.WriteLine($"[BuildInitPacketForPlayer] Monster : id={m.Id} Pos={m.Position} Type={m.Type}");
        }

        Console.WriteLine($"[BuildInitPacketForPlayer] Out");
        return packet;
    }

    /// <summary>GameRoom에서 각 유저 Session에게 호출되는 함수</summary>
    public override void SendInitPacketToPlayer(ClientSession s)
    {
        Console.WriteLine($"[SendInitPacketToPlayer] In");
        int slot = s.Slot;

        int actorId = GetActorIdBySlot(slot);
        if (actorId < 0)
        {
            Console.WriteLine($"[SendInitPacketToPlayer] slot not mapped yet. slot={slot}");
            return;
        }

        if (!World2D.ContainsEntity(actorId))
        {
            Console.WriteLine($"[SendInitPacketToPlayer] actorId not spawned. slot={slot}, actorId={actorId}");
            return;
        }

        SC_InitGame pkt = BuildInitPacketForPlayer(slot);
        s.Send(pkt.Write());

        Console.WriteLine($"[SendInitPacketToPlayer] Sent SC_InitGame to slot={slot}, actorId={pkt.MyActorId} || Out");
    }

    // =====================================================
    // 클라이언트 입력 처리 (전투)
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

    public void OnClientCalibPacketBySlot(int slot, CS_CalibHit req)
    {
        int actorId = GetActorIdBySlot(slot);
        if (actorId < 0)
        {
            Console.WriteLine($"[CalibReq] Invalid slot={slot}");
            return;
        }

        BeatActions.OnClientCalibRequest(actorId, req);
    }

    // =====================================================
    // Beat 도래 시 호출
    // =====================================================
    public void OnBeat(long beatIndex)
    {
        _monsterAI.UpdateAI(beatIndex, _monsters, _players);

        // 텔레그래프 먼저
        _telegraph.OnBeat(beatIndex);

        // 그 다음 액션 실행
        BeatActions.OnBeat(beatIndex);
    }

    public override void Update()
    {
        // 필요하면 Tick 기반 처리
    }
}

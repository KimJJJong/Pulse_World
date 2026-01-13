// ===============================
// GameSession.cs  
// ===============================
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

        BeatActions = new BeatActionManager(
            time,
            broadcaster,
            rhythm,
            World,
            frozenAttackRegistry: _frozen,
            actionWindowMs: _rhythmConfig.ActionWindowMs,
            maxBeatLookAhead: _rhythmConfig.MaxBeatLookAhead
        );

        _telegraph = new TelegraphScheduler(broadcaster);
        _patternRunner = new PatternRunner(World, BeatActions, _telegraph, ContentStore.Patterns, _frozen, map);
        _monsterAI = new MonsterAIController(_patternRunner);
    }

    // =====================================================
    //  세션별 Cleanup
    // =====================================================
    protected override void CleanupActor(int actorId)
    {
        BeatActions.CancelActor(actorId);
        _frozen.RemoveByActor(actorId);
        _telegraph.RemoveByCaster(actorId);
        base.CleanupActor(actorId);
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
                _monsterAI.RegisterMonster(m, "Default");
            }
        }

        Console.WriteLine($"[InitGame] End");
    }

    // ===============================
    // Init Packet (actorId 통일)
    // ===============================
    private SC_InitGame BuildInitPacketForPlayer(int myActorId)
    {
        SC_InitGame packet = new SC_InitGame();

        packet.ActionWindowMs = _rhythmConfig.ActionWindowMs;

        packet.MapWidth = _map.Width;
        packet.MapHeight = _map.Height;
        packet.MapName = "Map"; // TODO

        packet.playerActorIdss.Clear();
        foreach (var p in _players)
            packet.playerActorIdss.Add(new SC_InitGame.PlayerActorIds { ActorId = p.Id });

        packet.MyActorId = myActorId;

        packet.spawnEntitiess.Clear();

        foreach (var p in _players)
        {
            packet.spawnEntitiess.Add(new SC_InitGame.SpawnEntities
            {
                EntityId = p.Id,
                EntityType = (int)p.Type,

                // ⚠️ 임시 호환: OwnerSlot 필드에 "OwnerActorId" 의미로 actorId를 넣는다.
                OwnerSlot = p.Id,

                X = p.Position.X,
                Y = p.Position.Y,
                Hp = p.GetState<int>("HP")
            });
        }

        foreach (var m in _monsters)
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
        }

        return packet;
    }

    /// <summary>GameRoom에서 각 유저 Session에게 호출</summary>
    public override void SendInitPacketToPlayer(ClientSession s)
    {
        int myActorId = s.ActorId;
        if (myActorId < 0)
        {
            Console.WriteLine("[SendInitPacketToPlayer] actorId not assigned yet.");
            return;
        }

        if (!_actorIds.Contains(myActorId))
        {
            Console.WriteLine($"[SendInitPacketToPlayer] actorId not registered in session. actorId={myActorId}");
            return;
        }

        if (!World2D.ContainsEntity(myActorId))
        {
            Console.WriteLine($"[SendInitPacketToPlayer] actorId not spawned. actorId={myActorId}");
            return;
        }

        var pkt = BuildInitPacketForPlayer(myActorId);
        s.Send(pkt.Write());

        Console.WriteLine($"[SendInitPacketToPlayer] Sent SC_InitGame myActorId={pkt.MyActorId}");
    }

    // =====================================================
    // 클라이언트 입력 처리 (actorId 통일)
    // =====================================================
    public void OnClientActionPacketByActorId(int actorId, CS_ActionRequest req)
    {
        if (actorId < 0) return;
        BeatActions.OnClientActionRequest(actorId, req);
    }

    public void OnClientCalibPacketByActorId(int actorId, CS_CalibHit req)
    {
        if (actorId < 0) return;
        BeatActions.OnClientCalibRequest(actorId, req);
    }

    // =====================================================
    // Beat 도래 시 호출
    // =====================================================
    public void OnBeat(long beatIndex)
    {
        _monsterAI.UpdateAI(beatIndex, _monsters, _players);
        _telegraph.OnBeat(beatIndex);
        BeatActions.OnBeat(beatIndex);
    }

    public override void Update()
    {
        // 필요하면 Tick 기반 처리
    }
}

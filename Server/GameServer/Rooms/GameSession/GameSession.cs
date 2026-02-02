// ===============================
// GameSession.cs  
// ===============================
using GameServer.Content.Map;
using GameServer.InGame.Director.Core;
using GameServer.InGame.Director.Data;
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using System;
using System.Collections.Generic;
using Util;

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
    
    // [Director]
    public GameDirector Director { get; private set; }

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

        // [Director] Hook Death Event
        World2D.OnEntityDead += OnEntityDeadHandler;

        Director = new GameDirector(this);
    }

    // =====================================================
    //  세션별 Cleanup
    // =====================================================
    protected override void CleanupActor(int actorId)
    {
        BeatActions.CancelActor(actorId);
        _frozen.RemoveByActor(actorId);
        _telegraph.RemoveByCaster(actorId);

        // Event Unsub? 
        // SessionBase calls CleanupActor often, but OnEntityDead is on MapWorld2D which is per session.
        // It's safer to unsubscribe on Session Destroy, but currently we don't have explicit SessionDestroy hook in SessionBase except Cleanup.
        // Since World2D is owned by Session, it's fine.

        base.CleanupActor(actorId);
    }

    private void OnEntityDeadHandler(int actorId)
    {
        // MonsterId or GroupId logic needed?
        // Director expects TargetId to be GroupId for "MonsterAllDead".
        // BUT NotifyEvent(TargetId) logic in GameDirector was:
        // "if (!MonsterGroupDeadCounts.ContainsKey(context.TargetId)) ... count++"
        
        // Problem: context.TargetId is just unique ActorId here.
        // Director needs to know the GROUP ID of this actor.
        
        var monster = _monsters.Find(x => x.Id == actorId);
        if (monster != null)
        {
            try
            {
                int groupId = monster.GetState<int>("GroupId");
                Director.NotifyEvent(new GameEventContext 
                { 
                    Type = EventType.Dead, 
                    TargetId = groupId, // Pass GroupId as TargetId for counting
                    SourceActorId = actorId,
                    TimeMs = AppRef.ServerTimeMs() 
                });
                Console.WriteLine($"[GameSession] Reported Dead. Actor:{actorId} Group:{groupId}");
            }
            catch(Exception ex) 
            {
                Console.WriteLine($"[OnEntityDeadHandler] Failed to report death: {ex.Message}");
            }
        }
    }

    // =====================================================
    //  초기화
    // =====================================================
    // =====================================================
    //  초기화 (Changed Signature)
    // =====================================================
    public void InitGame(IEnumerable<MapEntity> players, StageScenario scenario)
    {
        InitPlayers(players);

        _monsters.Clear();

        Director.LoadScenario(scenario);
        
        Director.NotifyEvent(new GameEventContext { Type = EventType.GameStart, TimeMs = AppRef.ServerTimeMs() });

        // BeatSync (Force Sync at Start)
        var sync = new SC_BeatSync
        {
            ServerSendTimeMs = AppRef.ServerTimeMs(),
            ClientSendTimeMs = 0, // Server initiated
            SongStartServerTimeMs = _rhythm.SongStartServerTimeMs,
            Bpm = _rhythmConfig.Bpm,
            BaseBeatDivision = _rhythmConfig.BaseBeatDivision,
            BeatIndex = _rhythm.GetCurrentBeatIndex(AppRef.ServerTimeMs())
        };
        _broadcaster.Broadcast(sync);

        Console.WriteLine($"[InitGame] End (Director Loaded). Sent BeatSync (Start:{sync.SongStartServerTimeMs})");
    }
    
    // Director 전용 몬스터 소환 함수
    public void SpawnMonsterInternal(int monsterId, int x, int y, int groupId, string aiKey)
    {
        // 간단한 ID 생성 (실제론 GameManager 등에서 유니크 ID 발급 필요할 수 있음)
        // 여기선 임시로 Random or Hash 사용, 혹은 GameRoom에서 관리하던 NextId 가져와야 함.
        // 하지만 SessionBase는 NextActorId 관리를 안 함. 
        // 프로토타입: 현재 Tick + Hash 조합
        int newId = (int)(Environment.TickCount64 & 0x7FFFFFFF) + x * 1000 + y; 

        var m = new MapEntity(newId, EntityType.Monster, new GridPos(x, y));
        m.SetState("HP", 50); // TODO: MonsterTable 조회
        m.SetState("GroupId", groupId);
        
        if (World2D.TrySpawn(m, m.Position))
        {
            _monsters.Add(m);
            _monsterAI.RegisterMonster(m, aiKey);
            Console.WriteLine($"[GameSession] Spawned Monster {monsterId} at ({x},{y}). AI={aiKey}");

            // Broadcast Spawn
            var pkt = new SC_EntitySpawn
            {
                BeatIndex = 0, // dynamic spawn
                EntityId = m.Id,
                EntityType = (int)m.Type,
                X = m.Position.X,
                Y = m.Position.Y,
                Hp = m.GetState<int>("HP")
            };
            _broadcaster.Broadcast(pkt);
        }
    }

    // ===============================
    // Init Packet (actorId 통일)
    // ===============================
    private SC_InitMap BuildInitPacketForPlayer(int myActorId)
    {
        SC_InitMap packet = new SC_InitMap();

        packet.Mode = 2;        // TODO : int value to enum - Game, Town

        packet.ActionWindowMs = _rhythmConfig.ActionWindowMs;
        packet.SongId = "TestSong";
        packet.Bpm = _rhythmConfig.Bpm;
        packet.BaseBeatDivision = _rhythmConfig.BaseBeatDivision;
        packet.SongStartServerTime = _rhythm.SongStartServerTimeMs;

        packet.MapWidth = _map.Width;
        packet.MapHeight = _map.Height;
        packet.MapId = _map.MapId;

           packet.playerss.Clear();
        foreach (var p in _players)
            packet.playerss.Add(new SC_InitMap.Players { ActorId = p.Id, Name="Test", Uid="RealNeedit?" });

        packet.MyActorId = myActorId;

        packet.entitiess.Clear();

        foreach (var p in _players)
        {
            packet.entitiess.Add(new SC_InitMap.Entities
            {
                EntityId = p.Id,
                EntityType = (int)p.Type,

                //  OwnerSlot 같은 slot 개념 제거  -> 그럼 OwnActor?
                // OwnerSlot = ... (없애거나 0 고정)

                X = p.Position.X,
                Y = p.Position.Y,
                Hp = p.GetState<int>("HP")
            });
        }

        foreach (var m in _monsters)
        {
            packet.entitiess.Add(new SC_InitMap.Entities
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

        Console.WriteLine($"[SendInitPacketToPlayer] Sent SC_InitMap myActorId={pkt.MyActorId}");
    }

    // =====================================================
    // 클라이언트 입력 처리 (actorId 통일)
    // =====================================================
    public void OnClientActionPacketByActorId(int actorId, CS_ActionRequest req)
    {
        if (actorId < 0) return;
        BeatActions.OnClientActionRequest(actorId, req);

        // Move Event Hook
        // (ActionRequest 안에 Move인지 Attack인지 구분 필요. 여기선 일단 생략하거나 ActionManager에서 Callback 받아야 함)
        // 일단 예시로 Move라고 가정 시:
        // var p = GetActor(actorId);
        // Director.NotifyEvent(new GameEventContext { Type = EventType.Move, SourceActorId = actorId, X = p.X, Y = p.Y });
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
        
        Director.NotifyEvent(new GameEventContext { Type = EventType.Beat, TimeMs = AppRef.ServerTimeMs() });
    }

    public override void Update()
    {
        // 필요하면 Tick 기반 처리
        Director.Update(AppRef.ServerTimeMs());

    }

    public void BroadcastReturnToTown()
    {
        Console.WriteLine("[GameSession] Broadcast ReturnToTown");
        _broadcaster.Broadcast(new SC_ReturnToTown());
    }
}

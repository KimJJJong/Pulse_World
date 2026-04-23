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
using GameServer.Content.Game.Entity;
using System.Threading.Tasks;

public sealed class GameSession : SessionBase
{
    // 액션 처리 및 Beat 스케줄링
    public BeatActionManager BeatActions { get; }
    private readonly TelegraphScheduler _telegraph;
    private readonly PatternRunner _patternRunner;
    private readonly FrozenAttackRegistry _frozen = new();

    private readonly RhythmSystem _rhythm;
    private readonly RhythmConfig _rhythmConfig;
    private readonly DynamicRhythmManager _rhythmManager;

    // 전투용 엔티티
    private readonly List<MapEntity> _monsters = new();
    private readonly MonsterAIController _monsterAI;
    private readonly EntityIdGenerator _idGen = new EntityIdGenerator(); // [NEW] ID Generator
    
    // [Director]
    public GameDirector Director { get; private set; }

    public GameSession(
        int sessionId,
        IServerTime time,
        IGameBroadcaster broadcaster,
        RhythmSystem rhythm,
        RhythmConfig rhythmConfig,
        DynamicRhythmManager rhythmManager,
        Map2D map)
        : base(sessionId, time, broadcaster, map)
    {
        _rhythm = rhythm;
        _rhythmConfig = rhythmConfig;
        _rhythmManager = rhythmManager;

        // Load Entity Data
        EntityDataManager.Instance.Load();

        _telegraph = new TelegraphScheduler(broadcaster);

        BeatActions = new BeatActionManager(
            time,
            broadcaster,
            rhythm,
            World,
            frozenAttackRegistry: _frozen,
            telegraphScheduler: _telegraph,
            actionWindowMs: _rhythmConfig.ActionWindowMs,
            maxBeatLookAhead: _rhythmConfig.MaxBeatLookAhead
        );
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
        Console.WriteLine($"[InitGame] End (Director Loaded). SongStart={_rhythm.SongStartServerTimeMs}");
    }
    
    // Director 전용 엔티티(몬스터/오브젝트) 소환 함수
    public void SpawnEntityInternal(int entityId, EntityType type, int x, int y, int groupId, string aiKeyOrPattern)
    {
        int newId = _idGen.Generate(type);

        var e = new MapEntity(newId, type, new GridPos(x, y));
        
        // entityId param is technically the ModelId (TypeId) from Data
        e.SetState("ModelId", entityId); 

        // [Data Driven Stats]
        int maxHp = 10;
        var entityData = EntityDataManager.Instance.Get(entityId);
        if (entityData != null)
        {
            maxHp = entityData.MaxHp;
             Console.WriteLine($"[Spawn] Found Data for {entityId}: HP={maxHp}");
        }
        else
        {
            // Fallback
            if (type == EntityType.Monster) maxHp = 50;
        }

        e.SetState("HP", maxHp);
        e.SetState("GroupId", groupId);

        if (type == EntityType.Monster && !string.IsNullOrEmpty(aiKeyOrPattern))
        {
             _monsterAI.RegisterMonster(e, aiKeyOrPattern);
        }
        // Objects might use aiKeyOrPattern for Pattern logic in future
        
        if (World2D.TrySpawn(e, e.Position))
        {
            _monsters.Add(e); // _monsters 리스트 이름을 _entities로 바꾸는게 좋겠지만 일단 같이 관리 (MapEntity니까)
            
            Console.WriteLine($"[GameSession] Spawned Entity {entityId}({type}) at ({x},{y}). Pattern={aiKeyOrPattern}");

            // Broadcast Spawn
            var pkt = new SC_EntitySpawn
            {
                BeatIndex = 0,
                EntityId = e.Id,
                EntityType = (int)e.Type,
                AppearanceId = e.GetState<int>("ModelId"), // ModelId mapped to AppearanceId 
                X = e.Position.X,
                Y = e.Position.Y,
                Hp = e.GetState<int>("HP")
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
                Hp = m.GetState<int>("HP"),
                AppearanceId = m.GetState<int>("ModelId") // [NEW] Map to AppearanceId
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

        // [NEW] API 연동 (Loading Phase)
        Task.Run(async () =>
        {
            string uid = !string.IsNullOrEmpty(s.Uid) ? s.Uid : s.SessionID.ToString();
            var pState = await ServerServices.ApiClient.GetPlayerStateAsync(uid);

            if (pState != null)
            {
                // Update Entity Stats from API Response (Calculated in ApiServer)
                var myEnt = _players.Find(x => x.Id == myActorId);
                if (myEnt != null)
                {
                    // [Fix] 테스트 중 HP 보호: API 값이 현재 세팅보다 작으면 기존 값 유지
                    int currentHp = myEnt.GetState<int>("HP");
                    int apiHp = pState.TotalHp;
                    int finalHp = (apiHp > 0 && apiHp >= currentHp) ? apiHp : currentHp;
                    myEnt.SetState("HP", finalHp);
                    myEnt.SetState("ATK", pState.TotalAtk);
                    myEnt.SetState("DEF", pState.TotalDef);
                    Console.WriteLine($"[GameSession] HP resolved: API={apiHp} Current={currentHp} Final={finalHp}");
                }

                // Inject auto-assigned skill slots into BeatActions
                BeatActions.InjectSkillSlots(myActorId, pState.ActiveSkillSlots, pState.NormalAttackSkillId);

                // Send SC_UpdateSkillSlots to Client
                var updateSkillsPkt = new SC_UpdateSkillSlots
                {
                    NormalAttackSkillId = pState.NormalAttackSkillId
                };
                foreach (var skill in pState.ActiveSkillSlots)
                {
                    updateSkillsPkt.activeSkillSlotss.Add(new SC_UpdateSkillSlots.ActiveSkillSlots
                    {
                        SkillId = skill ?? ""
                    });
                }
                s.Send(updateSkillsPkt.Write());

                Console.WriteLine($"[GameSession] Loaded PlayerState for Actor {myActorId}. HP: {pState.TotalHp}, ATK: {pState.TotalAtk}. SkillSlots injected and sent to Client.");

                // To ensure compatibility with client if it expects SC_Inventory, we construct a mock or fetch it.
                // For now, we will notify client about game start later, or client just waits for SC_AllPlayersLoaded/SC_GameBegin.
            }
            else
            {
                Console.WriteLine($"[GameSession] Warning: PlayerState could not be loaded for Actor {myActorId}.");
            }
        });
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
        _rhythmManager?.UpdateMusicState(beatIndex);

        _monsterAI.UpdateAI(beatIndex, _monsters, _players);
        _telegraph.OnBeat(beatIndex);
        BeatActions.OnBeat(beatIndex);
        
        Director.NotifyEvent(new GameEventContext { Type = EventType.Beat, TimeMs = AppRef.ServerTimeMs() });

        Director.Update(AppRef.ServerTimeMs());
        CleanupDeadEntities();
    }

    private void CleanupDeadEntities()
    {
        // Remove dead monsters and release IDs
        _monsters.RemoveAll(m => 
        {
            if (!m.IsAlive)
            {
                _idGen.Release(m.Id);
                _monsterAI.UnregisterMonster(m.Id);
                // Also ensure it's removed from World2D if not already?
                // World2D removal often happens on death, but let's be safe:
                if (World2D.ContainsEntity(m.Id))
                    World2D.Despawn(m.Id);
                    
                Console.WriteLine($"[GameSession] Entity {m.Id} Cleaned Up & ID Released.");
                return true;
            }
            return false;
        });
    }

    public override void Update()
    {
        // 서브 비트(Tick) 기반 정밀 스킬 검증 로직 연결
        if (_rhythm != null && _patternRunner != null)
        {
            long currentTick = _rhythm.GetCurrentTick(AppRef.ServerTimeMs());
            _patternRunner.UpdateTick(currentTick);
        }
    }

    public void BroadcastReturnToTown()
    {
        Console.WriteLine("[GameSession] Broadcast ReturnToTown");
        _broadcaster.Broadcast(new SC_ReturnToTown());
    }
}

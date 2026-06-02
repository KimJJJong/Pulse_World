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
    private readonly EntityIdGenerator _idGen = new EntityIdGenerator();
    
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

        World2D.OnEntityDead += OnEntityDeadHandler;

        Director = new GameDirector(this);
    }

    protected override void CleanupActor(int actorId)
    {
        BeatActions.CancelActor(actorId);
        _frozen.RemoveByActor(actorId);
        _telegraph.RemoveByCaster(actorId);
        base.CleanupActor(actorId);
    }

    private void OnEntityDeadHandler(int actorId)
    {
        var monster = _monsters.Find(x => x.Id == actorId);
        if (monster != null)
        {
            try
            {
                int groupId = monster.GetState<int>("GroupId");
                Director.NotifyEvent(new GameEventContext 
                { 
                    Type = EventType.Dead, 
                    TargetId = groupId,
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
    public void InitGame(IEnumerable<MapEntity> players, StageScenario scenario)
    {
        InitPlayers(players);
        _monsters.Clear();
        Director.LoadScenario(scenario);
        Director.NotifyEvent(new GameEventContext { Type = EventType.GameStart, TimeMs = AppRef.ServerTimeMs() });
        Console.WriteLine($"[InitGame] End (Director Loaded). SongStart={_rhythm.SongStartServerTimeMs}");
    }
    
    // Director 전용 엔티티(몬스터/오브젝트) 소환
    public void SpawnEntityInternal(int entityId, EntityType type, int x, int y, int groupId, string aiKeyOrPattern)
    {
        int newId = _idGen.Generate(type);
        var e = new MapEntity(newId, type, new GridPos(x, y));
        e.SetState("ModelId", entityId); 

        int maxHp = 10;
        var entityData = EntityDataManager.Instance.Get(entityId);
        if (entityData != null)
        {
            maxHp = entityData.MaxHp;
            Console.WriteLine($"[Spawn] Found Data for {entityId}: HP={maxHp}");
        }
        else
        {
            if (type == EntityType.Monster) maxHp = 50;
        }

        e.SetState("HP", maxHp);
        e.SetState("GroupId", groupId);

        if (type == EntityType.Monster && !string.IsNullOrEmpty(aiKeyOrPattern))
             _monsterAI.RegisterMonster(e, aiKeyOrPattern);
        
        if (World2D.TrySpawn(e, e.Position))
        {
            _monsters.Add(e);
            Console.WriteLine($"[GameSession] Spawned Entity {entityId}({type}) at ({x},{y}). Pattern={aiKeyOrPattern}");

            var pkt = new SC_EntitySpawn
            {
                BeatIndex = 0,
                EntityId = e.Id,
                EntityType = (int)e.Type,
                AppearanceId = e.GetState<int>("ModelId"),
                X = e.Position.X,
                Y = e.Position.Y,
                Hp = e.GetState<int>("HP")
            };
            _broadcaster.Broadcast(pkt);
        }
    }

    // ===============================
    // Init Packet 빌드
    // ===============================
    private SC_InitMap BuildInitPacketForPlayer(int myActorId)
    {
        SC_InitMap packet = new SC_InitMap();
        packet.ServerTimeMs = _time.NowMs;
        packet.Mode = 2;
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
            packet.playerss.Add(new SC_InitMap.Players { ActorId = p.Id, Name = "Test", Uid = "RealNeedit?" });

        packet.MyActorId = myActorId;
        packet.entitiess.Clear();

        foreach (var p in _players)
        {
            packet.entitiess.Add(new SC_InitMap.Entities
            {
                EntityId = p.Id,
                EntityType = (int)p.Type,
                X = p.Position.X,
                Y = p.Position.Y,
                Hp = p.GetState<int>("HP"),
                // [AppearanceId] MapEntity에 저장된 값 사용 (SendInitPacketToPlayer에서 API 후 설정됨)
                AppearanceId = p.GetState<int>("AppearanceId")
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
                AppearanceId = m.GetState<int>("ModelId")
            });
        }

        return packet;
    }

    /// <summary>
    /// GameRoom에서 각 유저 Session에게 호출.
    /// API로 PlayerState를 먼저 가져온 후 AppearanceId를 포함한 InitMap 패킷을 전송.
    /// </summary>
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
            Console.WriteLine($"[SendInitPacketToPlayer] actorId not registered. actorId={myActorId}");
            return;
        }

        if (!World2D.ContainsEntity(myActorId))
        {
            Console.WriteLine($"[SendInitPacketToPlayer] actorId not spawned. actorId={myActorId}");
            return;
        }

        // API에서 PlayerState(AppearanceId 포함) 먼저 조회 후 패킷 전송
        Task.Run(async () =>
        {
            string uid = !string.IsNullOrEmpty(s.Uid) ? s.Uid : s.SessionID.ToString();
            var pState = await ServerServices.ApiClient.GetPlayerStateAsync(uid);

            var myEnt = _players.Find(x => x.Id == myActorId);

            if (pState != null)
            {
                if (myEnt != null)
                {
                    // HP 보호: API 값이 현재 값보다 작으면 기존 유지
                    int currentHp = myEnt.GetState<int>("HP");
                    int apiHp = pState.TotalHp;
                    int finalHp = (apiHp > 0 && apiHp >= currentHp) ? apiHp : currentHp;
                    myEnt.SetState("HP", finalHp);
                    myEnt.SetState("ATK", pState.TotalAtk);
                    myEnt.SetState("DEF", pState.TotalDef);

                    // [핵심] AppearanceId를 MapEntity에 저장 → BuildInitPacketForPlayer에서 참조
                    int appearanceId = pState.AppearanceId;
                    myEnt.SetState("AppearanceId", appearanceId);

                    Console.WriteLine(
                        $"[GameSession] PlayerState loaded. Actor={myActorId} " +
                        $"HP={finalHp} ATK={pState.TotalAtk} AppearanceId={appearanceId}");
                }

                // 스킬 슬롯 주입
                BeatActions.InjectSkillSlots(myActorId, pState.ActiveSkillSlots, pState.NormalAttackSkillId);

                // SC_UpdateSkillSlots 전송
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
            }
            else
            {
                Console.WriteLine($"[GameSession] Warning: PlayerState load failed for Actor={myActorId}. AppearanceId defaults to 0.");
            }

            // API 결과 반영 후 InitMap 패킷 전송
            var pkt = BuildInitPacketForPlayer(myActorId);
            s.Send(pkt.Write());

            Console.WriteLine($"[SendInitPacketToPlayer] SC_InitMap sent. myActorId={myActorId}");
        });
    }

    // =====================================================
    // 클라이언트 입력 처리
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
        _rhythmManager?.UpdateMusicState(beatIndex);
        _monsterAI.UpdateAI(beatIndex, _monsters, _players);
        BeatActions.OnBeat(beatIndex);
        
        Director.NotifyEvent(new GameEventContext { Type = EventType.Beat, TimeMs = AppRef.ServerTimeMs() });
        Director.Update(AppRef.ServerTimeMs());
        CleanupDeadEntities();
    }

    private void CleanupDeadEntities()
    {
        _monsters.RemoveAll(m => 
        {
            if (!m.IsAlive)
            {
                _idGen.Release(m.Id);
                _monsterAI.UnregisterMonster(m.Id);
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

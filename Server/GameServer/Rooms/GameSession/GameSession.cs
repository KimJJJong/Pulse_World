using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.Manager.Map;
using GameServer.InGame.Manager.Map.Interface;
using GameServer.InGame.System.Rhythm;
using System.Collections.Generic;
using System.Linq;
using static SC_InitGame;

public sealed class GameSession
{
    public int SessionId { get; }

    // 월드 / 맵
    public IGameWorld World { get; }
    public MapWorld2D World2D { get; }
    private readonly Map2D _map;                // 리플렉션 대신 직접 보관

    // 액션 처리 및 Beat 스케줄링
    public BeatActionManager BeatActions { get; }

    // 외부 시스템
    private readonly RhythmSystem _rhythm;
    private readonly IServerTime _time;
    private readonly IGameBroadcaster _broadcaster;
    private readonly RhythmConfig _rhythmConfig;

    // 룸 구성 요소
    private readonly List<MapEntity> _players = new();
    private readonly List<MapEntity> _monsters = new();

    // Content
    private readonly MonsterAIController _monsterAI;

    private readonly Dictionary<int, int> _slotToActorId = new(); // slot -> actorId
    public int GetActorIdBySlot(int slot)
     => _slotToActorId.TryGetValue(slot, out var id) ? id : -1;


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
            rhythm,                   // IBeatClock 역할
            World,
            actionWindowMs: _rhythmConfig.ActionWindowMs,
            maxBeatLookAhead: _rhythmConfig.MaxBeatLookAhead
        );
        _monsterAI = new MonsterAIController(World, BeatActions);

        // RhythmSystem이 Beat가 바뀔 때마다 GameSession.OnBeat 호출
        _rhythm.OnBeat += OnBeat;
    }

    // =====================================================
    //  초기화
    // =====================================================

    public void InitGame(IEnumerable<MapEntity> players, IEnumerable<MapEntity> monsters)
    {
        _players.Clear();
        _monsters.Clear();
        _slotToActorId.Clear();

        int slot = 0;
        foreach (var p in players)
        {
            if (World2D.TrySpawn(p, p.Position))
            {
                _players.Add(p);
                _slotToActorId[slot] = p.Id;
                slot++;
            }
        }

        foreach (var m in monsters)
        {
            if (World2D.TrySpawn(m, m.Position))
            {
                _monsters.Add(m);
                _monsterAI.RegisterMonster(m);
            }
        }

        SendInitPacket();
    }
    private void SendInitPacket()
    {
        // 패킷 인스턴스 생성
        SC_InitGame packet = new SC_InitGame();

        // --- 맵 정보 ---
        packet.MapWidth = _map.Width;
        packet.MapHeight = _map.Height;

        // --- Tiles 채우기 (Map2D -> SC_InitGame.Tiles 리스트) ---
        packet.tiless.Clear();
        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                var tile = new SC_InitGame.Tiles
                {
                    // Map2D.Get 이 TileKind(enum) 이면 (int) 캐스팅
                    TileKind = (int)_map.Get(x, y)
                };
                packet.tiless.Add(tile);
            }
        }

        // --- PlayerActorIds 채우기 ---
        packet.playerActorIdss.Clear();
        foreach (MapEntity p in _players)
        {
            var pa = new SC_InitGame.PlayerActorIds
            {
                ActorId = p.Id
            };
            packet.playerActorIdss.Add(pa);
        }

        // --- MyActorId (프로토타입: 첫 번째 플레이어 기준) ---
        packet.MyActorId = _players.Count > 0 ? _players[0].Id : 0;

        // --- SpawnEntities 채우기 (플레이어 + 몬스터) ---
        packet.spawnEntitiess.Clear();

        foreach (MapEntity p in _players)
        {
            var s = new SC_InitGame.SpawnEntities
            {
                EntityId = p.Id,
                EntityType = (int)p.Type,          // enum이면 (int) 캐스팅
                X = p.Position.X,
                Y = p.Position.Y,
                Hp = p.GetState<int>("HP") // 없으면 0 나와도 괜찮게 사용
            };
            packet.spawnEntitiess.Add(s);
        }

        foreach (MapEntity m in _monsters)
        {
            var s = new SC_InitGame.SpawnEntities
            {
                EntityId = m.Id,
                EntityType = (int)m.Type,
                X = m.Position.X,
                Y = m.Position.Y,
                Hp = m.GetState<int>("HP")
            };
            packet.spawnEntitiess.Add(s);
        }

        // 브로드캐스트
        _broadcaster.Broadcast(packet);
    }


    // =====================================================
    // 클라이언트 입력 처리
    // =====================================================
    public void OnClientActionPacketBySlot(int slot, CS_ActionRequest req)
    {
        int actorId = GetActorIdBySlot(slot);
        if (actorId < 0)
        {
            // TODO: 로그 및 무시
            return;
        }

        // 원래 OnClientActionPacket 로직을 여기로 통합하거나 호출
        BeatActions.OnClientActionRequest(actorId, req);
    }
    //public void OnClientActionPacket(int actorId, CS_ActionRequest req)
    //{
    //    // BeatActionManager 내부에서:
    //    // - ServerReceiveTimeMs = NowMs
    //    // - IBeatClock(RhythmSystem)로 Beat 판정
    //    // - BeatScheduler에 예약
    //    BeatActions.OnClientActionRequest(actorId, req);
    //}

    // =====================================================
    // Beat 도래 시 호출 (RhythmSystem에서 이벤트로 호출됨)
    // =====================================================

    public void OnBeat(long beatIndex)
    {
        _monsterAI.UpdateAI(beatIndex, _monsters, _players);

        // 2) 그 다음 예약된(플레이어/몬스터) 명령 전부 실행 + SC_BeatActions 송신
        BeatActions.OnBeat(beatIndex);
    }

    // =====================================================
    // 틱 업데이트 (원한다면 사용)
    // =====================================================

    public void Update()
    {
        // Tick 기반 AI, 상태변화, Debuff 처리 등을 넣을 수 있음
    }
}

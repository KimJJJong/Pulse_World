using UnityEngine;
using System.Collections.Generic;

public class BoardView : MonoBehaviour, IClientWorldView
{
    public static BoardView Instance { get; private set; }

    [Header("Prefabs (자동 생성/할당 가능)")]
    public GameObject playerPrefab;
    public GameObject monsterPrefab;

    public GameObject tilePrefab;
    
    // Runtime Cache
    private Dictionary<int, GameObject> _entityPrefabCache = new Dictionary<int, GameObject>();

    [Header("Rendering")]
    public float cellSize = 1.0f;
    public float moveLerpTime = 0.1f;

    // entityId -> EntityVisual
    private readonly Dictionary<int, EntityVisual> _entityViews = new();

    // (x,y) -> Tile GameObject
    private GameObject[,] _tiles;

    private Color[,] _baseTileColors;

    private static readonly Color TELEGRAPH_COLOR = Color.red;

    // Client-Side Prediction & Instant Broadcast: 이미 모션을 재생한 Attack/Skill을 추적
    // 서버 응답(SC_BeatActions)에서 중복 애니메이션 재생을 방지
    private Dictionary<int, float> _recentInstantActions = new();

    // [Client-Side Prediction] Move 예측 처리
    // value: (예측한 목표 타일 좌표, 예측 시점 출발 월드 포지션)
    // 출발 위치를 저장해 예측 실패 시 PlayBumpBack의 복귀 기준점으로 활용한다.
    private Dictionary<int, (Vector2Int predictedTile, Vector3 fromWorld)> _recentPredictedMoves = new();

    // Actor 별로 현재 실행 중인 데이터 기반 스킬 시뮬레이터(ClientSkillRunner) 추적
    private Dictionary<int, ClientSkillRunner> _activeSkillRunners = new();

    // [New] (x,y) -> Expire Beat
    private Dictionary<Vector2Int, long> _telegraphExpiration = new Dictionary<Vector2Int, long>();

    #region Debug
    private const bool DBG_POS = false;
    private int _dbgOnlyActorId = -1; // -1이면 전부, 특정 ID만 보고 싶으면 그 값으로
    #endregion

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        LoadEntityMapping();
        ClientGameState.Instance.WorldView = this;
    }

    void Update()
    {
        if (RhythmClient.Instance != null && RhythmClient.Instance.ServerSongStartMs > 0 && _telegraphExpiration.Count > 0)
        {
            long currentBeat = RhythmClient.Instance.GetCurrentBeatIndex();
            List<Vector2Int> toRemove = null;

            foreach (var kv in _telegraphExpiration)
            {
                // 현재 비트가 만료 비트와 같거나 크면 제거 (만료 비트는 해당 비트가 시작될 때 사라짐을 의미)
                if (currentBeat >= kv.Value)
                {
                    if (toRemove == null) toRemove = new List<Vector2Int>();
                    toRemove.Add(kv.Key);
                }
            }

            if (toRemove != null)
            {
                foreach (var pos in toRemove)
                {
                    _telegraphExpiration.Remove(pos);
                    SetTelegraphOverlay(pos.x, pos.y, false);
                }
            }
        }
    }

    private void LoadEntityMapping()
    {
        var textAsset = Resources.Load<TextAsset>("Data/EntityData");
        if (textAsset != null)
        {
            try
            {
                var root = JsonUtility.FromJson<EntityDataRoot>(textAsset.text);
                foreach (var e in root.Entities)
                {
                    if (!string.IsNullOrEmpty(e.ResourcePath))
                        _entityPathMap[e.EntityId] = e.ResourcePath;
                }
                Debug.Log($"[BoardView] Loaded {_entityPathMap.Count} entity paths from JSON.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoardView] Failed to parse EntityData.json: {ex.Message}");
            }
        }
    }

    [System.Serializable]
    class EntityDataRoot { public List<EntityDataDTO> Entities; }
    [System.Serializable]
    class EntityDataDTO { public int EntityId; public string ResourcePath; }

    #region IClientWorldView

    public void OnCreateMap(int width, int height)
    {
        if (TryBindTilesFromScene(width, height))
        {
            Debug.Log($"[BoardView] OnCreateMap: Baked tiles bound. ({width}x{height})");
            return;
        }

        Debug.LogWarning($"[BoardView] OnCreateMap: No baked tiles found. Fallback to Instantiate. ({width}x{height})");
        ClearTiles(destroyGameObjects: true);

        if (tilePrefab == null)
        {
            Debug.LogWarning("[BoardView] tilePrefab이 설정되지 않음. 타일을 생성하지 않습니다.");
            return;
        }

        _tiles = new GameObject[width, height];
        _baseTileColors = new Color[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = Instantiate(tilePrefab, transform);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.position = GridToWorld(x, y) + new Vector3(0, -2, 0);
                _tiles[x, y] = tile;
                _baseTileColors[x, y] = Color.gray;
            }
        }
    }

    public Color GetTileColor(int tileKind)
    {
        switch (tileKind)
        {
            case 0: return Color.gray;
            case 1: return Color.white;
            case 2: return Color.cyan;
            default: return Color.white;
        }
    }

    private Renderer GetTileRenderer(GameObject tile)
    {
        if (tile == null) return null;
        if (tile.TryGetComponent<Renderer>(out var r)) return r;
        return tile.GetComponentInChildren<Renderer>();
    }

    public void OnSetTile(int x, int y, int tileKind)
    {
        if (_tiles == null) return;
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1)) return;

        var tile = _tiles[x, y];
        if (tile == null) return;

        var rend = GetTileRenderer(tile);
        if (rend != null)
        {
            Color baseColor = GetTileColor(tileKind);
            if (_baseTileColors != null)
                _baseTileColors[x, y] = baseColor;
            rend.material.color = baseColor;
        }
    }

    /// <summary>
    /// 해당 좌표 타일을 "텔레그래프 빨강"으로 덮는다.
    /// </summary>
    public void SetTelegraphOverlay(int x, int y, bool on)
    {
        if (_tiles == null)
        {
            // Debug.LogWarning($"[BoardView] SetTelegraphOverlay: _tiles is null at ({x},{y}). MapGenerationComplete={ClientGameState.Instance?.IsMapGenerationComplete}");
            return;
        }
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1))
        {
            Debug.LogWarning($"[BoardView] SetTelegraphOverlay: Out of bounds ({x},{y}). MapSize=({_tiles.GetLength(0)}x{_tiles.GetLength(1)})");
            return;
        }

        var tile = _tiles[x, y];
        if (tile == null)
        {
            Debug.LogWarning($"[BoardView] SetTelegraphOverlay: Tile null at ({x},{y})");
            return;
        }

        var rend = GetTileRenderer(tile);
        if (rend == null)
        {
            Debug.LogWarning($"[BoardView] SetTelegraphOverlay: Renderer missing at ({x},{y}) Obj={tile.name}");
            return;
        }

        if (on)
            rend.material.color = TELEGRAPH_COLOR;
        else
            RestoreTileColor(x, y);
    }

    /// <summary>
    /// [New] 만료 비트를 지정하여 경고를 출력합니다. 중앙 집중식 관리용.
    /// </summary>
    public void SetTelegraphWithExpire(int x, int y, long expireBeat)
    {
        var pos = new Vector2Int(x, y);
        long currentBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : -1;

        if (_telegraphExpiration.TryGetValue(pos, out long currentExpire))
        {
            if (expireBeat <= currentExpire)
            {
                // [WarningShow] 기존 것이 더 미래 → 무시 (덮어쓰기로 인한 깜빡임 방지)
                Debug.Log($"[WarningShow] SKIP-older ({x},{y}) newExpire={expireBeat} <= existing={currentExpire} curBeat={currentBeat}");
                return;
            }
        }

        // [WarningShow_Error] 현재 비트보다 과거인 경고는 즉시 만료될 것 → 표시 안 함
        if (expireBeat <= currentBeat)
        {
            Debug.LogWarning($"[WarningShow_Error] PAST-expire ({x},{y}) expireBeat={expireBeat} curBeat={currentBeat} rtt={TimeSync.EstimatedRttMs:F0}ms — will flicker; skipping");
            return;
        }

        long livesBeats = expireBeat - currentBeat;
        _telegraphExpiration[pos] = expireBeat;
        SetTelegraphOverlay(x, y, true);

        // [WarningShow] 정상 표시 — 몇 비트 동안 보일지 로깅
        Debug.Log($"[WarningShow] ({x},{y}) expireBeat={expireBeat} curBeat={currentBeat} livesBeats={livesBeats}");
    }

    public void RestoreTileColor(int x, int y)
    {
        if (_tiles == null) return;
        if (_baseTileColors == null) return;
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1)) return;

        var tile = _tiles[x, y];
        if (tile == null) return;

        var rend = GetTileRenderer(tile);
        if (rend != null)
            rend.material.color = _baseTileColors[x, y];
    }

    public bool TryBindTilesFromScene(int width, int height)
    {
        var map = new Dictionary<(int x, int y), GameObject>();

        foreach (Transform child in transform)
        {
            if (ParseTileName(child.name, out int x, out int y))
                map[(x, y)] = child.gameObject;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!map.ContainsKey((x, y)))
                {
                    Debug.LogWarning($"[BoardView] Bind Fail: Missing tile at ({x},{y})");
                    return false;
                }
            }
        }

        ClearTiles(destroyGameObjects: false);
        _tiles = new GameObject[width, height];
        _baseTileColors = new Color[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _tiles[x, y] = map[(x, y)];
                var rend = GetTileRenderer(_tiles[x, y]);
                if (rend != null)
                    _baseTileColors[x, y] = rend.sharedMaterial != null ? rend.sharedMaterial.color : Color.gray;
                else
                {
                    Debug.LogWarning($"[BoardView] Bound tile at ({x},{y}) has no renderer!");
                    _baseTileColors[x, y] = Color.gray;
                }
            }
        }

        Debug.Log($"[BoardView] Bind Success (NameBased): {width}x{height}");
        return true;
    }

    public void OnClearEntities()
    {
        foreach (var kv in _entityViews)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
        _entityViews.Clear();
    }

    public void OnSpawnOrUpdateEntity(ClientEntityInfo info)
    {
        if (!_entityViews.TryGetValue(info.EntityId, out var visual) || visual == null)
        {
            GameObject prefab = ChoosePrefab(info.EntityType, info.AppearanceId);
            if (prefab == null)
            {
                Debug.LogWarning($"[BoardView] EntityType {info.EntityType}용 Prefab이 없음");
                return;
            }

            GameObject go = Instantiate(prefab, transform);
            go.name = $"Entity_{info.EntityId}";

            if (!go.TryGetComponent<EntityVisual>(out visual))
                visual = go.AddComponent<EntityVisual>();

            _entityViews[info.EntityId] = visual;

            if (visual.TryGetComponent<RhythmRPG.Visual.CharacterVisualController>(out var visualCtrl))
            {
                visualCtrl.SetContext(RhythmRPG.Visual.CharacterContext.Game);

                if (info.EntityId == ClientGameState.Instance.MyActorId)
                {
                    var myEquips = InventoryManager.Instance.Equipments;
                    List<int> equippedTemplateIds = new List<int>();
                    foreach (var e in myEquips)
                    {
                        if (e.IsEquipped) equippedTemplateIds.Add(e.TemplateId);
                    }
                    visualCtrl.UpdateEquipments(equippedTemplateIds);
                }
            }
        }

        if (info.EntityId == ClientGameState.Instance.MyActorId)
        {
            CameraBinder.Instance?.Bind(visual.transform);
            RhythmInputControllerBinder.Instance?.Bind(visual.gameObject);
        }

        visual.transform.position = GridToWorld(info.X, info.Y);
    }

    public void OnDespawnEntity(int entityId)
    {
        if (_entityViews.TryGetValue(entityId, out var visual) && visual != null)
            Destroy(visual.gameObject);
        _entityViews.Remove(entityId);
    }

    [Header("Sync")]
    [Range(0.1f, 2.0f)]
    public float actionDurationRatio = 0.5f;

    public void OnBeatAction(ClientBeatAction action, ClientEntityInfo entity)
    {
        if (!_entityViews.TryGetValue(action.ActorId, out var visual) || visual == null)
            return;

        double beatMs   = RhythmClient.Instance.GetBeatDurationMs();
        float  duration = (float)(beatMs / 1000.0) * actionDurationRatio;

        // ── Move 처리 ──────────────────────────────────────────────────────────
        if (action.ActionKind == (int)ActionKind.Move)
        {
            // [서버 권위] Prediction 검증 완전 제거
            // 서버에서 확정된 From/To를 무조건 사용해 위치 동기화
            _recentPredictedMoves.Remove(action.ActorId); // 혹시 남아있는 예측 캐시 정리

            Vector3 serverFromW = GridToWorld(action.FromX, action.FromY);
            Vector3 serverToW   = GridToWorld(action.ToX,   action.ToY);

            // [MoveRecv] 서버 확정 이동 수신 — packetBeat vs clientBeat의 차이가 곧 네트워크 지연
            long clientBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : -1;
            long beatGap = clientBeat - action.BeatIndex;
            Debug.Log($"[MoveRecv] actor={action.ActorId} from=({action.FromX},{action.FromY}) to=({action.ToX},{action.ToY}) " +
                      $"accepted={action.Accepted} packetBeat={action.BeatIndex} clientBeat={clientBeat} " +
                      $"beatGap={beatGap} (positive=late) rtt={TimeSync.EstimatedRttMs:F0}ms");

            if (!action.Accepted)
            {
                // 서버 거부: 시도 방향으로 살짝 BumpBack
                visual.PlayBumpBack(serverToW, serverFromW);
                return;
            }

            // 이동 거리 > 1칸이면 Dash/스킬 이동 → 빠른 duration
            int distanceTiles = Mathf.RoundToInt(Vector3.Distance(serverFromW, serverToW) / cellSize);
            float moveDuration = distanceTiles > 1 ? duration * 0.4f : duration;

            // 렌더 위치가 서버 From과 가까우면 현재 위치에서, 멀면 서버 From으로 스냅 후 이동
            float snapThreshold = 0.5f;
            float distFromServer = Vector3.Distance(visual.transform.position, serverFromW);
            Vector3 moveStart = distFromServer <= snapThreshold
                ? visual.transform.position
                : serverFromW;

            visual.StartMove(moveStart, serverToW, moveDuration);
            visual.SetRotation(action.Rotation);
            return;
        }

        // ── Attack / Skill ────────────────────────────────────────────────────
        // [Fix] Attack→Skill 통일: Attack/Skill 모두 PlaySkillInstant 경로로 처리
        if (!action.Accepted) return;

        bool isAttackOrSkill = action.ActionKind == (int)ActionKind.Attack
                            || action.ActionKind == (int)ActionKind.Skill;
        if (isAttackOrSkill)
        {
            if (_recentInstantActions.TryGetValue(action.ActorId, out float lastTime)
                && Time.time - lastTime < 1.5f)
            {
                _recentInstantActions.Remove(action.ActorId);
                // [Fix] 이미 PlaySkillInstant에서 SetRotation 완료한 경우는
                //       SC_BeatActions의 Rotation으로 덧쓰우지 않는다
                return;
            }
            _recentInstantActions.Remove(action.ActorId);

            // [Fix] 2Beat 스킬의 두 번째 패킷(Delayed Damage) 처리:
            // ClientSkillRunner가 아직 실행 중이면 애니메이션 재시작과 SetRotation 쓸는 것을 방지
            if (_activeSkillRunners.TryGetValue(action.ActorId, out var activeRunner) && activeRunner != null)
                return;
        }

        // Attack/Skill 모두 PlaySkill로 통일 (Animation + 사운드는 ClientSkillRunner가 처리)
        if (isAttackOrSkill)
        {
            bool isMine = ClientGameState.Instance != null
                       && action.ActorId == ClientGameState.Instance.MyActorId;
            visual.PlaySkill(duration, isMine);
        }

        // [Fix] Attack/Skill Rotation: instant 쾐시가 없어서 여기까지 내려온 케이스만
        //       SetRotation 적용 (타 플레이어 / 모두 줄어듦)
        visual.SetRotation(action.Rotation);
    }

    /// <summary>
    /// SC_ActionInstantBroadcast 처리: 즉각적인 공격/스킬 브로드캐스트 재생
    /// </summary>
    public void PlayInstantActionBroadcast(int actorId, ActionKind kind, float rotation, float duration)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null)
            return;

        bool isMine = (ClientGameState.Instance != null && actorId == ClientGameState.Instance.MyActorId);

        // [Fix] Attack→Skill 통일: Attack/Skill 모두 PlaySkill 호웈 (사운드는 ClientSkillRunner가 실행)
        if (kind == ActionKind.Attack || kind == ActionKind.Skill)
        {
            visual.PlaySkill(duration, isMine);
        }

        visual.SetRotation(rotation);

        _recentInstantActions[actorId] = Time.time;
    }

    /// <summary>
    /// [Client-Side Prediction] 즉시 타일 이동 렌더링 시뮬레이션.
    /// 서버 응답 전에 먼저 이동 연출을 재생하고, 출발 위치(curW)를 함께 저장해
    /// 예측 실패 시 PlayBumpBack의 복귀 기준점으로 사용한다.
    /// </summary>
    public void PlayMovePrediction(int actorId, int toX, int toY, float durationRatio)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null) return;

        Vector3 curW = visual.transform.position;
        Vector3 toW  = GridToWorldPublic(toX, toY);

        double beatMs  = RhythmClient.Instance.GetBeatDurationMs();
        float  duration = (float)(beatMs / 1000.0) * durationRatio;

        visual.StartMove(curW, toW, duration);

        // 출발 위치(curW)도 함께 저장 → 예측 실패 시 BumpBack 복귀 기준점
        _recentPredictedMoves[actorId] = (new Vector2Int(toX, toY), curW);
    }

    /// <summary>
    /// [NewSkill System] 데이터 기반 스킬 실행 (동적으로 ClientSkillRunner 생성)
    /// </summary>
    public void PlaySkillInstant(int actorId, string skillId, float rotation, long startTick)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null)
        {
            Debug.LogWarning($"[AnimPlay] MISS actor={actorId} skill={skillId} — no visual registered");
            return;
        }

        bool isMine = (ClientGameState.Instance != null && actorId == ClientGameState.Instance.MyActorId);

        _recentInstantActions[actorId] = Time.time;

        if (_activeSkillRunners.TryGetValue(actorId, out var prevRunner))
        {
            if (prevRunner != null) Destroy(prevRunner.gameObject);
            _activeSkillRunners.Remove(actorId);
        }

        GameObject go = new GameObject($"SkillRunner_{actorId}_{skillId}");
        // [Fix] SkillRunner를 visual의 자식이 아닌 BoardView 자식으로 생성.
        // visual.transform 자식으로 생성하면 EntityVisual.StopAllCoroutines()가
        // 이동 코루틴을 중단시킬 때 SkillRunner 코루틴도 함께 중단되어
        // 이동 스킬 초반 실행이 씹히는 문제가 발생함.
        // BoardView 하위에 두면 이동 연출과 독립적으로 동작함.
        go.transform.SetParent(this.transform);

        var runner = go.AddComponent<ClientSkillRunner>();
        runner.Initialize(this, actorId, visual, skillId, startTick, isMine, rotation);

        visual.SetRotation(rotation);

        _activeSkillRunners[actorId] = runner;

        // [AnimPlay] 스킬 애니메이션 시작 — startTick 기준이 현재 서버 tick과 얼마나 어긋나있는지 확인.
        // tickGap 양수=서버 tick이 startTick보다 미래(애니 시작이 '이미 늦음')
        // tickGap 음수=startTick이 미래(정상, 곧 재생될 예정)
        // 원격에서 큰 양수(수십~수백)가 지속되면 TimeSync/SongSync가 어긋나 있다는 신호.
        long curTick = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentServerTick() : 0;
        long tickGap = curTick - startTick;
        Debug.Log($"[AnimPlay] actor={actorId} skill={skillId} isMine={isMine} startTick={startTick} " +
                  $"curTick={curTick} tickGap={tickGap} rot={rotation:F0} rtt={TimeSync.EstimatedRttMs:F0}ms");
    }

    /// <summary>
    /// [NewSkill System] 스킬 트리거에 따른 텔레그래프(경고) 출력
    /// </summary>
    public void ShowSkillTelegraph(int actorId, int styleId, long startTick, int totalDurationTicks, int shape, int originType, int originX, int originY, int paramA, int paramB, List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return;
        
        // [Fix] 정수 나눗셈 올림 처리를 통해 최소 1비트 유지 보장
        long expireBeat = (startTick + totalDurationTicks + 479) / 480;
        
        foreach (var cell in cells)
            SetTelegraphWithExpire(cell.x, cell.y, expireBeat);
    }

    public bool IsActorRunningNewSkill(int actorId)
    {
        if (_activeSkillRunners.TryGetValue(actorId, out var runner))
            return runner != null;
        return false;
    }

    public void OnInitGameCompleted()
    {
        Debug.Log("[BoardView] InitGameCompleted");
    }

    #endregion

    #region Map Binding & Baking

    public Vector3 GridToWorldPublic(int x, int y)
    {
        return GridToWorld(x, y);
    }

    private bool ParseTileName(string name, out int x, out int y)
    {
        x = -1;
        y = -1;
        if (!name.StartsWith("Tile_")) return false;

        var parts = name.Split('_');
        if (parts.Length < 3) return false;

        if (int.TryParse(parts[1], out x) && int.TryParse(parts[2], out y))
            return true;

        return false;
    }

    #endregion

    #region Helper

    private Dictionary<int, string> _entityPathMap = new Dictionary<int, string>();

    private GameObject ChoosePrefab(int entityType, int modelId)
    {
        if (_entityPrefabCache.TryGetValue(modelId, out var prefab))
            return prefab;

        string loadPath = "";
        if (_entityPathMap.TryGetValue(modelId, out var mappedPath))
            loadPath = mappedPath;
        else
            loadPath = $"Entities/Entity_{modelId}";

        var def = Resources.Load<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(loadPath);

        if (def != null && def.Prefab != null)
        {
            _entityPrefabCache[modelId] = def.Prefab;
            Debug.Log($"[BoardView] Loaded Entity {modelId} from '{loadPath}'");
            return def.Prefab;
        }
        else
        {
            if (modelId > 0)
                Debug.LogWarning($"[BoardView] Failed to load Entity {modelId}. Path='{loadPath}'");
        }

        switch (entityType)
        {
            case (int)EntityType.Player:  return playerPrefab;
            case (int)EntityType.Monster: return monsterPrefab;
            case (int)EntityType.Object:  return monsterPrefab;
            default:                      return monsterPrefab;
        }
    }

    private Vector3 GridToWorld(int x, int y)
    {
        float targetX = x * cellSize;
        float targetZ = y * cellSize;
        float height  = GetGroundHeight(targetX, targetZ);
        return new Vector3(targetX, height, targetZ);
    }

    private float GetGroundHeight(float x, float z)
    {
        Ray ray = new Ray(new Vector3(x, 20f, z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 50f))
            return hit.point.y;

        Debug.LogWarning($"[GetGroundHeight] Miss! ({x}, {z}) -> Defaulting to 0");
        return 0f;
    }

    private void ClearTiles(bool destroyGameObjects = true)
    {
        if (_tiles == null) return;

        int w = _tiles.GetLength(0);
        int h = _tiles.GetLength(1);

        if (destroyGameObjects)
        {
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (_tiles[x, y] != null)
                        Destroy(_tiles[x, y]);
        }

        _tiles = null;
        _baseTileColors = null;
    }

    #endregion
}

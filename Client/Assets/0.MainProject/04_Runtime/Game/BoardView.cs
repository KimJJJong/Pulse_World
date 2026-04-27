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

    private Dictionary<int, float> _recentInstantActions = new();
    private Dictionary<int, (Vector2Int predictedTile, Vector3 fromWorld)> _recentPredictedMoves = new();
    private Dictionary<int, ClientSkillRunner> _activeSkillRunners = new();
    private Dictionary<Vector2Int, long> _telegraphExpiration = new Dictionary<Vector2Int, long>();

    #region Debug
    private const bool DBG_POS = false;
    private int _dbgOnlyActorId = -1;
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

    public void SetTelegraphOverlay(int x, int y, bool on)
    {
        if (_tiles == null) return;
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1))
        {
            //Out of Boundary
            //Debug.LogWarning($"[BoardView] SetTelegraphOverlay: Out of bounds ({x},{y}). MapSize=({_tiles.GetLength(0)}x{_tiles.GetLength(1)})");
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

    public void SetTelegraphWithExpire(int x, int y, long expireBeat)
    {
        var pos = new Vector2Int(x, y);
        long currentBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : -1;

        if (_telegraphExpiration.TryGetValue(pos, out long currentExpire))
        {
            if (expireBeat <= currentExpire)
            {
                //Debug.Log($"[WarningShow] SKIP-older ({x},{y}) newExpire={expireBeat} <= existing={currentExpire} curBeat={currentBeat}");
                return;
            }
        }

        if (expireBeat <= currentBeat)
        {
            Debug.LogWarning($"[WarningShow_Error] PAST-expire ({x},{y}) expireBeat={expireBeat} curBeat={currentBeat} rtt={TimeSync.EstimatedRttMs:F0}ms — will flicker; skipping");
            return;
        }

        _telegraphExpiration[pos] = expireBeat;
        SetTelegraphOverlay(x, y, true);
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
                Debug.LogWarning($"[BoardView] EntityType {info.EntityType}용 Prefab이 없음 (AppearanceId={info.AppearanceId})");
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
                    visualCtrl.SetLocalPlayer(true);
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

        if (action.ActionKind == (int)ActionKind.Move)
        {
            _recentPredictedMoves.Remove(action.ActorId);

            Vector3 serverFromW = GridToWorld(action.FromX, action.FromY);
            Vector3 serverToW   = GridToWorld(action.ToX,   action.ToY);

            if (!action.Accepted)
            {
                visual.PlayBumpBack(serverToW, serverFromW);
                return;
            }

            int distanceTiles = Mathf.RoundToInt(Vector3.Distance(serverFromW, serverToW) / cellSize);
            float moveDuration = distanceTiles > 1 ? duration * 0.4f : duration;

            float snapThreshold = 0.5f;
            float distFromServer = Vector3.Distance(visual.transform.position, serverFromW);
            Vector3 moveStart = distFromServer <= snapThreshold
                ? visual.transform.position
                : serverFromW;

            visual.StartMove(moveStart, serverToW, moveDuration);
            visual.SetRotation(action.Rotation);
            return;
        }

        if (!action.Accepted) return;

        bool isAttackOrSkill = action.ActionKind == (int)ActionKind.Attack
                            || action.ActionKind == (int)ActionKind.Skill;
        if (isAttackOrSkill)
        {
            if (_recentInstantActions.TryGetValue(action.ActorId, out float lastTime)
                && Time.time - lastTime < 1.5f)
            {
                _recentInstantActions.Remove(action.ActorId);
                return;
            }
            _recentInstantActions.Remove(action.ActorId);

            if (_activeSkillRunners.TryGetValue(action.ActorId, out var activeRunner) && activeRunner != null)
                return;
        }

        if (isAttackOrSkill)
        {
            bool isMine = ClientGameState.Instance != null
                       && action.ActorId == ClientGameState.Instance.MyActorId;
            visual.PlaySkill(duration, isMine);
        }

        visual.SetRotation(action.Rotation);
    }

    public void PlayInstantActionBroadcast(int actorId, ActionKind kind, float rotation, float duration)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null)
            return;

        bool isMine = (ClientGameState.Instance != null && actorId == ClientGameState.Instance.MyActorId);

        if (kind == ActionKind.Attack || kind == ActionKind.Skill)
            visual.PlaySkill(duration, isMine);

        visual.SetRotation(rotation);
        _recentInstantActions[actorId] = Time.time;
    }

    public void PlayMovePrediction(int actorId, int toX, int toY, float durationRatio)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null) return;

        Vector3 curW = visual.transform.position;
        Vector3 toW  = GridToWorldPublic(toX, toY);

        double beatMs  = RhythmClient.Instance.GetBeatDurationMs();
        float  duration = (float)(beatMs / 1000.0) * durationRatio;

        visual.StartMove(curW, toW, duration);
        _recentPredictedMoves[actorId] = (new Vector2Int(toX, toY), curW);
    }

    public void PlaySkillInstant(int actorId, string skillId, float rotation, long startTick)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null)
        {
            Debug.LogWarning($"[AnimPlay] MISS actor={actorId} skill={skillId} — no visual registered");
            return;
        }

        bool isMine = (ClientGameState.Instance != null && actorId == ClientGameState.Instance.MyActorId);

        _recentInstantActions[actorId] = Time.time;

        if (!_activeSkillRunners.TryGetValue(actorId, out var runner) || runner == null)
        {
            GameObject go = new GameObject($"SkillRunner_{actorId}_{skillId}");
            go.transform.SetParent(this.transform);
            runner = go.AddComponent<ClientSkillRunner>();
            _activeSkillRunners[actorId] = runner;
        }
        else
        {
            runner.gameObject.name = $"SkillRunner_{actorId}_{skillId}";
        }

        runner.Initialize(this, actorId, visual, skillId, startTick, isMine, rotation);

        visual.SetRotation(rotation);

        long curTick = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentServerTick() : 0;
        long tickGap = curTick - startTick;
        Debug.Log($"[AnimPlay] actor={actorId} skill={skillId} isMine={isMine} startTick={startTick} " +
                  $"curTick={curTick} tickGap={tickGap} rot={rotation:F0} rtt={TimeSync.EstimatedRttMs:F0}ms");
    }

    public void ShowSkillTelegraph(int actorId, int styleId, long startTick, int totalDurationTicks, int shape, int originType, int originX, int originY, int paramA, int paramB, List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return;
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

    public Vector3 GridToWorldPublic(int x, int y) => GridToWorld(x, y);

    private bool ParseTileName(string name, out int x, out int y)
    {
        x = -1; y = -1;
        if (!name.StartsWith("Tile_")) return false;
        var parts = name.Split('_');
        if (parts.Length < 3) return false;
        return int.TryParse(parts[1], out x) && int.TryParse(parts[2], out y);
    }

    #endregion

    #region Helper

    private Dictionary<int, string> _entityPathMap = new Dictionary<int, string>();

    /// <summary>
    /// AppearanceId로 프리팹을 결정합니다.
    /// 우선순위: 1) AppearanceId 정적 매핑 (플레이어 캐릭터 외견)
    ///          2) EntityData.json 매핑 → EntityDefinitionSO (몬스터/오브젝트)
    ///          3) Resources/Entity/ 직접 로드 (Entity_{id} 규칙)
    ///          4) Inspector playerPrefab / monsterPrefab 폴백
    /// </summary>
    private GameObject ChoosePrefab(int entityType, int modelId)
    {
        if (_entityPrefabCache.TryGetValue(modelId, out var cached))
            return cached;

        // 1. AppearanceId 정적 매핑 (플레이어 외견 ID 우선)
        if (AppearanceCatalog.TryGetPrefabName(modelId, out var appearanceName))
        {
            var ap = Resources.Load<GameObject>($"Entity/{appearanceName}");
            if (ap != null)
            {
                _entityPrefabCache[modelId] = ap;
                Debug.Log($"[BoardView] ✅ AppearanceId={modelId} → Resources/Entity/{appearanceName}");
                return ap;
            }
            Debug.LogWarning($"[BoardView] AppearanceId={modelId} 매핑 '{appearanceName}' 프리팹 없음. 폴백 진행.");
        }

        // 2. EntityData.json 매핑 → EntityDefinitionSO
        if (_entityPathMap.TryGetValue(modelId, out var mappedPath))
        {
            var def = Resources.Load<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(mappedPath);
            if (def != null && def.Prefab != null)
            {
                _entityPrefabCache[modelId] = def.Prefab;
                Debug.Log($"[BoardView] Loaded Entity {modelId} via EntityDefinitionSO '{mappedPath}'");
                return def.Prefab;
            }
        }

        // 3. Resources/Entity/ 직접 로드
        foreach (var dp in new[] { $"Entity/Entity_{modelId}", $"Entity/{modelId}" })
        {
            var dp2 = Resources.Load<GameObject>(dp);
            if (dp2 != null)
            {
                _entityPrefabCache[modelId] = dp2;
                Debug.Log($"[BoardView] Loaded Entity {modelId} from 'Resources/{dp}'");
                return dp2;
            }
        }

        // 4. Inspector 폴백
        GameObject fallback = entityType switch
        {
            (int)EntityType.Player  => playerPrefab,
            (int)EntityType.Monster => monsterPrefab,
            (int)EntityType.Object  => monsterPrefab,
            _                       => monsterPrefab
        };
        if (fallback != null)
        {
            Debug.Log($"[BoardView] Entity {modelId} (type={entityType}) → Inspector fallback '{fallback.name}'");
            return fallback;
        }

        Debug.LogError($"[BoardView] ❌ No prefab for EntityType={entityType} ModelId={modelId}.");
        return null;
    }

    private Vector3 GridToWorld(int x, int y)
    {
        float targetX = x * cellSize;
        float targetZ = y * cellSize;
        return new Vector3(targetX, GetGroundHeight(targetX, targetZ), targetZ);
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
        int w = _tiles.GetLength(0), h = _tiles.GetLength(1);
        if (destroyGameObjects)
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (_tiles[x, y] != null) Destroy(_tiles[x, y]);
        _tiles = null;
        _baseTileColors = null;
    }

    #endregion
}

using UnityEngine;
using System.Collections.Generic;

public class BoardView : MonoBehaviour, IClientWorldView
{
    public static BoardView Instance { get; private set; }

    [Header("Prefabs (자동 생성/할당 가능)")]
    public GameObject playerPrefab;
    public GameObject monsterPrefab;

    public GameObject tilePrefab;
    public AppearanceAutoTilePalette appearancePalette;
    
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
    private Color[,] _logicTileColors;
    private AppearanceTileCell[,] _appearanceTiles;
    private Material _defaultTileBaseMaterial;

    private static readonly Color TELEGRAPH_COLOR = new Color(1f, 0.08f, 0f, 0.45f);

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

        if (TryBindAvailableTilesFromScene(width, height, out int boundCount, out int missingCount))
        {
            Debug.LogWarning(
                $"[BoardView] OnCreateMap: Baked tiles partially bound. " +
                $"Bound={boundCount}, Missing={missingCount}, Expected={width}x{height}. Existing scene tiles were preserved.");
            return;
        }

        Debug.LogWarning($"[BoardView] OnCreateMap: No baked tiles found. Fallback to Instantiate. ({width}x{height})");

        if (tilePrefab == null)
        {
            Debug.LogWarning("[BoardView] tilePrefab이 설정되지 않음. 타일을 생성하지 않습니다. 기존 씬 타일은 유지합니다.");
            return;
        }

        ClearTiles(destroyGameObjects: true);
        _tiles = new GameObject[width, height];
        _baseTileColors = new Color[width, height];
        _logicTileColors = new Color[width, height];
        _appearanceTiles = new AppearanceTileCell[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = Instantiate(tilePrefab, transform);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.position = GridToWorld(x, y) + new Vector3(0, -2, 0);
                _tiles[x, y] = tile;
                _baseTileColors[x, y] = Color.gray;
                _logicTileColors[x, y] = Color.gray;
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

    public Color GetAppearanceTileColor(int appearanceKind, int appearanceVariant)
    {
        AppearanceTileKind kind = (AppearanceTileKind)appearanceKind;
        Color baseColor = appearancePalette != null
            ? appearancePalette.GetPreviewColor(kind)
            : AppearanceAutoTilePalette.GetBuiltInPreviewColor(kind);

        float connectionWeight = CountBits((byte)appearanceVariant) / 8f;
        return Color.Lerp(baseColor, Color.white, connectionWeight * 0.12f);
    }

    private Renderer GetTileRenderer(GameObject tile)
    {
        var visual = GetTileVisual(tile);
        return visual != null ? visual.BaseRenderer : null;
    }

    private BoardTileVisual GetTileVisual(GameObject tile)
    {
        if (tile == null)
            return null;

        if (!tile.TryGetComponent<BoardTileVisual>(out var visual))
            visual = tile.AddComponent<BoardTileVisual>();

        return visual;
    }

    private Material GetDefaultTileBaseMaterial()
    {
        if (_defaultTileBaseMaterial != null)
            return _defaultTileBaseMaterial;

        var renderer = BoardTileVisual.FindBaseRenderer(tilePrefab);
        _defaultTileBaseMaterial = renderer != null ? renderer.sharedMaterial : null;
        return _defaultTileBaseMaterial;
    }

    public void OnSetTile(int x, int y, int tileKind)
    {
        if (_tiles == null) return;
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1)) return;

        if (_logicTileColors != null)
            _logicTileColors[x, y] = GetTileColor(tileKind);

        ApplyTileVisual(x, y, updateTopSurface: false);
    }

    public void OnSetAppearancePalette(AppearanceAutoTilePalette palette)
    {
        if (palette == null)
            return;

        appearancePalette = palette;

        if (_tiles == null)
            return;

        int width = _tiles.GetLength(0);
        int height = _tiles.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (_appearanceTiles != null && _appearanceTiles[x, y].Kind != AppearanceTileKind.None)
                    ApplyTileVisual(x, y);
            }
        }
    }

    public void OnSetAppearanceTile(int x, int y, int appearanceKind, int appearanceVariant)
    {
        if (_tiles == null) return;
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1)) return;

        if (_appearanceTiles != null)
        {
            _appearanceTiles[x, y] = new AppearanceTileCell
            {
                Kind = (AppearanceTileKind)appearanceKind,
                Variant = (byte)appearanceVariant
            };
        }

        ApplyTileVisual(x, y);
    }

    private void ApplyTileVisual(int x, int y, bool updateTopSurface = true)
    {
        var tile = _tiles[x, y];
        if (tile == null) return;

        Color logicColor = _logicTileColors != null ? _logicTileColors[x, y] : Color.gray;
        AppearanceTileCell appearance = _appearanceTiles != null ? _appearanceTiles[x, y] : default;
        Color finalColor = ComposeTileColor(logicColor, appearance);

        if (_baseTileColors != null)
            _baseTileColors[x, y] = finalColor;

        var visual = GetTileVisual(tile);
        if (visual != null)
        {
            visual.SetBaseMaterial(GetDefaultTileBaseMaterial());

            if (TryGetAppearanceMaterial(appearance, out var material))
            {
                visual.SetBaseColor(logicColor);
                visual.SetTopMaterial(material);
            }
            else
            {
                if (updateTopSurface)
                    visual.HideTopSurface();
                visual.SetBaseColor(finalColor);
            }
        }
    }

    private Color ComposeTileColor(Color logicColor, AppearanceTileCell appearance)
    {
        if (appearance.Kind == AppearanceTileKind.None)
            return logicColor;

        Color appearanceColor = GetAppearanceTileColor((int)appearance.Kind, appearance.Variant);
        return Color.Lerp(logicColor, appearanceColor, 0.75f);
    }

    private bool TryGetAppearanceMaterial(AppearanceTileCell appearance, out Material material)
    {
        if (appearance.Kind != AppearanceTileKind.None && appearancePalette != null)
            return appearancePalette.TryGetMaterial(appearance.Kind, appearance.Variant, out material);

        material = null;
        return false;
    }

    private int CountBits(byte value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
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

        var visual = GetTileVisual(tile);
        var rend = visual != null ? visual.BaseRenderer : null;
        if (rend == null)
        {
            Debug.LogWarning($"[BoardView] SetTelegraphOverlay: Renderer missing at ({x},{y}) Obj={tile.name}");
            return;
        }

        if (on)
        {
            visual.ShowWarningOverlay(TELEGRAPH_COLOR);
        }
        else
        {
            visual.HideWarningOverlay();
            ApplyTileVisual(x, y, updateTopSurface: false);
        }
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

        ApplyTileVisual(x, y);
    }

    public bool TryBindTilesFromScene(int width, int height)
    {
        var map = CollectSceneTilesByName();

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

        AllocateTileState(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                BindTileState(x, y, map[(x, y)]);
            }
        }

        Debug.Log($"[BoardView] Bind Success (NameBased): {width}x{height}");
        return true;
    }

    private bool TryBindAvailableTilesFromScene(int width, int height, out int boundCount, out int missingCount)
    {
        boundCount = 0;
        missingCount = 0;

        var map = CollectSceneTilesByName();
        if (map.Count == 0)
        {
            missingCount = width * height;
            return false;
        }

        AllocateTileState(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (map.TryGetValue((x, y), out var tile) && tile != null)
                {
                    BindTileState(x, y, tile);
                    boundCount++;
                }
                else
                {
                    _baseTileColors[x, y] = Color.gray;
                    _logicTileColors[x, y] = Color.gray;
                    missingCount++;
                }
            }
        }

        return boundCount > 0;
    }

    private Dictionary<(int x, int y), GameObject> CollectSceneTilesByName()
    {
        var map = new Dictionary<(int x, int y), GameObject>();

        foreach (Transform child in transform)
        {
            if (ParseTileName(child.name, out int x, out int y))
                map[(x, y)] = child.gameObject;
        }

        return map;
    }

    private void AllocateTileState(int width, int height)
    {
        ClearTiles(destroyGameObjects: false);
        _tiles = new GameObject[width, height];
        _baseTileColors = new Color[width, height];
        _logicTileColors = new Color[width, height];
        _appearanceTiles = new AppearanceTileCell[width, height];
    }

    private void BindTileState(int x, int y, GameObject tile)
    {
        _tiles[x, y] = tile;
        var rend = GetTileRenderer(tile);
        if (rend != null)
        {
            _baseTileColors[x, y] = rend.sharedMaterial != null ? rend.sharedMaterial.color : Color.gray;
            _logicTileColors[x, y] = _baseTileColors[x, y];
        }
        else
        {
            Debug.LogWarning($"[BoardView] Bound tile at ({x},{y}) has no renderer!");
            _baseTileColors[x, y] = Color.gray;
            _logicTileColors[x, y] = Color.gray;
        }
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
        bool createdNow = false;
        if (!_entityViews.TryGetValue(info.EntityId, out var visual) || visual == null)
        {
            GameObject prefab = ChoosePrefab(info.EntityType, info.AppearanceId);
            if (prefab == null)
            {
                Debug.LogWarning($"[BoardView] EntityType {info.EntityType}용 Prefab이 없음 (AppearanceId={info.AppearanceId})");
                if (info.EntityType == (int)EntityType.Player)
                    Debug.LogWarning($"[P2PPlayerSync] Player visual spawn failed actor={info.EntityId} reason=MissingPrefab app={info.AppearanceId}");
                return;
            }

            GameObject go = Instantiate(prefab, transform);
            go.name = $"Entity_{info.EntityId}";

            if (!go.TryGetComponent<EntityVisual>(out visual))
                visual = go.AddComponent<EntityVisual>();

            _entityViews[info.EntityId] = visual;
            createdNow = true;
            if (info.EntityType == (int)EntityType.Player)
            {
                bool isLocal = ClientGameState.Instance != null && info.EntityId == ClientGameState.Instance.MyActorId;
                Debug.Log(
                    $"[P2PPlayerSync] Player visual created actor={info.EntityId} local={isLocal} " +
                    $"prefab={prefab.name} pos=({info.X},{info.Y}) app={info.AppearanceId}");
            }

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

            if (createdNow)
            {
                string controllerState = RhythmInputController.Instance != null
                    ? RhythmInputController.Instance.GetDebugState()
                    : "<controller-missing>";
                Debug.Log($"[BoardView] Local player bound actor={info.EntityId} target={visual.gameObject.name} controller={controllerState}");
            }
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
            bool hadPrediction = _recentPredictedMoves.TryGetValue(action.ActorId, out var predictedMove);
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
            if (hadPrediction
                && predictedMove.predictedTile.x == action.ToX
                && predictedMove.predictedTile.y == action.ToY
                && Vector3.Distance(visual.transform.position, serverToW) <= snapThreshold)
            {
                visual.transform.position = serverToW;
                visual.SetRotation(action.Rotation);
                return;
            }

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

    public bool TryGetPredictedMoveTile(int actorId, out Vector2Int tile)
    {
        if (_recentPredictedMoves.TryGetValue(actorId, out var predictedMove))
        {
            tile = predictedMove.predictedTile;
            return true;
        }

        tile = default;
        return false;
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
        if (P2PDebugConfig.TraceCombat)
        {
            Debug.Log($"[AnimPlay] actor={actorId} skill={skillId} isMine={isMine} startTick={startTick} " +
                      $"curTick={curTick} tickGap={tickGap} rot={rotation:F0} rtt={TimeSync.EstimatedRttMs:F0}ms");
        }
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
    /// 우선순위: 1) AppearanceCatalog 경로 매핑 (플레이어 외견)
    ///          2) EntityData.json 매핑 → EntityDefinitionSO (몬스터/오브젝트)
    ///          3) Resources/Entity/ 직접 로드 (Entity_{id} 규칙)
    ///          4) Inspector playerPrefab / monsterPrefab 폴백
    /// </summary>
    private GameObject ChoosePrefab(int entityType, int modelId)
    {
        if (_entityPrefabCache.TryGetValue(modelId, out var cached))
            return cached;

        // 1. AppearanceCatalog 경로 매핑 (플레이어 외견 ID 우선)
        if (AppearanceCatalog.TryGetDefinitionPath(modelId, out var appearancePath))
        {
            var def = Resources.Load<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(appearancePath);
            if (def != null && def.Prefab != null)
            {
                _entityPrefabCache[modelId] = def.Prefab;
                Debug.Log($"[BoardView] ✅ AppearanceId={modelId} → EntityDefinitionSO '{appearancePath}'");
                return def.Prefab;
            }

            Debug.LogWarning($"[BoardView] AppearanceId={modelId} 매핑 '{appearancePath}' 프리팹 없음. 폴백 진행.");
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

        if (TryGetTileTopFromRendererBounds(x, z, out float tileTopY))
            return tileTopY;

        Debug.LogWarning($"[GetGroundHeight] Miss! ({x}, {z}) -> Defaulting to 0");
        return 0f;
    }

    private bool TryGetTileTopFromRendererBounds(float x, float z, out float topY)
    {
        topY = 0f;

        if (_tiles == null)
            return false;

        int tileX = Mathf.RoundToInt(x / cellSize);
        int tileY = Mathf.RoundToInt(z / cellSize);
        if (tileX < 0 || tileY < 0 || tileX >= _tiles.GetLength(0) || tileY >= _tiles.GetLength(1))
            return false;

        var tile = _tiles[tileX, tileY];
        if (tile == null)
            return false;

        var renderer = GetTileRenderer(tile);
        if (renderer == null)
            return false;

        topY = renderer.bounds.max.y;
        return true;
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
        _logicTileColors = null;
        _appearanceTiles = null;
    }

    #endregion
}

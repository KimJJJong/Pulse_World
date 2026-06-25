using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using RhythmRPG.Game.Stage;

public class BoardView : MonoBehaviour, IClientWorldView, IClientWorldViewMapUpdateBatch
{
    public static BoardView Instance { get; private set; }

    [Header("Prefabs (자동 생성/할당 가능)")]
    public GameObject playerPrefab;
    public GameObject monsterPrefab;

    public GameObject tilePrefab;
    public AppearanceAutoTilePalette appearancePalette;

    [Header("Scene Hierarchy Roots")]
    [SerializeField] private Transform bakedTileRoot;
    [SerializeField] private Transform entityRoot;
    [SerializeField] private Transform skillRunnerRoot;
    
    // Runtime Cache
    private Dictionary<int, GameObject> _entityPrefabCache = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, EntityAnimationProfile> _entityAnimationProfileCache = new();
    private readonly Dictionary<int, string> _entityNameMap = new();
    private readonly Dictionary<int, int> _entityMaxHpMap = new();
    private readonly Dictionary<int, RemoteEquipmentState> _remoteEquipmentByActorId = new();
    private readonly HashSet<int> _remoteEquipmentRequests = new();

    [Header("Rendering")]
    public float cellSize = 1.0f;
    public float moveLerpTime = 0.1f;

    [Header("Monster Health UI")]
    [SerializeField] private bool monsterHealthBarsEnabled = true;
    [SerializeField, Min(1)] private int normalMonsterHealthVisibleBeats = 8;

    [Header("Walkable Grid")]
    [SerializeField] private bool showWalkableGrid = true;
    [SerializeField] private bool walkableGridBeatPulse = true;
    [SerializeField] private bool playWalkableGridWaveOnCombat = true;
    [SerializeField, Range(0.005f, 0.08f)] private float walkableGridLineWidth = 0.018f;
    [SerializeField, Range(0f, 0.2f)] private float walkableGridHeightOffset = 0.045f;
    [SerializeField] private Color walkableGridColor = new Color(0.42f, 0.92f, 1f, 0.22f);
    [SerializeField] private Color walkableGridBeatFlashColor = new Color(0.88f, 1f, 1f, 0.42f);
    [SerializeField, Range(0f, 1f)] private float walkableGridBeatFlashStrength = 0.22f;
    [SerializeField, Min(0.1f)] private float walkableGridBeatFlashSharpness = 4.5f;
    [SerializeField, Min(0.25f)] private float walkableGridPlayerPulseRadiusTiles = 3f;
    [SerializeField] private Color walkableGridWaveColor = new Color(0.78f, 1f, 1f, 0.95f);
    [SerializeField, Min(0.05f)] private float walkableGridWaveDuration = 0.55f;
    [SerializeField, Min(0.25f)] private float walkableGridWaveRadiusTiles = 5f;
    [SerializeField, Min(0.05f)] private float walkableGridWaveWidthTiles = 0.7f;
    [SerializeField, Range(15f, 60f)] private float walkableGridEffectRefreshRate = 30f;

    [Header("Tile Distance Culling")]
    [SerializeField] private bool tileDistanceCullingEnabled = true;
    [SerializeField, Min(1f)] private float tileVisibleDistance = 48f;
    [SerializeField, Min(0f)] private float tileCullingHysteresis = 10f;
    [SerializeField, Min(0f)] private float tileCameraForwardLookahead = 28f;
    [SerializeField, Min(0.05f)] private float tileCullingRefreshInterval = 0.1f;
    [SerializeField, Min(1)] private int tileCullingChecksPerRefresh = 4096;
    [SerializeField] private bool tileCullingPreferLocalPlayer = true;

    // entityId -> EntityVisual
    private readonly Dictionary<int, EntityVisual> _entityViews = new();
    private readonly Dictionary<int, int> _entityVisualAppearanceIds = new();
    private const string EntityAnimationProfileResourceRoot = "Data/EntityAnimationProfiles";
    private const float RemoteEquipmentRefreshIntervalSeconds = 2f;

    // (x,y) -> Tile GameObject
    private GameObject[,] _tiles;
    private BoardTileVisual[,] _tileVisuals;

    private Color[,] _baseTileColors;
    private Color[,] _logicTileColors;
    private TileKind[,] _logicTileKinds;
    private AppearanceTileCell[,] _appearanceTiles;
    private Material _defaultTileBaseMaterial;
    private bool[,] _tileCullingVisible;
    private bool _tileCullingAnyHidden;
    private int _tileCullingCursor;
    private float _nextTileCullingRefreshTime;
    private readonly Vector3[] _tileCullingReferenceBuffer = new Vector3[3];
    private int _mapVisualUpdateBatchDepth;

    private static readonly Color TELEGRAPH_COLOR = new Color(1f, 0.08f, 0f, 0.45f);
    private static readonly Color PLAYER_TELEGRAPH_COLOR = new Color(0.05f, 0.55f, 1f, 0.45f);

    private const int MaxPredictedMovesPerActor = 8;
    private const int DecoyEntityIdBase = 700000;
    private const int DecoyEntityIdLimit = DecoyEntityIdBase + 100000;
    private const float DecoyOwnerVisualAlpha = 0.58f;

    private struct PredictedMove
    {
        public long BeatIndex;
        public Vector2Int PredictedTile;
    }

    private struct WalkableGridTile
    {
        public Vector2 Coord;
        public int VertexStart;
        public int VertexCount;
    }

    private struct WalkableGridWave
    {
        public Vector2 Origin;
        public float StartTime;
        public float Duration;
        public float RadiusTiles;
        public float WidthTiles;
        public Color Color;
    }

    private struct MonsterHealthPresentation
    {
        public string DisplayName;
        public string MonsterType;
        public bool IsElite;
        public int MaxHp;
    }

    private sealed class PendingVisualDespawn
    {
        public readonly EntityVisual Visual;
        public readonly Coroutine Coroutine;

        public PendingVisualDespawn(EntityVisual visual, Coroutine coroutine)
        {
            Visual = visual;
            Coroutine = coroutine;
        }
    }

    private Dictionary<int, float> _recentInstantActions = new();
    private Dictionary<int, List<PredictedMove>> _recentPredictedMoves = new();
    private Dictionary<int, ClientSkillRunner> _activeSkillRunners = new();
    private readonly Dictionary<int, MonsterHealthBarView> _monsterHealthBars = new();
    private readonly Dictionary<int, PendingVisualDespawn> _pendingVisualDespawns = new();
    private readonly HashSet<int> _activeDecoyEntityIds = new();
    private readonly Dictionary<int, int> _activeDecoyCountByOwner = new();

    private sealed class RemoteEquipmentState
    {
        public string Uid = string.Empty;
        public List<int> EquipmentTemplateIds = new();
        public int AppearanceId;
        public float FetchedAtRealtime;
    }
    private Dictionary<Vector2Int, long> _telegraphExpiration = new Dictionary<Vector2Int, long>();
    private readonly List<Vector2Int> _expiredTelegraphBuffer = new List<Vector2Int>();
    private const string WalkableGridObjectName = "__WalkableGridOutline";
    private GameObject _walkableGridObject;
    private MeshFilter _walkableGridMeshFilter;
    private MeshRenderer _walkableGridRenderer;
    private Mesh _walkableGridMesh;
    private Material _walkableGridMaterial;
    private Color32[] _walkableGridColors;
    private bool _walkableGridDirty;
    private bool _walkableGridTintActive;
    private float _walkableGridTintStartTime;
    private float _walkableGridTintDuration;
    private Color _walkableGridTintColor;
    private readonly List<WalkableGridTile> _walkableGridTiles = new List<WalkableGridTile>();
    private readonly List<WalkableGridWave> _walkableGridWaves = new List<WalkableGridWave>();
    private readonly List<Vector3> _walkableGridVertices = new List<Vector3>(4096);
    private readonly List<int> _walkableGridTriangles = new List<int>(6144);
    private readonly List<Color32> _walkableGridBuildColors = new List<Color32>(4096);
    private readonly HashSet<Vector3Int> _walkableGridDrawnEdges = new HashSet<Vector3Int>();
    private float _nextWalkableGridEffectTime;

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
            _expiredTelegraphBuffer.Clear();

            foreach (var kv in _telegraphExpiration)
            {
                if (currentBeat >= kv.Value)
                    _expiredTelegraphBuffer.Add(kv.Key);
            }

            if (_expiredTelegraphBuffer.Count > 0)
            {
                foreach (var pos in _expiredTelegraphBuffer)
                {
                    _telegraphExpiration.Remove(pos);
                    SetTelegraphOverlay(pos.x, pos.y, false);
                }
            }
        }

        if (_mapVisualUpdateBatchDepth <= 0)
            UpdateTileDistanceCulling();

        UpdateWalkableGridEffects();
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
                    if (!string.IsNullOrWhiteSpace(e.Name))
                        _entityNameMap[e.EntityId] = e.Name;
                    if (e.MaxHp > 0)
                        _entityMaxHpMap[e.EntityId] = e.MaxHp;
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
    class EntityDataDTO
    {
        public int EntityId;
        public string Name;
        public int EntityType;
        public int MaxHp;
        public string ResourcePath;
    }

    #region IClientWorldView

    public void OnCreateMap(int width, int height)
    {
        if (TryBindTilesFromScene(width, height))
        {
            Debug.Log($"[BoardView] OnCreateMap: Baked tiles bound. ({width}x{height})");
            RefreshTileCullingWhenReady();
            return;
        }

        if (TryBindAvailableTilesFromScene(width, height, out int boundCount, out int missingCount))
        {
            Debug.LogWarning(
                $"[BoardView] OnCreateMap: Baked tiles partially bound. " +
                $"Bound={boundCount}, Missing={missingCount}, Expected={width}x{height}. Existing scene tiles were preserved.");
            RefreshTileCullingWhenReady();
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
        _logicTileKinds = new TileKind[width, height];
        _appearanceTiles = new AppearanceTileCell[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = Instantiate(tilePrefab, GetTileRoot());
                tile.name = $"Tile_{x}_{y}";
                tile.transform.position = GridToWorld(x, y) + new Vector3(0, -2, 0);
                _tiles[x, y] = tile;
                _baseTileColors[x, y] = Color.gray;
                _logicTileColors[x, y] = Color.gray;
            }
        }

        RefreshTileCullingWhenReady();
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

    public void ConfigureSceneRoots(Transform tilesRoot, Transform entitiesRoot, Transform skillsRoot)
    {
        bakedTileRoot = tilesRoot;
        entityRoot = entitiesRoot;
        skillRunnerRoot = skillsRoot;
    }

    private Transform GetTileRoot() => bakedTileRoot != null ? bakedTileRoot : transform;

    private Transform GetEntityRoot() => entityRoot != null ? entityRoot : transform;

    private Transform GetSkillRunnerRoot() => skillRunnerRoot != null ? skillRunnerRoot : transform;

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

    private BoardTileVisual GetTileVisualAt(int x, int y)
    {
        if (_tiles == null || _tileVisuals == null)
            return null;

        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1))
            return null;

        var visual = _tileVisuals[x, y];
        if (visual != null)
            return visual;

        visual = GetTileVisual(_tiles[x, y]);
        _tileVisuals[x, y] = visual;
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
        if (_logicTileKinds != null)
            _logicTileKinds[x, y] = (TileKind)tileKind;

        ApplyTileVisual(x, y, updateTopSurface: false);
        MarkWalkableGridDirty();
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

        var visual = GetTileVisualAt(x, y);
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
        SetTelegraphOverlay(x, y, on, TELEGRAPH_COLOR);
    }

    public void SetTelegraphOverlay(int x, int y, bool on, Color color)
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

        var visual = GetTileVisualAt(x, y);
        var rend = visual != null ? visual.BaseRenderer : null;
        if (rend == null)
        {
            Debug.LogWarning($"[BoardView] SetTelegraphOverlay: Renderer missing at ({x},{y}) Obj={tile.name}");
            return;
        }

        if (on)
        {
            visual.ShowWarningOverlay(color);
        }
        else
        {
            visual.HideWarningOverlay();
            ApplyTileVisual(x, y, updateTopSurface: false);
        }
    }

    public void SetTelegraphWithExpire(int x, int y, long expireBeat)
    {
        SetTelegraphWithExpire(x, y, expireBeat, TELEGRAPH_COLOR);
    }

    public void SetTelegraphWithExpire(int x, int y, long expireBeat, Color color)
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
        SetTelegraphOverlay(x, y, true, color);
    }

    public Color GetTelegraphColorForActor(int actorId)
    {
        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetEntity(actorId, out var info))
            return info.EntityType == (int)EntityType.Player ? PLAYER_TELEGRAPH_COLOR : TELEGRAPH_COLOR;

        return TELEGRAPH_COLOR;
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

    public void SetWalkableGridVisible(bool visible)
    {
        showWalkableGrid = visible;

        if (_walkableGridRenderer != null)
            _walkableGridRenderer.enabled = visible && _walkableGridMesh != null && _walkableGridMesh.vertexCount > 0;

        if (visible)
            MarkWalkableGridDirty();
    }

    public void PlayWalkableGridWave(int originX, int originY)
    {
        PlayWalkableGridWave(originX, originY, walkableGridWaveColor, walkableGridWaveRadiusTiles, walkableGridWaveDuration);
    }

    public void PlayWalkableGridWave(int originX, int originY, Color color)
    {
        PlayWalkableGridWave(originX, originY, color, walkableGridWaveRadiusTiles, walkableGridWaveDuration);
    }

    public void PlayWalkableGridWave(int originX, int originY, Color color, float radiusTiles, float duration)
    {
        if (!showWalkableGrid)
            return;

        _walkableGridWaves.Add(new WalkableGridWave
        {
            Origin = GetGridTileCenter(originX, originY),
            StartTime = Time.time,
            Duration = Mathf.Max(0.05f, duration),
            RadiusTiles = Mathf.Max(0.25f, radiusTiles),
            WidthTiles = Mathf.Max(0.05f, walkableGridWaveWidthTiles),
            Color = color
        });
    }

    public void FlashWalkableGrid(Color color, float duration)
    {
        if (!showWalkableGrid)
            return;

        _walkableGridTintActive = true;
        _walkableGridTintStartTime = Time.time;
        _walkableGridTintDuration = Mathf.Max(0.05f, duration);
        _walkableGridTintColor = color;
    }

    private void MarkWalkableGridDirty()
    {
        _walkableGridDirty = true;
    }

    private void UpdateWalkableGridEffects()
    {
        if (_mapVisualUpdateBatchDepth > 0)
            return;

        if (_walkableGridDirty)
            RebuildWalkableGridMesh();

        if (!showWalkableGrid || _walkableGridMesh == null || _walkableGridColors == null || _walkableGridTiles.Count == 0)
            return;

        bool hadWaves = _walkableGridWaves.Count > 0;
        bool hadTint = _walkableGridTintActive;
        RemoveExpiredWalkableGridWaves();

        Vector2 playerPulseCenter = default;
        bool hasBeatPulse = walkableGridBeatPulse
                            && RhythmClient.Instance != null
                            && RhythmClient.Instance.ServerSongStartMs > 0
                            && TryGetLocalPlayerGridPosition(out playerPulseCenter);
        bool hasTint = IsWalkableGridTintActive();

        if (!hasBeatPulse && !hasTint && _walkableGridWaves.Count == 0 && !hadWaves && !hadTint)
            return;

        float now = Time.time;
        float refreshInterval = 1f / Mathf.Max(15f, walkableGridEffectRefreshRate);
        if (now < _nextWalkableGridEffectTime)
            return;

        _nextWalkableGridEffectTime = now + refreshInterval;

        Color baseColor = EvaluateWalkableGridBaseColor(hasTint);
        for (int i = 0; i < _walkableGridTiles.Count; i++)
        {
            WalkableGridTile tile = _walkableGridTiles[i];
            Color color = EvaluateWalkableGridTileColor(tile.Coord, baseColor, hasBeatPulse, playerPulseCenter);
            ApplyWalkableGridTileColor(tile, color);
        }

        _walkableGridMesh.colors32 = _walkableGridColors;
    }

    private void RebuildWalkableGridMesh()
    {
        _walkableGridDirty = false;
        _walkableGridTiles.Clear();

        if (!showWalkableGrid || _tiles == null || _logicTileKinds == null)
        {
            ClearWalkableGridMesh();
            return;
        }

        EnsureWalkableGridRenderer();

        _walkableGridVertices.Clear();
        _walkableGridTriangles.Clear();
        _walkableGridBuildColors.Clear();
        _walkableGridDrawnEdges.Clear();
        Color baseColor = EvaluateWalkableGridBaseColor(IsWalkableGridTintActive());

        int width = _tiles.GetLength(0);
        int height = _tiles.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsWalkableTileKind(_logicTileKinds[x, y]))
                    continue;

                GameObject tile = _tiles[x, y];
                Renderer renderer = GetTileRenderer(tile);
                if (renderer == null)
                    continue;

                AddWalkableGridEdgesForTile(
                    x,
                    y,
                    renderer.bounds,
                    _walkableGridDrawnEdges,
                    _walkableGridVertices,
                    _walkableGridTriangles,
                    _walkableGridBuildColors,
                    baseColor);
            }
        }

        if (_walkableGridMesh == null)
            _walkableGridMesh = new Mesh { name = "BoardWalkableGridOutlineMesh" };

        _walkableGridMesh.Clear();
        _walkableGridMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _walkableGridMesh.SetVertices(_walkableGridVertices);
        _walkableGridMesh.SetTriangles(_walkableGridTriangles, 0);
        _walkableGridMesh.SetColors(_walkableGridBuildColors);
        _walkableGridMesh.RecalculateBounds();
        _walkableGridMeshFilter.sharedMesh = _walkableGridMesh;
        if (_walkableGridColors == null || _walkableGridColors.Length != _walkableGridBuildColors.Count)
            _walkableGridColors = new Color32[_walkableGridBuildColors.Count];
        _walkableGridBuildColors.CopyTo(_walkableGridColors);

        if (_walkableGridRenderer != null)
            _walkableGridRenderer.enabled = showWalkableGrid && _walkableGridVertices.Count > 0;
    }

    private void AddWalkableGridEdgesForTile(
        int x,
        int y,
        Bounds bounds,
        HashSet<Vector3Int> drawnEdges,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color32> colors,
        Color color)
    {
        float minSize = Mathf.Min(bounds.size.x, bounds.size.z);
        float lineWidth = Mathf.Min(Mathf.Max(0.001f, walkableGridLineWidth), minSize * 0.35f);
        float halfLine = lineWidth * 0.5f;
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minZ = bounds.min.z;
        float maxZ = bounds.max.z;
        float yTop = bounds.max.y + walkableGridHeightOffset;

        if (maxX <= minX || maxZ <= minZ)
            return;

        halfLine = Mathf.Min(halfLine, (maxX - minX) * 0.225f, (maxZ - minZ) * 0.225f);

        AddWalkableGridEdge(
            new Vector3Int(x, y, 0),
            new Vector2(x + 0.5f, y),
            minX,
            minZ - halfLine,
            maxX,
            minZ + halfLine,
            yTop,
            drawnEdges,
            vertices,
            triangles,
            colors,
            color);

        AddWalkableGridEdge(
            new Vector3Int(x, y + 1, 0),
            new Vector2(x + 0.5f, y + 1f),
            minX,
            maxZ - halfLine,
            maxX,
            maxZ + halfLine,
            yTop,
            drawnEdges,
            vertices,
            triangles,
            colors,
            color);

        AddWalkableGridEdge(
            new Vector3Int(x, y, 1),
            new Vector2(x, y + 0.5f),
            minX - halfLine,
            minZ,
            minX + halfLine,
            maxZ,
            yTop,
            drawnEdges,
            vertices,
            triangles,
            colors,
            color);

        AddWalkableGridEdge(
            new Vector3Int(x + 1, y, 1),
            new Vector2(x + 1f, y + 0.5f),
            maxX - halfLine,
            minZ,
            maxX + halfLine,
            maxZ,
            yTop,
            drawnEdges,
            vertices,
            triangles,
            colors,
            color);
    }

    private void AddWalkableGridEdge(
        Vector3Int key,
        Vector2 coord,
        float minX,
        float minZ,
        float maxX,
        float maxZ,
        float y,
        HashSet<Vector3Int> drawnEdges,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color32> colors,
        Color color)
    {
        if (!drawnEdges.Add(key))
            return;

        int vertexStart = vertices.Count;
        AddWalkableGridQuad(minX, minZ, maxX, maxZ, y, vertices, triangles, colors, color);

        int vertexCount = vertices.Count - vertexStart;
        if (vertexCount > 0)
        {
            _walkableGridTiles.Add(new WalkableGridTile
            {
                Coord = coord,
                VertexStart = vertexStart,
                VertexCount = vertexCount
            });
        }
    }

    private void AddWalkableGridQuad(
        float minX,
        float minZ,
        float maxX,
        float maxZ,
        float y,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color32> colors,
        Color color)
    {
        int start = vertices.Count;
        vertices.Add(transform.InverseTransformPoint(new Vector3(minX, y, minZ)));
        vertices.Add(transform.InverseTransformPoint(new Vector3(maxX, y, minZ)));
        vertices.Add(transform.InverseTransformPoint(new Vector3(maxX, y, maxZ)));
        vertices.Add(transform.InverseTransformPoint(new Vector3(minX, y, maxZ)));

        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 0);
        triangles.Add(start + 3);
        triangles.Add(start + 2);

        Color32 color32 = color;
        colors.Add(color32);
        colors.Add(color32);
        colors.Add(color32);
        colors.Add(color32);
    }

    private void EnsureWalkableGridRenderer()
    {
        if (_walkableGridObject == null)
        {
            Transform existing = transform.Find(WalkableGridObjectName);
            _walkableGridObject = existing != null ? existing.gameObject : new GameObject(WalkableGridObjectName);
            _walkableGridObject.transform.SetParent(transform, false);
            _walkableGridObject.transform.localPosition = Vector3.zero;
            _walkableGridObject.transform.localRotation = Quaternion.identity;
            _walkableGridObject.transform.localScale = Vector3.one;
        }

        if (!_walkableGridObject.TryGetComponent(out _walkableGridMeshFilter))
            _walkableGridMeshFilter = _walkableGridObject.AddComponent<MeshFilter>();

        if (!_walkableGridObject.TryGetComponent(out _walkableGridRenderer))
            _walkableGridRenderer = _walkableGridObject.AddComponent<MeshRenderer>();

        if (_walkableGridMesh == null)
            _walkableGridMesh = new Mesh { name = "BoardWalkableGridOutlineMesh" };

        _walkableGridMeshFilter.sharedMesh = _walkableGridMesh;
        _walkableGridRenderer.sharedMaterial = GetWalkableGridMaterial();
        _walkableGridRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _walkableGridRenderer.receiveShadows = false;
    }

    private Material GetWalkableGridMaterial()
    {
        if (_walkableGridMaterial != null)
            return _walkableGridMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        _walkableGridMaterial = new Material(shader)
        {
            name = "BoardWalkableGrid_Runtime",
            renderQueue = 3050,
            color = Color.white
        };

        _walkableGridMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _walkableGridMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _walkableGridMaterial.SetInt("_ZWrite", 0);
        _walkableGridMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _walkableGridMaterial.SetFloat("_Surface", 1f);
        _walkableGridMaterial.EnableKeyword("_ALPHABLEND_ON");
        _walkableGridMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return _walkableGridMaterial;
    }

    private void ClearWalkableGridMesh()
    {
        _walkableGridTiles.Clear();
        _walkableGridColors = null;

        if (_walkableGridMesh != null)
            _walkableGridMesh.Clear();

        if (_walkableGridRenderer != null)
            _walkableGridRenderer.enabled = false;
    }

    private void RemoveExpiredWalkableGridWaves()
    {
        float now = Time.time;
        for (int i = _walkableGridWaves.Count - 1; i >= 0; i--)
        {
            WalkableGridWave wave = _walkableGridWaves[i];
            if (now - wave.StartTime >= wave.Duration)
                _walkableGridWaves.RemoveAt(i);
        }
    }

    private bool IsWalkableGridTintActive()
    {
        if (!_walkableGridTintActive)
            return false;

        if (Time.time - _walkableGridTintStartTime <= _walkableGridTintDuration)
            return true;

        _walkableGridTintActive = false;
        return false;
    }

    private Color EvaluateWalkableGridBaseColor(bool hasTint)
    {
        Color color = walkableGridColor;

        if (hasTint)
        {
            float elapsed = Time.time - _walkableGridTintStartTime;
            float fade = 1f - Mathf.Clamp01(elapsed / _walkableGridTintDuration);
            color = BlendWalkableGridColor(color, _walkableGridTintColor, fade);
        }

        return color;
    }

    private Color EvaluateWalkableGridTileColor(
        Vector2 coord,
        Color baseColor,
        bool hasBeatPulse,
        Vector2 playerPulseCenter)
    {
        Color color = baseColor;
        float now = Time.time;

        if (hasBeatPulse)
        {
            float distance = Vector2.Distance(coord, playerPulseCenter);
            float radius = Mathf.Max(0.25f, walkableGridPlayerPulseRadiusTiles);
            float falloff = 1f - Mathf.Clamp01(distance / radius);

            if (falloff > 0f)
            {
                float progress = (float)RhythmClient.Instance.GetCurrentBeatProgress01();
                float pulse = Mathf.Pow(1f - progress, walkableGridBeatFlashSharpness)
                              * walkableGridBeatFlashStrength
                              * Mathf.SmoothStep(0f, 1f, falloff);
                color = BlendWalkableGridColor(color, walkableGridBeatFlashColor, pulse);
            }
        }

        for (int i = 0; i < _walkableGridWaves.Count; i++)
        {
            WalkableGridWave wave = _walkableGridWaves[i];
            float progress = Mathf.Clamp01((now - wave.StartTime) / wave.Duration);
            float radius = progress * wave.RadiusTiles;
            float distance = Vector2.Distance(coord, wave.Origin);
            float band = 1f - Mathf.Abs(distance - radius) / wave.WidthTiles;
            if (band <= 0f)
                continue;

            float intensity = Mathf.SmoothStep(0f, 1f, band) * (1f - progress);
            color = BlendWalkableGridColor(color, wave.Color, intensity);
        }

        return color;
    }

    private void ApplyWalkableGridTileColor(WalkableGridTile tile, Color color)
    {
        Color32 color32 = color;
        int end = Mathf.Min(tile.VertexStart + tile.VertexCount, _walkableGridColors.Length);
        for (int i = tile.VertexStart; i < end; i++)
            _walkableGridColors[i] = color32;
    }

    private Color BlendWalkableGridColor(Color baseColor, Color overlayColor, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color(
            Mathf.Lerp(baseColor.r, overlayColor.r, amount),
            Mathf.Lerp(baseColor.g, overlayColor.g, amount),
            Mathf.Lerp(baseColor.b, overlayColor.b, amount),
            Mathf.Clamp01(baseColor.a + overlayColor.a * amount));
    }

    private bool IsWalkableTileKind(TileKind kind)
    {
        return kind == TileKind.Floor || kind == TileKind.Spawn;
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
                    GameObject fallbackTile = CreateMissingSceneTile(x, y);
                    if (fallbackTile != null)
                    {
                        BindTileState(x, y, fallbackTile);
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
        }

        return boundCount > 0;
    }

    private GameObject CreateMissingSceneTile(int x, int y)
    {
        if (tilePrefab == null)
            return null;

        var tile = Instantiate(tilePrefab, GetTileRoot());
        tile.name = $"Tile_{x}_{y}";
        tile.transform.position = new Vector3(x * cellSize, -2f, y * cellSize);
        return tile;
    }

    private Dictionary<(int x, int y), GameObject> CollectSceneTilesByName()
    {
        Transform tileRoot = GetTileRoot();
        var map = new Dictionary<(int x, int y), GameObject>(tileRoot != null ? tileRoot.childCount : 0);

        foreach (Transform child in tileRoot)
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
        _tileVisuals = new BoardTileVisual[width, height];
        _baseTileColors = new Color[width, height];
        _logicTileColors = new Color[width, height];
        _logicTileKinds = new TileKind[width, height];
        _appearanceTiles = new AppearanceTileCell[width, height];
        _tileCullingVisible = new bool[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                _tileCullingVisible[x, y] = true;
        }

        _tileCullingAnyHidden = false;
        _tileCullingCursor = 0;
        _nextTileCullingRefreshTime = 0f;
        MarkWalkableGridDirty();
    }

    private void BindTileState(int x, int y, GameObject tile)
    {
        _tiles[x, y] = tile;
        var visual = GetTileVisual(tile);
        _tileVisuals[x, y] = visual;
        var rend = visual != null ? visual.BaseRenderer : null;
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
        foreach (int ownerActorId in _activeDecoyCountByOwner.Keys)
            ApplyDecoyOwnerVisualAlpha(ownerActorId, false);
        _activeDecoyEntityIds.Clear();
        _activeDecoyCountByOwner.Clear();

        foreach (var kv in _pendingVisualDespawns)
        {
            var pending = kv.Value;
            if (pending == null)
                continue;

            if (pending.Coroutine != null)
                StopCoroutine(pending.Coroutine);

            if (pending.Visual != null)
                Destroy(pending.Visual.gameObject);
        }
        _pendingVisualDespawns.Clear();

        foreach (var kv in _entityViews)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
        _entityViews.Clear();
        _entityVisualAppearanceIds.Clear();
        _monsterHealthBars.Clear();
        _remoteEquipmentByActorId.Clear();
        _remoteEquipmentRequests.Clear();
    }

    public void OnSpawnOrUpdateEntity(ClientEntityInfo info)
    {
        CancelPendingVisualDespawn(info.EntityId);
        var gameState = ClientGameState.Instance;

        _entityViews.TryGetValue(info.EntityId, out var visual);
        if (visual != null
            && _entityVisualAppearanceIds.TryGetValue(info.EntityId, out int visualAppearanceId)
            && visualAppearanceId != info.AppearanceId)
        {
            Destroy(visual.gameObject);
            _entityViews.Remove(info.EntityId);
            _entityVisualAppearanceIds.Remove(info.EntityId);
            visual = null;
        }

        bool createdNow = false;
        if (visual == null)
        {
            GameObject prefab = ChoosePrefab(info.EntityType, info.AppearanceId);
            if (prefab == null)
            {
                Debug.LogWarning($"[BoardView] EntityType {info.EntityType}용 Prefab이 없음 (AppearanceId={info.AppearanceId})");
                if (info.EntityType == (int)EntityType.Player)
                    Debug.LogWarning($"[P2PPlayerSync] Player visual spawn failed actor={info.EntityId} reason=MissingPrefab app={info.AppearanceId}");
                return;
            }

            GameObject go = Instantiate(prefab, GetEntityRoot());
            go.name = $"Entity_{info.EntityId}";

            if (!go.TryGetComponent<EntityVisual>(out visual))
                visual = go.AddComponent<EntityVisual>();

            EntityAnimationProfile animationProfile = ResolveEntityAnimationProfile(info.EntityType, info.AppearanceId, prefab);
            if (animationProfile != null)
                visual.BindAnimationProfile(animationProfile);

            _entityViews[info.EntityId] = visual;
            _entityVisualAppearanceIds[info.EntityId] = info.AppearanceId;
            createdNow = true;
            if (info.EntityType == (int)EntityType.Player)
            {
                bool isLocal = gameState != null && info.EntityId == gameState.MyActorId;
                Debug.Log(
                    $"[P2PPlayerSync] Player visual created actor={info.EntityId} local={isLocal} " +
                    $"prefab={prefab.name} pos=({info.X},{info.Y}) app={info.AppearanceId}");
            }
        }

        SyncCharacterVisualController(info, visual, gameState);

        if (gameState != null && info.EntityId == gameState.MyActorId)
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

            RefreshTileCullingWhenReady();
        }

        visual.transform.position = ResolveEntityWorldPosition(info, visual.transform);
        if (info.EntityType == (int)EntityType.Object)
        {
            Vector3 euler = visual.transform.eulerAngles;
            visual.transform.rotation = Quaternion.Euler(euler.x, info.Rotation, euler.z);
        }
        else
        {
            visual.SetRotation(info.Rotation);
        }

        if (createdNow)
        {
            BindStageSceneObjectTargets(info, visual);
            CameraObstacleFadeRuntimeTargets.EnsureForEntity(info, visual.transform);
        }

        RegisterDecoyVisualIfNeeded(info.EntityId);
        ApplyExistingDecoyFadeIfNeeded(info.EntityId, visual);
        SyncMonsterHealthBar(info, visual);
    }

    private static void BindStageSceneObjectTargets(ClientEntityInfo info, EntityVisual visual)
    {
        if (visual == null || info.EntityType != (int)EntityType.Object)
            return;

        var targets = visual.GetComponentsInChildren<StageSceneObjectTarget>(true);
        if (targets == null || targets.Length == 0)
            return;

        foreach (var target in targets)
        {
            if (target != null)
                target.BindRuntimeTarget(info.GroupId);
        }
    }

    private void SyncCharacterVisualController(ClientEntityInfo info, EntityVisual visual, ClientGameState gameState)
    {
        if (info.EntityType != (int)EntityType.Player || visual == null || gameState == null)
            return;

        var visualCtrl = visual.GetComponent<RhythmRPG.Visual.CharacterVisualController>();
        if (visualCtrl == null)
            visualCtrl = visual.GetComponentInChildren<RhythmRPG.Visual.CharacterVisualController>(true);
        if (visualCtrl == null)
            return;

        if (visualCtrl.CurrentContext != RhythmRPG.Visual.CharacterContext.Game)
            visualCtrl.SetContext(RhythmRPG.Visual.CharacterContext.Game);

        bool isLocal = info.EntityId == gameState.MyActorId;
        if (visualCtrl.IsLocalPlayer != isLocal)
            visualCtrl.SetLocalPlayer(isLocal);

        if (!isLocal)
            SyncRemotePlayerEquipment(info.EntityId, visualCtrl, gameState);
    }

    private void SyncRemotePlayerEquipment(int actorId, RhythmRPG.Visual.CharacterVisualController visualCtrl, ClientGameState gameState)
    {
        if (visualCtrl == null || gameState == null)
            return;

        if (!gameState.TryGetPlayerUid(actorId, out string uid) || string.IsNullOrWhiteSpace(uid))
            return;

        if (_remoteEquipmentByActorId.TryGetValue(actorId, out var cached)
            && cached != null
            && string.Equals(cached.Uid, uid, StringComparison.OrdinalIgnoreCase))
        {
            visualCtrl.UpdateEquipments(cached.EquipmentTemplateIds);
            if (cached.AppearanceId > 0)
                gameState.ApplyPlayerAppearance(actorId, cached.AppearanceId);

            if (Time.realtimeSinceStartup - cached.FetchedAtRealtime < RemoteEquipmentRefreshIntervalSeconds)
                return;
        }

        if (_remoteEquipmentRequests.Contains(actorId))
            return;

        _remoteEquipmentRequests.Add(actorId);
        FetchRemotePlayerEquipmentAsync(actorId, uid);
    }

    private async void FetchRemotePlayerEquipmentAsync(int actorId, string uid)
    {
        try
        {
            var api = AppBootstrap.Instance?.Root?.PlayerStateApi;
            if (api == null || ClientGameState.Instance == null)
                return;

            var result = await api.GetPlayerStateAsync(uid);
            if (!result.Ok || result.Data == null || ClientGameState.Instance == null)
                return;

            if (!ClientGameState.Instance.TryGetPlayerUid(actorId, out string currentUid)
                || !string.Equals(currentUid, uid, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var equipmentIds = new List<int>();
            var gears = result.Data.Gears;
            if (gears != null)
            {
                for (int i = 0; i < gears.Length; i++)
                {
                    int templateId = gears[i]?.TemplateId ?? 0;
                    if (templateId > 0)
                        equipmentIds.Add(templateId);
                }
            }

            _remoteEquipmentByActorId[actorId] = new RemoteEquipmentState
            {
                Uid = uid,
                EquipmentTemplateIds = equipmentIds,
                AppearanceId = result.Data.AppearanceId,
                FetchedAtRealtime = Time.realtimeSinceStartup
            };

            if (result.Data.AppearanceId > 0)
                ClientGameState.Instance.ApplyPlayerAppearance(actorId, result.Data.AppearanceId);

            if (_entityViews.TryGetValue(actorId, out var visual) && visual != null)
            {
                var visualCtrl = visual.GetComponent<RhythmRPG.Visual.CharacterVisualController>();
                if (visualCtrl == null)
                    visualCtrl = visual.GetComponentInChildren<RhythmRPG.Visual.CharacterVisualController>(true);

                if (visualCtrl != null && !visualCtrl.IsLocalPlayer)
                    visualCtrl.UpdateEquipments(equipmentIds);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BoardView] Remote equipment load failed actor={actorId} uid={uid}: {ex.Message}");
        }
        finally
        {
            _remoteEquipmentRequests.Remove(actorId);
        }
    }

    public bool HasEntityView(int entityId)
        => _entityViews.TryGetValue(entityId, out var visual) && visual != null;

    public bool TryGetEntityView(int entityId, out EntityVisual visual)
        => _entityViews.TryGetValue(entityId, out visual) && visual != null;

    public void OnDespawnEntity(int entityId)
    {
        UnregisterDecoyVisualIfNeeded(entityId);
        RemoveMonsterHealthBar(entityId);

        if (_entityViews.TryGetValue(entityId, out var visual) && visual != null)
        {
            float delay = visual.IsDead
                ? visual.GetRemainingDeathDelaySeconds()
                : visual.PlayDeath();

            if (delay > 0.05f && visual.HasAnimator)
                _pendingVisualDespawns[entityId] = new PendingVisualDespawn(
                    visual,
                    StartCoroutine(CoDestroyVisualAfterDelay(entityId, visual, delay)));
            else
                Destroy(visual.gameObject);
        }

        _entityViews.Remove(entityId);
        _entityVisualAppearanceIds.Remove(entityId);
        _remoteEquipmentByActorId.Remove(entityId);
        _remoteEquipmentRequests.Remove(entityId);
        _recentInstantActions.Remove(entityId);
        ClearPredictedMoves(entityId);

        if (_activeSkillRunners.TryGetValue(entityId, out var runner) && runner != null)
            Destroy(runner.gameObject);
        _activeSkillRunners.Remove(entityId);
    }

    private void RegisterDecoyVisualIfNeeded(int entityId)
    {
        if (!TryGetDecoyOwnerActorId(entityId, out int ownerActorId) || !_activeDecoyEntityIds.Add(entityId))
            return;

        _activeDecoyCountByOwner.TryGetValue(ownerActorId, out int count);
        _activeDecoyCountByOwner[ownerActorId] = count + 1;
        ApplyDecoyOwnerVisualAlpha(ownerActorId, true);
    }

    private void UnregisterDecoyVisualIfNeeded(int entityId)
    {
        if (!TryGetDecoyOwnerActorId(entityId, out int ownerActorId) || !_activeDecoyEntityIds.Remove(entityId))
            return;

        if (_activeDecoyCountByOwner.TryGetValue(ownerActorId, out int count) && count > 1)
        {
            _activeDecoyCountByOwner[ownerActorId] = count - 1;
            return;
        }

        _activeDecoyCountByOwner.Remove(ownerActorId);
        ApplyDecoyOwnerVisualAlpha(ownerActorId, false);
    }

    private void ApplyExistingDecoyFadeIfNeeded(int entityId, EntityVisual visual)
    {
        if (visual == null)
            return;

        if (_activeDecoyCountByOwner.TryGetValue(entityId, out int count) && count > 0)
            visual.SetVisualAlpha(DecoyOwnerVisualAlpha);
    }

    private void ApplyDecoyOwnerVisualAlpha(int ownerActorId, bool active)
    {
        if (!_entityViews.TryGetValue(ownerActorId, out var ownerVisual) || ownerVisual == null)
            return;

        ownerVisual.SetVisualAlpha(active ? DecoyOwnerVisualAlpha : 1f);
    }

    private static bool TryGetDecoyOwnerActorId(int entityId, out int ownerActorId)
    {
        ownerActorId = 0;
        if (entityId < DecoyEntityIdBase || entityId >= DecoyEntityIdLimit)
            return false;

        ownerActorId = entityId - DecoyEntityIdBase;
        return ownerActorId > 0;
    }

    private void SyncMonsterHealthBar(ClientEntityInfo info, EntityVisual visual)
    {
        if (!monsterHealthBarsEnabled || info.EntityType != (int)EntityType.Monster || visual == null)
        {
            RemoveMonsterHealthBar(info.EntityId);
            return;
        }

        MonsterHealthPresentation presentation = ResolveMonsterHealthPresentation(info);
        int maxHp = ResolveMonsterMaxHp(info, presentation.MaxHp);
        MonsterHealthBarView bar = MonsterHealthBarView.GetOrCreate(visual.transform);
        if (bar == null)
            return;

        _monsterHealthBars[info.EntityId] = bar;
        bar.Bind(
            visual.transform,
            presentation.DisplayName,
            presentation.IsElite,
            info.Hp,
            maxHp,
            normalMonsterHealthVisibleBeats);
    }

    private void ShowMonsterHealthBarForDamage(int entityId, int newHp, EntityVisual visual)
    {
        if (!monsterHealthBarsEnabled || visual == null)
            return;

        if (ClientGameState.Instance == null || !ClientGameState.Instance.TryGetEntity(entityId, out var info))
            return;

        if (info.EntityType != (int)EntityType.Monster)
            return;

        info.Hp = newHp;
        MonsterHealthPresentation presentation = ResolveMonsterHealthPresentation(info);
        int maxHp = ResolveMonsterMaxHp(info, presentation.MaxHp);
        MonsterHealthBarView bar = MonsterHealthBarView.GetOrCreate(visual.transform);
        if (bar == null)
            return;

        _monsterHealthBars[entityId] = bar;
        bar.Bind(
            visual.transform,
            presentation.DisplayName,
            presentation.IsElite,
            newHp,
            maxHp,
            normalMonsterHealthVisibleBeats);
        bar.ShowForHit(newHp, maxHp);
    }

    private void RemoveMonsterHealthBar(int entityId)
    {
        if (!_monsterHealthBars.TryGetValue(entityId, out var bar))
            return;

        if (bar != null)
            Destroy(bar.gameObject);

        _monsterHealthBars.Remove(entityId);
    }

    private MonsterHealthPresentation ResolveMonsterHealthPresentation(ClientEntityInfo info)
    {
        string monsterType = "";
        int directorMaxHp = 0;

        if (P2PContentDirector.HasInstance)
            P2PContentDirector.Instance.TryGetMonsterPresentation(info, out monsterType, out directorMaxHp);

        _entityNameMap.TryGetValue(info.AppearanceId, out string entityName);

        bool isElite = ContainsElite(monsterType) || ContainsElite(entityName);
        string displayName = ResolveMonsterDisplayName(info.AppearanceId, entityName, monsterType, isElite);

        return new MonsterHealthPresentation
        {
            DisplayName = displayName,
            MonsterType = monsterType,
            IsElite = isElite,
            MaxHp = directorMaxHp
        };
    }

    private int ResolveMonsterMaxHp(ClientEntityInfo info, int directorMaxHp)
    {
        if (info.MaxHp > 0)
            return info.MaxHp;

        if (directorMaxHp > 0)
            return directorMaxHp;

        if (_entityMaxHpMap.TryGetValue(info.AppearanceId, out int dataMaxHp) && dataMaxHp > 0)
            return dataMaxHp;

        return Mathf.Max(1, info.Hp);
    }

    private static string ResolveMonsterDisplayName(int appearanceId, string entityName, string monsterType, bool isElite)
    {
        string source = !string.IsNullOrWhiteSpace(monsterType) && !string.Equals(monsterType, "Default", StringComparison.OrdinalIgnoreCase)
            ? monsterType
            : entityName;

        string displayName = HumanizeMonsterIdentifier(source, appearanceId);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "Monster";

        if (isElite && displayName.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) < 0)
            displayName = $"Elite {displayName}";

        return displayName;
    }

    private static string HumanizeMonsterIdentifier(string value, int appearanceId)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        int slashIndex = value.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex + 1 < value.Length)
            value = value.Substring(slashIndex + 1);

        string[] parts = value.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> words = new List<string>(parts.Length);
        string appearanceToken = appearanceId.ToString();

        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i].Trim();
            if (token.Length == 0
                || token.Equals("Entity", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Monster", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Enemy", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Elite", StringComparison.OrdinalIgnoreCase)
                || token.Equals(appearanceToken, StringComparison.OrdinalIgnoreCase)
                || int.TryParse(token, out _))
            {
                continue;
            }

            string expanded = InsertWordBreaks(token);
            if (!string.IsNullOrWhiteSpace(expanded))
                words.Add(expanded);
        }

        return words.Count > 0 ? string.Join(" ", words) : "";
    }

    private static string InsertWordBreaks(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "";

        System.Text.StringBuilder sb = new System.Text.StringBuilder(token.Length + 4);
        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (i > 0 && char.IsUpper(c))
            {
                char previous = token[i - 1];
                bool nextIsLower = i + 1 < token.Length && char.IsLower(token[i + 1]);
                if (char.IsLower(previous) || char.IsDigit(previous) || nextIsLower)
                    sb.Append(' ');
            }

            sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    private static bool ContainsElite(string value)
        => !string.IsNullOrWhiteSpace(value)
           && value.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0;

    public void PlayEntityDamageFeedback(int entityId, int oldHp, int newHp)
    {
        if (oldHp <= 0 || newHp >= oldHp)
            return;

        if (!_entityViews.TryGetValue(entityId, out var visual) || visual == null)
            return;

        ShowMonsterHealthBarForDamage(entityId, newHp, visual);
        int damage = oldHp - newHp;
        PlayMonsterDamageEffect(entityId, damage, visual);
        PlayPlayerDamageEffect(entityId, damage, oldHp, newHp, visual);

        if (newHp <= 0)
            visual.PlayDeath();
        else
            visual.PlayHit(ResolveHitFeedbackDuration());
    }

    private void PlayMonsterDamageEffect(int entityId, int damage, EntityVisual visual)
    {
        if (damage <= 0 || visual == null || ClientGameState.Instance == null)
            return;

        if (!ClientGameState.Instance.TryGetEntity(entityId, out var info)
            || info.EntityType != (int)EntityType.Monster)
        {
            return;
        }

        MonsterDamagePopupView.Play(visual.transform, damage);
    }

    private void PlayPlayerDamageEffect(int entityId, int damage, int oldHp, int newHp, EntityVisual visual)
    {
        if (damage <= 0 || visual == null || ClientGameState.Instance == null)
            return;

        if (entityId != ClientGameState.Instance.MyActorId)
            return;

        if (!ClientGameState.Instance.TryGetEntity(entityId, out var info)
            || info.EntityType != (int)EntityType.Player)
        {
            return;
        }

        PlayerHitSfxOverlay.Play(visual.transform, damage, oldHp, newHp);
    }

    private float ResolveHitFeedbackDuration()
    {
        const float fallbackDuration = 0.25f;

        if (RhythmClient.Instance == null)
            return fallbackDuration;

        double beatMs = RhythmClient.Instance.GetBeatDurationMs();
        if (beatMs <= 0)
            return fallbackDuration;

        return Mathf.Clamp((float)(beatMs / 1000.0) * actionDurationRatio, 0.08f, 0.35f);
    }

    private IEnumerator CoDestroyVisualAfterDelay(int entityId, EntityVisual visual, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, delay));

        if (visual != null)
            Destroy(visual.gameObject);

        if (_pendingVisualDespawns.TryGetValue(entityId, out var pending) && pending.Visual == visual)
            _pendingVisualDespawns.Remove(entityId);
    }

    private void CancelPendingVisualDespawn(int entityId)
    {
        if (!_pendingVisualDespawns.TryGetValue(entityId, out var pending))
            return;

        if (pending.Coroutine != null)
            StopCoroutine(pending.Coroutine);

        if (pending.Visual != null)
            Destroy(pending.Visual.gameObject);

        _pendingVisualDespawns.Remove(entityId);
    }

    [Header("Sync")]
    [Range(0.1f, 2.0f)]
    public float actionDurationRatio = 0.5f;
    [SerializeField, Range(0.02f, 0.2f)] private float remoteMoveMinCatchUpDuration = 0.06f;
    [SerializeField, Range(0f, 0.25f)] private float remoteMoveStartGraceSeconds = 0.03f;

    public void OnBeatAction(ClientBeatAction action, ClientEntityInfo entity)
    {
        if (!_entityViews.TryGetValue(action.ActorId, out var visual) || visual == null)
            return;

        double beatMs   = RhythmClient.Instance.GetBeatDurationMs();
        float  duration = (float)(beatMs / 1000.0) * actionDurationRatio;

        if (action.ActionKind == (int)ActionKind.Move)
        {
            bool hadPrediction = TryConsumePredictedMove(action.ActorId, action, out var predictedMove);

            Vector3 serverFromW = GridToWorld(action.FromX, action.FromY);
            Vector3 serverToW   = GridToWorld(action.ToX,   action.ToY);

            if (!action.Accepted)
            {
                ClearPredictedMoves(action.ActorId);
                visual.PlayBumpBack(serverToW, serverFromW);
                return;
            }

            int distanceTiles = Mathf.RoundToInt(Vector3.Distance(serverFromW, serverToW) / cellSize);
            float moveDuration = distanceTiles > 1 ? duration * 0.4f : duration;

            if (hadPrediction)
            {
                bool predictedTargetMatched = predictedMove.PredictedTile.x == action.ToX
                                           && predictedMove.PredictedTile.y == action.ToY;
                if (predictedTargetMatched)
                {
                    visual.SetRotation(action.Rotation);
                    return;
                }

                ClearPredictedMoves(action.ActorId);
            }

            moveDuration = ResolveRemoteMoveDuration(action, moveDuration);

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

        if (action.ActionKind == (int)ActionKind.Interact)
        {
            bool isMine = ClientGameState.Instance != null
                       && action.ActorId == ClientGameState.Instance.MyActorId;

            if (playWalkableGridWaveOnCombat)
                PlayWalkableGridWave(action.ToX, action.ToY);

            visual.PlaySkill("Interact", duration * 0.35f, isMine);
            visual.SetRotation(action.Rotation);
            return;
        }

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
            if (playWalkableGridWaveOnCombat)
                PlayWalkableGridWave(entity.X, entity.Y);

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
        {
            visual.PlaySkill(duration, isMine);

            if (playWalkableGridWaveOnCombat && TryGetEntityGridPosition(actorId, out Vector2Int origin))
                PlayWalkableGridWave(origin.x, origin.y);
        }

        visual.SetRotation(rotation);
        _recentInstantActions[actorId] = Time.time;
    }

    private float ResolveRemoteMoveDuration(ClientBeatAction action, float baseDuration)
    {
        if (baseDuration <= remoteMoveMinCatchUpDuration)
            return baseDuration;

        if (action.BeatIndex < 0
            || RhythmClient.Instance == null
            || RhythmClient.Instance.ServerSongStartMs <= 0)
            return baseDuration;

        if (ClientGameState.Instance != null
            && action.ActorId == ClientGameState.Instance.MyActorId)
            return baseDuration;

        long beatTimeMs = RhythmClient.Instance.GetBeatTimeMs(action.BeatIndex);
        long nowMs = RhythmClient.Instance.GetCurrentServerTimeMs();
        float elapsed = Mathf.Max(0f, (nowMs - beatTimeMs) / 1000f - remoteMoveStartGraceSeconds);
        if (elapsed <= 0f)
            return baseDuration;

        return Mathf.Clamp(baseDuration - elapsed, remoteMoveMinCatchUpDuration, baseDuration);
    }

    public bool TryGetPredictedMoveTile(int actorId, out Vector2Int tile)
    {
        if (_recentPredictedMoves.TryGetValue(actorId, out var predictedMoves)
            && predictedMoves.Count > 0)
        {
            tile = predictedMoves[predictedMoves.Count - 1].PredictedTile;
            return true;
        }

        tile = default;
        return false;
    }

    public void PlayMovePrediction(int actorId, int toX, int toY, float durationRatio, long beatIndex = -1)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null) return;

        Vector3 curW = visual.transform.position;
        Vector3 toW  = GridToWorldPublic(toX, toY);

        double beatMs  = RhythmClient.Instance.GetBeatDurationMs();
        float  duration = (float)(beatMs / 1000.0) * durationRatio;

        visual.StartMove(curW, toW, duration);
        AddPredictedMove(actorId, beatIndex, new Vector2Int(toX, toY));
    }

    private void AddPredictedMove(int actorId, long beatIndex, Vector2Int predictedTile)
    {
        if (!_recentPredictedMoves.TryGetValue(actorId, out var predictedMoves))
        {
            predictedMoves = new List<PredictedMove>();
            _recentPredictedMoves[actorId] = predictedMoves;
        }

        predictedMoves.Add(new PredictedMove
        {
            BeatIndex = beatIndex,
            PredictedTile = predictedTile
        });

        while (predictedMoves.Count > MaxPredictedMovesPerActor)
            predictedMoves.RemoveAt(0);
    }

    private bool TryConsumePredictedMove(
        int actorId,
        ClientBeatAction action,
        out PredictedMove predictedMove)
    {
        predictedMove = default;

        if (!_recentPredictedMoves.TryGetValue(actorId, out var predictedMoves)
            || predictedMoves.Count == 0)
            return false;

        int matchIndex = -1;
        if (action.BeatIndex >= 0)
        {
            for (int i = 0; i < predictedMoves.Count; i++)
            {
                if (predictedMoves[i].BeatIndex == action.BeatIndex)
                {
                    matchIndex = i;
                    break;
                }
            }
        }

        if (matchIndex < 0)
        {
            for (int i = 0; i < predictedMoves.Count; i++)
            {
                var candidate = predictedMoves[i];
                if (candidate.PredictedTile.x == action.ToX
                    && candidate.PredictedTile.y == action.ToY)
                {
                    matchIndex = i;
                    break;
                }
            }
        }

        if (matchIndex < 0)
            return false;

        predictedMove = predictedMoves[matchIndex];
        predictedMoves.RemoveRange(0, matchIndex + 1);

        if (predictedMoves.Count == 0)
            _recentPredictedMoves.Remove(actorId);

        return true;
    }

    private void ClearPredictedMoves(int actorId)
    {
        _recentPredictedMoves.Remove(actorId);
    }

    public void PlaySkillInstant(int actorId, string skillId, float rotation, long startTick)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null)
        {
            Debug.LogWarning($"[AnimPlay] MISS actor={actorId} skill={skillId} — no visual registered");
            return;
        }

        bool isMine = (ClientGameState.Instance != null && actorId == ClientGameState.Instance.MyActorId);

        if (!_activeSkillRunners.TryGetValue(actorId, out var runner) || runner == null)
        {
            GameObject go = new GameObject($"SkillRunner_{actorId}_{skillId}");
            go.transform.SetParent(GetSkillRunnerRoot());
            runner = go.AddComponent<ClientSkillRunner>();
            _activeSkillRunners[actorId] = runner;
        }
        else
        {
            runner.gameObject.name = $"SkillRunner_{actorId}_{skillId}";
        }

        if (runner.Initialize(this, actorId, visual, skillId, startTick, isMine, rotation))
            _recentInstantActions[actorId] = Time.time;

        visual.SetRotation(rotation);

        if (playWalkableGridWaveOnCombat && TryGetEntityGridPosition(actorId, out Vector2Int origin))
            PlayWalkableGridWave(origin.x, origin.y);

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
        Color color = GetTelegraphColorForActor(actorId);
        foreach (var cell in cells)
            SetTelegraphWithExpire(cell.x, cell.y, expireBeat, color);
    }

    public bool IsActorRunningNewSkill(int actorId)
    {
        if (_activeSkillRunners.TryGetValue(actorId, out var runner))
            return runner != null;
        return false;
    }

    public void NotifySkillRunnerStopped(int actorId, ClientSkillRunner runner)
    {
        if (_activeSkillRunners.TryGetValue(actorId, out var activeRunner) && activeRunner == runner)
            _activeSkillRunners.Remove(actorId);
    }

    public void OnInitGameCompleted()
    {
        Debug.Log("[BoardView] InitGameCompleted");
        EndMapVisualUpdate();
    }

    public void BeginMapVisualUpdate(int expectedTileCount)
    {
        _mapVisualUpdateBatchDepth++;
    }

    public void EndMapVisualUpdate()
    {
        if (_mapVisualUpdateBatchDepth > 0)
            _mapVisualUpdateBatchDepth--;

        if (_mapVisualUpdateBatchDepth > 0)
            return;

        MarkWalkableGridDirty();
        RefreshTileCullingWhenReady();
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

    private EntityAnimationProfile ResolveEntityAnimationProfile(int entityType, int modelId, GameObject prefab)
    {
        int profileId = entityType == (int)EntityType.Player && modelId <= 0
            ? 10
            : modelId;

        if (profileId > 0 && _entityAnimationProfileCache.TryGetValue(profileId, out var cached))
            return cached;

        EntityAnimationProfile profile = null;
        if (profileId > 0)
            profile = Resources.Load<EntityAnimationProfile>($"{EntityAnimationProfileResourceRoot}/Entity_{profileId}");

        if (profile == null && prefab != null)
            profile = Resources.Load<EntityAnimationProfile>($"{EntityAnimationProfileResourceRoot}/{prefab.name}");

        if (profileId > 0)
            _entityAnimationProfileCache[profileId] = profile;

        return profile;
    }

    private bool TryGetEntityGridPosition(int actorId, out Vector2Int position)
    {
        position = default;

        if (ClientGameState.Instance == null)
            return false;

        if (!ClientGameState.Instance.TryGetEntity(actorId, out var info))
            return false;

        position = new Vector2Int(info.X, info.Y);
        return true;
    }

    private bool TryGetLocalPlayerGridPosition(out Vector2 position)
    {
        position = default;

        if (ClientGameState.Instance == null)
            return false;

        int actorId = ClientGameState.Instance.MyActorId;
        if (actorId <= 0)
            return false;

        if (TryGetPredictedMoveTile(actorId, out Vector2Int predictedTile))
        {
            position = GetGridTileCenter(predictedTile.x, predictedTile.y);
            return true;
        }

        if (!TryGetEntityGridPosition(actorId, out Vector2Int tile))
            return false;

        position = GetGridTileCenter(tile.x, tile.y);
        return true;
    }

    private Vector2 GetGridTileCenter(int x, int y)
    {
        return new Vector2(x + 0.5f, y + 0.5f);
    }

    /// <summary>
    /// AppearanceId로 프리팹을 결정합니다.
    /// 우선순위: 1) AppearanceCatalog 경로 매핑 (플레이어 외견)
    ///          2) EntityData.json 매핑 → EntityDefinitionSO (몬스터/오브젝트)
    ///          3) Resources/Entity/ 직접 로드 (Entity_{id} 규칙)
    ///          4) Inspector playerPrefab / monsterPrefab 폴백
    /// </summary>
    private GameObject ChoosePrefab(int entityType, int modelId)
    {
        if (entityType == (int)EntityType.Player && modelId <= 0)
            modelId = 10;

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

    private Vector3 ResolveEntityWorldPosition(ClientEntityInfo info, Transform visualTransform)
    {
        Vector3 position = GridToWorld(info.X, info.Y);

        if (info.EntityType != (int)EntityType.Object || visualTransform == null)
            return position;

        int sizeX = Mathf.Max(1, info.SizeX);
        int sizeY = Mathf.Max(1, info.SizeY);
        position.x += (sizeX - 1) * cellSize * 0.5f;
        position.z += (sizeY - 1) * cellSize * 0.5f;

        visualTransform.position = position;
        if (TryGetObjectGroundingBounds(visualTransform, out Bounds bounds))
        {
            float yCorrection = position.y - bounds.min.y;
            if (Mathf.Abs(yCorrection) > 0.0001f)
                position.y += yCorrection;
        }

        return position;
    }

    private static bool TryGetObjectGroundingBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        bool hasBounds = false;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || !renderer.enabled || ShouldIgnoreForObjectGrounding(renderer))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool ShouldIgnoreForObjectGrounding(Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return true;

        string objectName = renderer.gameObject.name;
        return objectName.StartsWith("FX_", System.StringComparison.OrdinalIgnoreCase)
               || objectName.IndexOf("Glow", System.StringComparison.OrdinalIgnoreCase) >= 0
               || objectName.IndexOf("EmissionShell", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private float GetGroundHeight(float x, float z)
    {
        if (TryGetTileTopFromRendererBounds(x, z, out float tileTopY))
            return tileTopY;

        Ray ray = new Ray(new Vector3(x, 20f, z), Vector3.down);
        var groundMask = Physics.DefaultRaycastLayers;
        var wallLayer = LayerMask.NameToLayer("Wall");
        if (wallLayer >= 0)
            groundMask &= ~(1 << wallLayer);

        if (Physics.Raycast(ray, out RaycastHit hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

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
        _tileVisuals = null;
        _baseTileColors = null;
        _logicTileColors = null;
        _logicTileKinds = null;
        _appearanceTiles = null;
        _tileCullingVisible = null;
        _tileCullingAnyHidden = false;
        _tileCullingCursor = 0;
        ClearWalkableGridMesh();
        MarkWalkableGridDirty();
    }

    private void RefreshTileCullingWhenReady()
    {
        if (_mapVisualUpdateBatchDepth > 0)
            return;

        ForceRefreshTileCulling();
    }

    private void UpdateTileDistanceCulling()
    {
        if (_tiles == null || _tileCullingVisible == null)
            return;

        if (!tileDistanceCullingEnabled)
        {
            if (_tileCullingAnyHidden)
                RestoreAllTileCulling();
            return;
        }

        if (Time.unscaledTime < _nextTileCullingRefreshTime)
            return;

        if (!TryBuildTileCullingReferences(out int referenceCount))
            return;

        _nextTileCullingRefreshTime = Time.unscaledTime + tileCullingRefreshInterval;

        int width = _tiles.GetLength(0);
        int height = _tiles.GetLength(1);
        int total = width * height;
        int checks = Mathf.Min(Mathf.Max(1, tileCullingChecksPerRefresh), total);

        for (int i = 0; i < checks; i++)
        {
            if (_tileCullingCursor >= total)
                _tileCullingCursor = 0;

            int x = _tileCullingCursor % width;
            int y = _tileCullingCursor / width;
            UpdateTileCullingVisibility(x, y, _tileCullingReferenceBuffer, referenceCount);
            _tileCullingCursor++;
        }
    }

    private void ForceRefreshTileCulling()
    {
        if (_tiles == null || _tileCullingVisible == null)
            return;

        if (!tileDistanceCullingEnabled)
        {
            RestoreAllTileCulling();
            return;
        }

        if (!TryBuildTileCullingReferences(out int referenceCount))
            return;

        int width = _tiles.GetLength(0);
        int height = _tiles.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                UpdateTileCullingVisibility(x, y, _tileCullingReferenceBuffer, referenceCount, force: true);
        }

        _tileCullingCursor = 0;
        _nextTileCullingRefreshTime = Time.unscaledTime + tileCullingRefreshInterval;
    }

    private void UpdateTileCullingVisibility(
        int x,
        int y,
        Vector3[] referencePositions,
        int referenceCount,
        bool force = false)
    {
        GameObject tile = _tiles[x, y];
        if (tile == null)
            return;

        bool currentlyVisible = _tileCullingVisible[x, y];
        Vector3 tilePosition = tile.transform.position;
        float threshold = tileVisibleDistance + (currentlyVisible ? tileCullingHysteresis : 0f);
        bool shouldBeVisible = IsWithinAnyTileCullingReference(
            tilePosition,
            referencePositions,
            referenceCount,
            threshold);

        if (!force && currentlyVisible == shouldBeVisible)
            return;

        _tileCullingVisible[x, y] = shouldBeVisible;

        var visual = GetTileVisualAt(x, y);
        if (visual != null)
            visual.SetCullingVisible(shouldBeVisible);

        if (!shouldBeVisible)
            _tileCullingAnyHidden = true;
    }

    private void RestoreAllTileCulling()
    {
        if (_tiles == null || _tileCullingVisible == null)
            return;

        int width = _tiles.GetLength(0);
        int height = _tiles.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (_tileCullingVisible[x, y])
                    continue;

                _tileCullingVisible[x, y] = true;
                var visual = GetTileVisualAt(x, y);
                if (visual != null)
                    visual.SetCullingVisible(true);
            }
        }

        _tileCullingAnyHidden = false;
    }

    private bool TryBuildTileCullingReferences(out int referenceCount)
    {
        referenceCount = 0;
        bool hasLocalPlayer = TryGetLocalPlayerGridPosition(out Vector2 localPlayerTile);

        if (tileCullingPreferLocalPlayer && hasLocalPlayer)
        {
            AddTileCullingReference(
                new Vector3(localPlayerTile.x * cellSize, 0f, localPlayerTile.y * cellSize),
                ref referenceCount);
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 cameraPosition = mainCamera.transform.position;
            cameraPosition.y = 0f;
            AddTileCullingReference(cameraPosition, ref referenceCount);

            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0f;
            if (tileCameraForwardLookahead > 0f && forward.sqrMagnitude > 0.0001f)
            {
                Vector3 lookaheadPosition = cameraPosition + forward.normalized * tileCameraForwardLookahead;
                AddTileCullingReference(lookaheadPosition, ref referenceCount);
            }
        }

        if (referenceCount == 0 && hasLocalPlayer)
        {
            AddTileCullingReference(
                new Vector3(localPlayerTile.x * cellSize, 0f, localPlayerTile.y * cellSize),
                ref referenceCount);
        }

        return referenceCount > 0;
    }

    private void AddTileCullingReference(Vector3 referencePosition, ref int referenceCount)
    {
        if (referenceCount >= _tileCullingReferenceBuffer.Length)
            return;

        for (int i = 0; i < referenceCount; i++)
        {
            Vector3 existing = _tileCullingReferenceBuffer[i];
            float dx = existing.x - referencePosition.x;
            float dz = existing.z - referencePosition.z;
            if (dx * dx + dz * dz < 0.25f)
                return;
        }

        _tileCullingReferenceBuffer[referenceCount] = referencePosition;
        referenceCount++;
    }

    private static bool IsWithinAnyTileCullingReference(
        Vector3 tilePosition,
        Vector3[] referencePositions,
        int referenceCount,
        float threshold)
    {
        float thresholdSqr = threshold * threshold;
        for (int i = 0; i < referenceCount; i++)
        {
            Vector3 referencePosition = referencePositions[i];
            float dx = tilePosition.x - referencePosition.x;
            float dz = tilePosition.z - referencePosition.z;
            if (dx * dx + dz * dz <= thresholdSqr)
                return true;
        }

        return false;
    }

    #endregion
}

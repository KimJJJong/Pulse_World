using UnityEngine;
using System.Collections.Generic;

public class BoardView : MonoBehaviour, IClientWorldView
{
    public static BoardView Instance { get; private set; }

    [Header("Prefabs (자동 생성/할당 가능)")]
    public GameObject playerPrefab;
    public GameObject monsterPrefab;

    public GameObject tilePrefab;
    
    // [System.Serializable]
    // public struct EntityPrefabMapping ... // Removed (Auto Load)
    // public List<EntityPrefabMapping> entityPrefabs ... // Removed

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
        // Load Entity Path Map
        LoadEntityMapping();
        ClientGameState.Instance.WorldView = this;
    }

    private void LoadEntityMapping()
    {
        var textAsset = Resources.Load<TextAsset>("Data/EntityData");
        if (textAsset != null)
        {
            try 
            {
                var root = JsonUtility.FromJson<EntityDataRoot>(textAsset.text);
                foreach(var e in root.Entities)
                {
                    if (!string.IsNullOrEmpty(e.ResourcePath))
                        _entityPathMap[e.EntityId] = e.ResourcePath;
                }
                Debug.Log($"[BoardView] Loaded {_entityPathMap.Count} entity paths from JSON.");
            }
            catch(System.Exception ex)
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
        // 1) 씬에 이미 베이크된 타일이 있는지 확인하고 바인딩 시도
        if (TryBindTilesFromScene(width, height))
        {
            Debug.Log($"[BoardView] OnCreateMap: Baked tiles bound. ({width}x{height})");
            return;
        }

        // 2) 베이크된 타일이 없거나 부족하면 기존 방식(런타임 생성)으로 폴백
        Debug.LogWarning($"[BoardView] OnCreateMap: No baked tiles found. Fallback to Instantiate. ({width}x{height})");
        
        ClearTiles(destroyGameObjects: true); // 기존 것 싹 날리고 새로 만듬

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
                
                // 더 이상 TileIndex 컴포넌트 붙이지 않음.
                // 이름(Tile_x_y)만 잘 지켜지면 문제 없음.

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

            // [추가] "정상 상태 기본색"을 저장해둔다 (텔레그래프 해제 시 원복용)
            if (_baseTileColors != null)
                _baseTileColors[x, y] = baseColor;

            rend.material.color = baseColor;
        }
    }

    /// <summary>
    /// 해당 좌표 타일을 "텔레그래프 빨강"으로 덮는다.
    /// - base color는 건드리지 않는다. (원복은 RestoreTileColor에서)
    /// </summary>
    public void SetTelegraphOverlay(int x, int y, bool on)
    {
        if (_tiles == null) 
        {
            Debug.LogWarning("[BoardView] SetTelegraphOverlay: _tiles is null");
            return;
        }
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1))
        {
            //Debug.LogWarning($"[BoardView] SetTelegraphOverlay: Out of bounds ({x},{y}) MapSize={_tiles.GetLength(0)}x{_tiles.GetLength(1)}");
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

         //Debug.Log($"[BoardView] SetTelegraphOverlay({x},{y}) - Rend={rend.name}, Mat={rend.material.name}, On={on}");

        if (on)
        {
            rend.material.color = TELEGRAPH_COLOR;
        }
        else
        {
            RestoreTileColor(x, y);
        }
    }

    /// <summary>
    /// tileKind 기반 기본색으로 원복한다.
    /// - SetTile에서 저장해둔 base color 사용
    /// </summary>
    public void RestoreTileColor(int x, int y)
    {
        if (_tiles == null) return;
        if (_baseTileColors == null) return;
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1)) return;

        var tile = _tiles[x, y];
        if (tile == null) return;

        var rend = GetTileRenderer(tile);
        if (rend != null)
        {
            rend.material.color = _baseTileColors[x, y];
        }
    }
    
    // ... [Inside TryBindTilesFromScene loop] ...
    // Note: I cannot replace inside a large method easily with replace_file_content if I don't select the whole method.
    // I will use a separate Replace call for TryBindTilesFromScene or just assume standard Replace works if I capture the block correctly.
    // Wait, TryBindTilesFromScene logic needs to be updated too.
    
    public bool TryBindTilesFromScene(int width, int height)
    {
        // 최적화: TileIndex 컴포넌트에 의존하지 않고, 자식 오브젝트의 이름(Tile_x_y)을 파싱하여 바인딩
        // O(ChildCount) 1회 순회로 끝냄.

        var map = new Dictionary<(int x, int y), GameObject>();
        
        foreach (Transform child in transform)
        {
            // 이름 파싱: "Tile_x_y"
            if (ParseTileName(child.name, out int x, out int y))
            {
                map[(x, y)] = child.gameObject;
            }
        }

        // 전체 범위가 다 있는지 확인
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!map.ContainsKey((x, y)))
                {
                    Debug.LogWarning($"[BoardView] Bind Fail: Missing tile at ({x},{y})");
                    // 실패 시 즉시 리턴하거나, 일부만 바인딩할지 결정. 안전을 위해 false 리턴.
                    return false;
                }
            }
        }

        // 성공 -> 실제 배열에 할당
        ClearTiles(destroyGameObjects: false); 
        _tiles = new GameObject[width, height];
        _baseTileColors = new Color[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _tiles[x, y] = map[(x, y)];
                
                // [Fix] 베이크된 타일의 실제 색상을 초기값으로 저장 (Telegraph 복구용)
                // Use GetTileRenderer here
                var rend = GetTileRenderer(_tiles[x, y]);
                if (rend != null)
                {
                    // material 접근 시 인스턴스 생성됨. (Telegraph에서도 어차피 생성하므로 무방)
                    // [Fix] Editor Warning 방지를 위해 읽을 때는 sharedMaterial 사용
                    _baseTileColors[x, y] = rend.sharedMaterial != null ? rend.sharedMaterial.color : Color.gray;
                }
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

            // EntityVisual 컴포넌트 확인/추가
            if (!go.TryGetComponent<EntityVisual>(out visual))
            {
                visual = go.AddComponent<EntityVisual>();
            }
            
            _entityViews[info.EntityId] = visual;

            // [New] Character Visual Controller Setup
            if (visual.TryGetComponent<RhythmRPG.Visual.CharacterVisualController>(out var visualCtrl))
            {
                // Game Scene Context
                visualCtrl.SetContext(RhythmRPG.Visual.CharacterContext.Game);

                // If it's my player, load equipment from InventoryManager
                if (info.EntityId == ClientGameState.Instance.MyActorId)
                {
                    // Convert Long InstanceID to TemplateID(int) list for visual loading
                    // Note: This needs to be robust. Ideally BoardView receives this info.
                    // For now, if it is MY player, I can read from local InventoryManager.
                    // For OTHER players, we need network data (EntityInfo should contain appearance data).
                    
                    var myEquips = InventoryManager.Instance.Equipments;
                    List<int> equippedTemplateIds = new List<int>();
                    foreach(var e in myEquips)
                    {
                        if (e.IsEquipped) equippedTemplateIds.Add(e.TemplateId);
                    }
                    visualCtrl.UpdateEquipments(equippedTemplateIds);
                }
                else
                {
                   // For other players/monsters, we need appearance info in ClientEntityInfo.
                   // Currently ClientEntityInfo only has 'AppearanceId' (ModelId).
                   // TODO: Extend protocol to sync equipment for other players.
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
        {
            return;
        }

        if (!action.Accepted)
        {
             return;
        }

        Vector3 fromW = GridToWorld(action.FromX, action.FromY);
        Vector3 toW = GridToWorld(action.ToX, action.ToY);

        // 현재 트랜스폼
        Vector3 curW = visual.transform.position;

        // "현재 트랜스폼이 원래 from 위치와 얼마나 다른가" (드리프트/누적오차 체크)
        float driftFrom = Vector3.Distance(curW, fromW);

        // 이동 거리 (월드 기준)
        float moveDist = Vector3.Distance(fromW, toW);

        // 튐(텔레포트) 감지
        bool teleportLike = moveDist > 2.0f;
        bool desynced = driftFrom > 0.5f;

        // [Sync] BPM 기반 이동 시간 계산
        double beatMs = RhythmClient.Instance.GetBeatDurationMs();
        
        // 1Beat 시간(초) * 비율 (예: 0.5초 * 0.5 = 0.25초 만에 행동 끝냄)
        float duration = (float)(beatMs / 1000.0) * actionDurationRatio;

        if (action.ActionKind == (int)ActionKind.Move)
        {
            // [Refactor] EntityVisual에게 위임
            visual.StartMove(fromW, toW, duration);
        }

        // ── Client-Side Prediction & Instant Broadcast 체크 ──
        // 내 Actor 뿐만 아니라, 서버의 SC_ActionInstantBroadcast로 먼저 재생된 애니메이션을 추적
        bool isAttackOrSkill = action.ActionKind == (int)ActionKind.Attack 
                            || action.ActionKind == (int)ActionKind.Skill;

        if (isAttackOrSkill)
        {
            if (_recentInstantActions.TryGetValue(action.ActorId, out float lastActionTime))
            {
                // 최근 1.5초(충분히 넉넉한 윈도우) 내에 트리거된 액션이면 이미 재생된 것으로 간주
                if (Time.time - lastActionTime < 1.5f)
                {
                    // 판정 완료, 리셋 (연속 판정을 위해)
                    _recentInstantActions.Remove(action.ActorId);
                    return; // 모션 재생 스킵 (HP 업데이트는 ClientHandlers에서 별도 처리)
                }
                else 
                {
                    _recentInstantActions.Remove(action.ActorId);
                }
            }
        }

        // [Sync] 공격 애니메이션 연결 (다른 플레이어/몬스터 or Prediction 안 된 경우)
        bool isMine = (ClientGameState.Instance != null && action.ActorId == ClientGameState.Instance.MyActorId);
        
        if (action.ActionKind == (int)ActionKind.Attack) 
        {
            visual.PlayAttack(duration, isMine);
        }
        else if (action.ActionKind == (int)ActionKind.Skill)
        {
            visual.PlaySkill(duration, isMine);
        }
    }


    /// <summary>
    /// SC_ActionInstantBroadcast 처리: 즉각적인 공격/스킬 브로드캐스트 재생 
    /// </summary>
    public void PlayInstantActionBroadcast(int actorId, ActionKind kind, float duration)
    {
        if (!_entityViews.TryGetValue(actorId, out var visual) || visual == null)
            return;

        bool isMine = (ClientGameState.Instance != null && actorId == ClientGameState.Instance.MyActorId);

        if (kind == ActionKind.Attack)
            visual.PlayAttack(duration, isMine);
        else if (kind == ActionKind.Skill)
            visual.PlaySkill(duration, isMine);

        // 시간 기록 (이중 재생 방지)
        _recentInstantActions[actorId] = Time.time;
    }

    public void OnInitGameCompleted()
    {
        Debug.Log("[BoardView] InitGameCompleted");
    }

    #endregion

    #region Map Binding & Baking
    
    // 에디터 툴(BoardViewMapBakerWindow)에서 호출하기 위해 public으로 변경
    public Vector3 GridToWorldPublic(int x, int y)
    {
        return GridToWorld(x, y);
    }
    
    //public bool TryBindTilesFromScene(int width, int height)
    //{
    //    // 최적화: TileIndex 컴포넌트에 의존하지 않고, 자식 오브젝트의 이름(Tile_x_y)을 파싱하여 바인딩
    //    // O(ChildCount) 1회 순회로 끝냄.

    //    var map = new Dictionary<(int x, int y), GameObject>();
        
    //    foreach (Transform child in transform)
    //    {
    //        // 이름 파싱: "Tile_x_y"
    //        if (ParseTileName(child.name, out int x, out int y))
    //        {
    //            map[(x, y)] = child.gameObject;
    //        }
    //    }

    //    // 전체 범위가 다 있는지 확인
    //    for (int y = 0; y < height; y++)
    //    {
    //        for (int x = 0; x < width; x++)
    //        {
    //            if (!map.ContainsKey((x, y)))
    //            {
    //                Debug.LogWarning($"[BoardView] Bind Fail: Missing tile at ({x},{y})");
    //                // 실패 시 즉시 리턴하거나, 일부만 바인딩할지 결정. 안전을 위해 false 리턴.
    //                return false;
    //            }
    //        }
    //    }

    //    // 성공 -> 실제 배열에 할당
    //    ClearTiles(destroyGameObjects: false); 
    //    _tiles = new GameObject[width, height];
    //    _baseTileColors = new Color[width, height];

    //    for (int y = 0; y < height; y++)
    //    {
    //        for (int x = 0; x < width; x++)
    //        {
    //            _tiles[x, y] = map[(x, y)];
                
    //            // [Fix] 베이크된 타일의 실제 색상을 초기값으로 저장 (Telegraph 복구용)
    //            // Use GetTileRenderer here
    //            var rend = GetTileRenderer(_tiles[x, y]);
    //            if (rend != null)
    //            {
    //                // material 접근 시 인스턴스 생성됨. (Telegraph에서도 어차피 생성하므로 무방)
    //                _baseTileColors[x, y] = rend.material.color;
    //            }
    //            else
    //            {
    //                Debug.LogWarning($"[BoardView] Bound tile at ({x},{y}) has no renderer!");
    //                _baseTileColors[x, y] = Color.gray;
    //            }
    //        }
    //    }

    //    Debug.Log($"[BoardView] Bind Success (NameBased): {width}x{height}");
    //    return true;
    //}

    private bool ParseTileName(string name, out int x, out int y)
    {
        x = -1; 
        y = -1;
        if (!name.StartsWith("Tile_")) return false;

        var parts = name.Split('_'); // Tile, x, y
        if (parts.Length < 3) return false;

        if (int.TryParse(parts[1], out x) && int.TryParse(parts[2], out y))
            return true;

        return false;
    }

    #endregion

    #region Helper

    private Dictionary<int, string> _entityPathMap = new Dictionary<int, string>(); // [ID -> ResourcePath]

    private GameObject ChoosePrefab(int entityType, int modelId)
    {
        // 1. Check Runtime Cache
        if (_entityPrefabCache.TryGetValue(modelId, out var prefab))
            return prefab;

        // 2. Load via Path Map
        string loadPath = "";
        if (_entityPathMap.TryGetValue(modelId, out var mappedPath))
        {
            loadPath = mappedPath;
        }
        else
        {
            // Fallback: Legacy Naming
            loadPath = $"Entities/Entity_{modelId}";
        }

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

        // 3. Fallback based on type (Legacy)
        switch (entityType)
        {
            case (int)EntityType.Player: // 1
                return playerPrefab;
            case (int)EntityType.Monster: // 2
                return monsterPrefab;
            case (int)EntityType.Object: // 3
                return monsterPrefab; // 임시: 오브젝트도 없을 땐 몬스터? 아니면 null
            default:
                return monsterPrefab;
        }
    }

    private Vector3 GridToWorld(int x, int y)
    {
        float targetX = x * cellSize;
        float targetZ = y * cellSize;
        float height = GetGroundHeight(targetX, targetZ);
        
        return new Vector3(targetX, height, targetZ);
    }

    private float GetGroundHeight(float x, float z)
    {
        // 높이 10에서 아래로 레이캐스트
        Ray ray = new Ray(new Vector3(x, 20f, z), Vector3.down);
        // Debug.DrawRay(ray.origin, ray.direction * 50f, Color.red, 2.0f); // Editor Debug

        if (Physics.Raycast(ray, out RaycastHit hit, 50f))
        {
            //Debug.Log($"[GetGroundHeight] Hit! ({x}, {z}) -> Y={hit.point.y} Collider={hit.collider.name}");
            return hit.point.y;
        }
        
        Debug.LogWarning($"[GetGroundHeight] Miss! ({x}, {z}) -> Defaulting to 0");
        return 0f; // 바닥 없으면 0
    }


    private void ClearTiles(bool destroyGameObjects = true)
    {
        if (_tiles == null)
            return;

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

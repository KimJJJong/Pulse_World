using UnityEngine;
using System.Collections.Generic;

public class BoardView : MonoBehaviour, IClientWorldView
{
    public static BoardView Instance { get; private set; }

    [Header("Prefabs (자동 생성/할당 가능)")]
    public GameObject playerPrefab;
    public GameObject monsterPrefab;
    public GameObject tilePrefab;

    [Header("Rendering")]
    public float cellSize = 1.0f;
    public float moveLerpTime = 0.1f;

    // entityId -> GameObject
    private readonly Dictionary<int, GameObject> _entityViews = new();

    // (x,y) -> Tile GameObject
    private GameObject[,] _tiles;

    private Color[,] _baseTileColors;

    private static readonly Color TELEGRAPH_COLOR = Color.red;


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
        ClientGameState.Instance.WorldView = this;

    }
    #region IClientWorldView

    public void OnCreateMap(int width, int height)
    {
        Debug.Log($"[BoardView] CreateMap {width}x{height}");
        ClearTiles();

        if (tilePrefab == null)
        {
            Debug.LogWarning("[BoardView] tilePrefab이 설정되지 않음. 타일을 생성하지 않습니다.");
            return;
        }

        _tiles = new GameObject[width, height];

        // [추가] 기본 색 캐시도 맵 크기에 맞춰 생성
        _baseTileColors = new Color[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = Instantiate(tilePrefab, transform);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.position = GridToWorld(x, y) + new Vector3(0, -2, 0);
                _tiles[x, y] = tile;

                // [추가] 초기 기본색은 일단 gray로 둔다 (SetTile이 들어오면 덮어씀)
                _baseTileColors[x, y] = Color.gray;
            }
        }
    }

    public void OnSetTile(int x, int y, int tileKind)
    {
        if (_tiles == null)
            return;
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1))
            return;

        var tile = _tiles[x, y];
        if (tile == null) return;

        if (tile.TryGetComponent<Renderer>(out var rend))
        {
            // ===== 기존 로직 유지 =====
            Color baseColor;
            switch (tileKind)
            {
                case 0: baseColor = Color.gray; break;
                case 1: baseColor = Color.white; break;
                case 2: baseColor = Color.cyan; break;
                default: baseColor = Color.white; break;
            }

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
        if (_tiles == null) return;
        if (x < 0 || y < 0 || x >= _tiles.GetLength(0) || y >= _tiles.GetLength(1)) return;

        var tile = _tiles[x, y];
        if (tile == null) return;

        if (!tile.TryGetComponent<Renderer>(out var rend))
            return;

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

        if (!tile.TryGetComponent<Renderer>(out var rend))
            return;

        rend.material.color = _baseTileColors[x, y];
    }

    public void OnClearEntities()
    {
        foreach (var kv in _entityViews)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }
        _entityViews.Clear();
    }

    public void OnSpawnOrUpdateEntity(ClientEntityInfo info)
    {
        if (!_entityViews.TryGetValue(info.EntityId, out var go) || go == null)
        {
            GameObject prefab = ChoosePrefab(info.EntityType);
            if (prefab == null)
            {
                Debug.LogWarning($"[BoardView] EntityType {info.EntityType}용 Prefab이 없음");
                return;
            }

            go = Instantiate(prefab, transform);
            go.name = $"Entity_{info.EntityId}";
            _entityViews[info.EntityId] = go;
        }

        if (info.EntityId == ClientGameState.Instance.MyActorId)
        {
            CameraBinder.Instance?.Bind(go.transform);
            RhythmInputControllerBinder.Instance?.Bind(go);
        }


        go.transform.position = GridToWorld(info.X, info.Y);
    }
    public void OnDespawnEntity(int entityId)
    {
        if (_entityViews.TryGetValue(entityId, out var go) && go != null)
            Destroy(go);

        _entityViews.Remove(entityId);

    }


    public void OnBeatAction(ClientBeatAction action, ClientEntityInfo entity)
    {
        if (!_entityViews.TryGetValue(action.ActorId, out var go) || go == null)
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
        Vector3 curW = go.transform.position;

        // “현재 트랜스폼이 원래 from 위치와 얼마나 다른가” (드리프트/누적오차 체크)
        float driftFrom = Vector3.Distance(curW, fromW);

        // 이동 거리 (월드 기준)
        float moveDist = Vector3.Distance(fromW, toW);

        // 튐(텔레포트) 감지: 상황에 맞게 임계값 조정
        bool teleportLike = moveDist > 2.0f;       // 한 비트에 2m 이상 이동이면 이상
        bool desynced = driftFrom > 0.5f;      // 현재 위치가 from과 0.5m 이상 차이나면 이상


        StartCoroutine(LerpMove(go.transform, fromW, toW, moveLerpTime));


        // 여기서 실제 이동 처리(lerp/warp 등) 하기 전/후로 한 번 더 찍고 싶으면:
        // go.transform.position = toW;
        // PosLog($"[POS] APPLY actor={action.ActorId} newCur={go.transform.position:F3}");
    }

    public void OnInitGameCompleted()
    {
        Debug.Log("[BoardView] InitGameCompleted");
    }

    #endregion

    #region Helper

    private GameObject ChoosePrefab(int entityType)
    {
        // 서버 EntityType enum과 맞춰야 함
        // 예: 0 = Player, 1 = Monster
        switch (entityType)
        {
            case 0:
                return playerPrefab;
            case 1:
                return playerPrefab;
            default:
                return monsterPrefab;//!= null ? playerPrefab : monsterPrefab;
        }
    }

    private Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(x * cellSize, 0, y * cellSize);
    }

    private System.Collections.IEnumerator LerpMove(Transform target, Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        target.position = from;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duration);
            target.position = Vector3.Lerp(from, to, alpha);
            yield return null;
        }

        target.position = to;
    }

    private void ClearTiles()
    {
        if (_tiles == null)
            return;

        int w = _tiles.GetLength(0);
        int h = _tiles.GetLength(1);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (_tiles[x, y] != null)
                    Destroy(_tiles[x, y]);

        _tiles = null;

        // [추가] 캐시도 같이 정리
        _baseTileColors = null;
    }

    #endregion
}

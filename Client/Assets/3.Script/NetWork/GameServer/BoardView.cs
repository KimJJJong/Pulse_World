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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = Instantiate(tilePrefab, transform);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.position = GridToWorld(x, y);
                _tiles[x, y] = tile;
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

        // 간단 예시: tileKind에 따라 색상 변경
        if (tile.TryGetComponent<Renderer>(out var rend))
        {
            switch (tileKind)
            {
                case 0: // 예: 빈 공간
                    rend.material.color = Color.gray;
                    break;
                case 1: // 예: 벽
                    rend.material.color = Color.black;
                    break;
                case 2: // 예: 스폰 지역
                    rend.material.color = Color.cyan;
                    break;
                default:
                    rend.material.color = Color.white;
                    break;
            }
        }
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

        go.transform.position = GridToWorld(info.X, info.Y);
    }

    public void OnBeatAction(ClientBeatAction action, ClientEntityInfo entity)
    {
        if (!_entityViews.TryGetValue(action.ActorId, out var go) || go == null)
            return;

        if (!action.Accepted)
        {
            // TODO: 실패 이펙트 넣고 싶으면 여기
            return;
        }

        Vector3 from = GridToWorld(action.FromX, action.FromY);
        Vector3 to = GridToWorld(action.ToX, action.ToY);

        StartCoroutine(LerpMove(go.transform, from, to, moveLerpTime));
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
                return monsterPrefab;
            default:
                return playerPrefab != null ? playerPrefab : monsterPrefab;
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
    }

    #endregion
}

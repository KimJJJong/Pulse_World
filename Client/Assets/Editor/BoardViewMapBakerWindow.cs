#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class BoardViewMapBakerWindow : EditorWindow
{
    private BoardView _boardView;
    private MapAsset _mapAsset;
    private GameObject _tilePrefab;
    private AppearanceAutoTilePalette _appearancePalette;
    private bool _repositionEvenIfExists = true;
    private Material _tilePrefabBaseMaterial;

    [MenuItem("RhythmRPG/Editors/World/BoardView Map Baker")]
    public static void Open()
    {
        GetWindow<BoardViewMapBakerWindow>("BoardView Map Baker");
    }

    [MenuItem("RhythmRPG/Editors/World/Fix Baked Tile Top Surfaces")]
    private static void FixBakedTileTopSurfaces()
    {
        int count = 0;
        var visuals = Resources.FindObjectsOfTypeAll<BoardTileVisual>();
        for (int i = 0; i < visuals.Length; i++)
        {
            var visual = visuals[i];
            if (visual == null || !visual.gameObject.scene.IsValid())
                continue;

            UnityEditor.Undo.RegisterFullObjectHierarchyUndo(visual.gameObject, "Fix Tile Top Surface");
            visual.RefreshTopSurfaceLayout();
            EditorUtility.SetDirty(visual.gameObject);
            count++;
        }

        Debug.Log($"[MapBaker] Fixed baked tile top surfaces: {count}");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bake tiles into Scene from MapAsset", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        _boardView = (BoardView)EditorGUILayout.ObjectField("BoardView (in Scene)", _boardView, typeof(BoardView), true);
        _mapAsset = (MapAsset)EditorGUILayout.ObjectField("Map Asset", _mapAsset, typeof(MapAsset), false);
        _tilePrefab = (GameObject)EditorGUILayout.ObjectField("Tile Prefab", _tilePrefab, typeof(GameObject), false);
        _appearancePalette = (AppearanceAutoTilePalette)EditorGUILayout.ObjectField(
            "Appearance Palette",
            ResolveAppearancePalette(),
            typeof(AppearanceAutoTilePalette),
            false);

        EditorGUILayout.Space(8);
        _repositionEvenIfExists = EditorGUILayout.ToggleLeft("Reposition tiles even if they exist", _repositionEvenIfExists);

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(_boardView == null || _tilePrefab == null || _mapAsset == null))
        {
            if (GUILayout.Button($"Bake / Update Tiles ({(_mapAsset != null ? $"{_mapAsset.Width}x{_mapAsset.Height}" : "?")})"))
            {
                BakeOrUpdate();
            }

            if (GUILayout.Button("Delete All Baked Tiles"))
            {
                if (EditorUtility.DisplayDialog("Delete Tiles", "BoardView 자식 타일들을 모두 삭제할까요?", "Delete", "Cancel"))
                    DeleteAllTiles();
            }
            
             if (GUILayout.Button("Bind Test (simulate runtime bind)"))
            {
                if (_mapAsset != null)
                {
                    var ok = _boardView.TryBindTilesFromScene(_mapAsset.Width, _mapAsset.Height);
                    Debug.Log(ok
                        ? $"[MapBaker] Bind OK ({_mapAsset.Width}x{_mapAsset.Height})"
                        : $"[MapBaker] Bind FAIL. Missing tiles for ({_mapAsset.Width}x{_mapAsset.Height})");
                }
            }
        }
    }

    private void BakeOrUpdate()
    {
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(_boardView.gameObject, "Bake Tiles");

        _mapAsset.EnsureSize();
        _mapAsset.RebuildAppearanceAutoTiles();
        _boardView.tilePrefab = _tilePrefab;
        _boardView.appearancePalette = ResolveAppearancePalette();
        _tilePrefabBaseMaterial = null;

        int width = _mapAsset.Width;
        int height = _mapAsset.Height;

        // 기존 타일 검색: 이름 파싱으로 식별
        // 반복문에서 transform.Find("Tile_x_y")를 써도 되지만, 
        // 최적화를 위해 미리 딕셔너리에 넣어둠.
        var map = new System.Collections.Generic.Dictionary<(int x, int y), GameObject>();
        foreach (Transform child in _boardView.transform)
        {
            if (ParseTileName(child.name, out int tx, out int ty))
            {
                map[(tx, ty)] = child.gameObject;
            }
        }

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!map.TryGetValue((x, y), out var go) || go == null)
            {
                // 프리팹 인스턴스 생성
                // (TileIndex 컴포넌트 추가 X)
                go = (GameObject)PrefabUtility.InstantiatePrefab(ResolveTilePrefab(x, y), _boardView.transform);
                go.name = $"Tile_{x}_{y}";
                go.transform.position = _boardView.GridToWorldPublic(x, y) + new Vector3(0, -2, 0);

                ApplyColor(go, x, y);
            }
            else
            {
                // 있으면 재사용
                if (_repositionEvenIfExists)
                {
                    go.transform.position = _boardView.GridToWorldPublic(x, y) + new Vector3(0, -2, 0);
                    ApplyColor(go, x, y);
                }
            }
        }

        EditorUtility.SetDirty(_boardView.gameObject);
        EditorUtility.SetDirty(_boardView);
        EditorUtility.SetDirty(_mapAsset);
        Debug.Log($"[MapBaker] Baked/Updated tiles: {width}x{height} from {_mapAsset.name} (Scriptless)");
    }

    private GameObject ResolveTilePrefab(int x, int y)
    {
        var appearance = _mapAsset.GetAppearance(x, y);
        var palette = ResolveAppearancePalette();
        if (appearance.Kind != AppearanceTileKind.None
            && palette != null
            && palette.TryGetPrefab(appearance.Kind, appearance.Variant, out var prefab))
        {
            return prefab;
        }

        return _tilePrefab;
    }

    private void ApplyColor(GameObject go, int x, int y)
    {
        var visual = GetOrAddTileVisual(go);
        var rend = visual != null ? visual.BaseRenderer : null;

        if (rend != null)
        {
            visual.SetBaseMaterial(GetTilePrefabBaseMaterial());

            var cell = _mapAsset.Get(x, y);
            Color color = _boardView.GetTileColor((int)cell.Kind);
            var appearance = _mapAsset.GetAppearance(x, y);
            bool materialApplied = false;
            if (appearance.Kind != AppearanceTileKind.None)
            {
                var palette = ResolveAppearancePalette();
                if (palette != null && palette.TryGetMaterial(appearance.Kind, appearance.Variant, out var material))
                {
                    visual.SetBaseColor(color);
                    visual.SetTopMaterial(material);
                    materialApplied = true;
                }

                Color appearanceColor = GetAppearancePreviewColor(palette, appearance);
                color = Color.Lerp(color, appearanceColor, 0.75f);
            }

            if (materialApplied)
                return;

            visual.HideTopSurface();
            visual.SetBaseColor(color);
        }
    }

    private BoardTileVisual GetOrAddTileVisual(GameObject go)
    {
        if (go == null)
            return null;

        if (!go.TryGetComponent<BoardTileVisual>(out var visual))
            visual = go.AddComponent<BoardTileVisual>();

        return visual;
    }

    private Material GetTilePrefabBaseMaterial()
    {
        if (_tilePrefabBaseMaterial != null)
            return _tilePrefabBaseMaterial;

        var renderer = BoardTileVisual.FindBaseRenderer(_tilePrefab);
        _tilePrefabBaseMaterial = renderer != null ? renderer.sharedMaterial : null;
        return _tilePrefabBaseMaterial;
    }

    private AppearanceAutoTilePalette ResolveAppearancePalette()
    {
        if (_appearancePalette != null)
            return _appearancePalette;

        return _mapAsset != null ? _mapAsset.AppearancePalette : null;
    }

    private Color GetAppearancePreviewColor(AppearanceAutoTilePalette palette, AppearanceTileCell appearance)
    {
        Color baseColor = palette != null
            ? palette.GetPreviewColor(appearance.Kind)
            : AppearanceAutoTilePalette.GetBuiltInPreviewColor(appearance.Kind);

        int connectionCount = CountBits(appearance.Variant);
        return Color.Lerp(baseColor, Color.white, connectionCount / 8f * 0.12f);
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

    private void DeleteAllTiles()
    {
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(_boardView.gameObject, "Delete Tiles");

        // "Tile_" 로 시작하는 자식만 삭제
        for (int i = _boardView.transform.childCount - 1; i >= 0; i--)
        {
            var child = _boardView.transform.GetChild(i);
            if (child.name.StartsWith("Tile_"))
            {
                UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        EditorUtility.SetDirty(_boardView.gameObject);
        Debug.Log("[MapBaker] Deleted all baked tiles under BoardView");
    }

    private bool ParseTileName(string name, out int x, out int y)
    {
        x = -1; 
        y = -1;
        if (string.IsNullOrEmpty(name) || !name.StartsWith("Tile_")) return false;

        var parts = name.Split('_'); // Tile, x, y
        if (parts.Length < 3) return false;

        if (int.TryParse(parts[1], out x) && int.TryParse(parts[2], out y))
            return true;

        return false;
    }
}
#endif

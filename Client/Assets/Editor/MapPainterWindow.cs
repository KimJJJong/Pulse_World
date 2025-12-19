#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public sealed class MapPainterWindow : EditorWindow
{
    private MapAsset _map;
    private TileKind _paintKind = TileKind.Floor;
    private int _paintVariant = 0;

    private Vector2 _scroll;
    private const float CellSize = 28f;

    [MenuItem("RhythmRPG/Game/Map Painter")]
    public static void Open() => GetWindow<MapPainterWindow>("Map Painter");

    private void OnGUI()
    {
        EditorGUILayout.Space(4);

        _map = (MapAsset)EditorGUILayout.ObjectField("Map Asset", _map, typeof(MapAsset), false);
        if (_map == null)
        {
            EditorGUILayout.HelpBox("MapAsset을 만들고(우클릭 Create/Game/Map/MapAsset) 여기 넣어줘.", MessageType.Info);
            return;
        }
        _map.EnsureSize();


        DrawSizeControls();
        EditorGUILayout.Space(8);
        DrawPalette();
        EditorGUILayout.Space(8);
        try
        {
            DrawGrid();
        }
        catch (Exception ex)
        {
            EditorGUILayout.HelpBox(ex.ToString(), MessageType.Error);
        }
        EditorGUILayout.Space(8);
        DrawExportHint();
    }

    private void DrawSizeControls()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Size", EditorStyles.boldLabel);

            int w = EditorGUILayout.IntField("Width", _map.Width);
            int h = EditorGUILayout.IntField("Height", _map.Height);

            w = Mathf.Clamp(w, 1, 256);
            h = Mathf.Clamp(h, 1, 256);

            if (w != _map.Width || h != _map.Height)
            {
                if (GUILayout.Button("Apply Resize (data reset)"))
                {
                    UnityEditor.Undo.RecordObject(_map, "Resize Map");
                    _map.Width = w;
                    _map.Height = h;
                    _map.Cells = new TileCell[w * h]; // 기본 None
                    EditorUtility.SetDirty(_map);
                }
            }
        }
    }

    private void DrawPalette()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

            _paintKind = (TileKind)EditorGUILayout.EnumPopup("TileKind", _paintKind);
            _paintVariant = EditorGUILayout.IntSlider("Variant", _paintVariant, 0, 15);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill All"))
            {
                UnityEditor.Undo.RecordObject(_map, "Fill All");
                var v = new TileCell { Kind = _paintKind, Variant = (byte)_paintVariant };
                for (int i = 0; i < _map.Cells.Length; i++) _map.Cells[i] = v;
                EditorUtility.SetDirty(_map);
            }
            if (GUILayout.Button("Clear All"))
            {
                UnityEditor.Undo.RecordObject(_map, "Clear All");
                for (int i = 0; i < _map.Cells.Length; i++) _map.Cells[i] = default;
                EditorUtility.SetDirty(_map);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "같은 TileKind라도 Variant가 다르면 칸 색/표시가 달라져서 구분됨.\n" +
                "클릭/드래그로 페인트 가능.",
                MessageType.None
            );
        }
    }

    private void DrawGrid()
    {
        // 마우스 이벤트 받기 위해 Rect 확보
        int gridW = Mathf.RoundToInt(_map.Width * CellSize);
        int gridH = Mathf.RoundToInt(_map.Height * CellSize);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(600));


        Rect gridRect = GUILayoutUtility.GetRect(gridW, gridH);
        GUI.Box(gridRect, GUIContent.none);

        HandlePaint(gridRect);

        // 그리기
        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                Rect cellRect = new Rect(
                    gridRect.x + x * CellSize,
                    gridRect.y + y * CellSize,
                    CellSize,
                    CellSize
                );

                var cell = _map.Get(x, y);
                DrawCell(cellRect, cell);

                // grid line
                Handles.color = new Color(0, 0, 0, 0.15f);
                Handles.DrawLine(new Vector3(cellRect.x, cellRect.y), new Vector3(cellRect.xMax, cellRect.y));
                Handles.DrawLine(new Vector3(cellRect.x, cellRect.yMax), new Vector3(cellRect.xMax, cellRect.yMax));
                Handles.DrawLine(new Vector3(cellRect.x, cellRect.y), new Vector3(cellRect.x, cellRect.yMax));
                Handles.DrawLine(new Vector3(cellRect.xMax, cellRect.y), new Vector3(cellRect.xMax, cellRect.yMax));
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void HandlePaint(Rect gridRect)
    {
        Event e = Event.current;
        if (e == null) return;

        bool isPaintEvent = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
        if (!isPaintEvent) return;

        if (!gridRect.Contains(e.mousePosition)) return;

        int x = Mathf.FloorToInt((e.mousePosition.x - gridRect.x) / CellSize);
        int y = Mathf.FloorToInt((e.mousePosition.y - gridRect.y) / CellSize);

        if (!_map.InBounds(x, y)) return;

        // 좌클릭: 칠하기 / 우클릭: 지우기
        UnityEditor.Undo.RecordObject(_map, "Paint Tile");

        if (e.button == 0)
        {
            _map.Set(x, y, new TileCell { Kind = _paintKind, Variant = (byte)_paintVariant });
        }
        else if (e.button == 1)
        {
            _map.Set(x, y, default);
        }

        EditorUtility.SetDirty(_map);
        e.Use();
    }

    private void DrawCell(Rect r, TileCell cell)
    {
        // Kind에 따른 기본 톤 + Variant에 따른 밝기/패턴 차이
        Color baseColor = cell.Kind switch
        {
            TileKind.None => new Color(0.2f, 0.2f, 0.2f, 0.35f),
            TileKind.Floor => new Color(0.3f, 0.75f, 0.35f, 0.85f),
            TileKind.Wall => new Color(0.55f, 0.55f, 0.55f, 0.9f),
            TileKind.Spawn => new Color(0.35f, 0.55f, 0.85f, 0.9f),
            _ => new Color(1, 0, 1, 0.9f),
        };

        // Variant가 다르면 색이 살짝 달라지게(눈으로 구분)
        float t = (cell.Variant % 16) / 15f; // 0~1
        Color vColor = Color.Lerp(baseColor, Color.white, 0.15f * t);

        EditorGUI.DrawRect(r, vColor);

        // 텍스트로도 Kind/Variant 보이게
        var label = cell.Kind == TileKind.None ? "" : $"{(byte)cell.Kind}:{cell.Variant}";
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.black }
        };
        GUI.Label(r, label, style);
    }

    private void DrawExportHint()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Next Step", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("이 MapAsset을 JSON으로 Export 해서 서버(Map2D)에서 로드하면 끝.");
        }
    }
}
#endif

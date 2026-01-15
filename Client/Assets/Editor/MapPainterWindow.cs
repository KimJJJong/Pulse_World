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
    private const float ViewHeight = 600f;

    // 캐시(매번 new 금지)
    private static readonly Color GridLineColor = new Color(0, 0, 0, 0.15f);
    private static readonly GUIStyle CellLabelStyle = new GUIStyle(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        normal = { textColor = Color.black }
    };

    // 드래그 중 Undo 중복 방지
    private bool _dragPainting;
    private int _lastPaintIndex = -1;

    [MenuItem("RhythmRPG/Game/Map Painter")]
    public static void Open() => GetWindow<MapPainterWindow>("Map Painter");

    private void OnGUI()
    {
        EditorGUILayout.Space(4);

        // Map 변경 감지 시에만 EnsureSize
        EditorGUI.BeginChangeCheck();
        _map = (MapAsset)EditorGUILayout.ObjectField("Map Asset", _map, typeof(MapAsset), false);
        if (EditorGUI.EndChangeCheck() && _map != null)
            _map.EnsureSize();

        if (_map == null)
        {
            EditorGUILayout.HelpBox("MapAsset을 만들고(우클릭 Create/Game/Map/MapAsset) 여기 넣어줘.", MessageType.Info);
            return;
        }

        DrawSizeControls();
        EditorGUILayout.Space(8);
        DrawPalette();
        EditorGUILayout.Space(8);

        try { DrawGridOptimized(); }
        catch (Exception ex) { EditorGUILayout.HelpBox(ex.ToString(), MessageType.Error); }

        EditorGUILayout.Space(8);
        DrawExportHint();
    }

    private void DrawGridOptimized()
    {
        int gridW = Mathf.RoundToInt(_map.Width * CellSize);
        int gridH = Mathf.RoundToInt(_map.Height * CellSize);

        // ScrollView 안에서 실제로 그릴 영역 확보
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(ViewHeight));

        Rect gridRect = GUILayoutUtility.GetRect(gridW, gridH);
        GUI.Box(gridRect, GUIContent.none);

        // 입력 먼저 처리 (좌표 계산에 gridRect 필요)
        HandlePaintOptimized(gridRect);

        // === "보이는 영역만" 계산 ===
        // ScrollView의 뷰포트(대략): 현재 창 폭/고정 높이
        float viewW = position.width; // 대략치 (충분)
        Rect viewRect = new Rect(_scroll.x, _scroll.y, viewW, ViewHeight);

        int xMin = Mathf.Clamp(Mathf.FloorToInt(viewRect.xMin / CellSize), 0, _map.Width - 1);
        int xMax = Mathf.Clamp(Mathf.CeilToInt(viewRect.xMax / CellSize), 0, _map.Width);
        int yMin = Mathf.Clamp(Mathf.FloorToInt(viewRect.yMin / CellSize), 0, _map.Height - 1);
        int yMax = Mathf.Clamp(Mathf.CeilToInt(viewRect.yMax / CellSize), 0, _map.Height);

        // === 그리기: Repaint 이벤트에서만 ===
        if (Event.current.type == EventType.Repaint)
        {
            // (1) 셀 채우기: visible range만 순회
            for (int y = yMin; y < yMax; y++)
            {
                float cy = gridRect.y + y * CellSize;
                for (int x = xMin; x < xMax; x++)
                {
                    Rect cellRect = new Rect(
                        gridRect.x + x * CellSize,
                        cy,
                        CellSize,
                        CellSize
                    );

                    var cell = _map.Get(x, y);
                    DrawCellFast(cellRect, cell);
                }
            }

            // (2) Grid line: 타일마다 4줄 X -> 세로/가로줄만 그리기
            Handles.BeginGUI();
            Handles.color = GridLineColor;

            // 세로줄
            for (int x = xMin; x <= xMax; x++)
            {
                float gx = gridRect.x + x * CellSize;
                Handles.DrawLine(
                    new Vector3(gx, gridRect.y + yMin * CellSize),
                    new Vector3(gx, gridRect.y + yMax * CellSize)
                );
            }

            // 가로줄
            for (int y = yMin; y <= yMax; y++)
            {
                float gy = gridRect.y + y * CellSize;
                Handles.DrawLine(
                    new Vector3(gridRect.x + xMin * CellSize, gy),
                    new Vector3(gridRect.x + xMax * CellSize, gy)
                );
            }

            Handles.EndGUI();
        }

        EditorGUILayout.EndScrollView();
    }

    private void HandlePaintOptimized(Rect gridRect)
    {
        Event e = Event.current;
        if (e == null) return;

        // 마우스 업에서 드래그 상태 종료
        if (e.type == EventType.MouseUp)
        {
            _dragPainting = false;
            _lastPaintIndex = -1;
            return;
        }

        bool isPaintEvent = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
        if (!isPaintEvent) return;
        if (!gridRect.Contains(e.mousePosition)) return;

        int x = Mathf.FloorToInt((e.mousePosition.x - gridRect.x) / CellSize);
        int y = Mathf.FloorToInt((e.mousePosition.y - gridRect.y) / CellSize);
        if (!_map.InBounds(x, y)) return;

        int index = y * _map.Width + x;

        // 같은 칸에 드래그 중복 칠하기 방지 (불필요 SetDirty/리페인트/Undo 감소)
        if (index == _lastPaintIndex && e.type == EventType.MouseDrag)
            return;
        _lastPaintIndex = index;

        // Undo는 MouseDown에서 1번만
        if (e.type == EventType.MouseDown)
        {
            UnityEditor.Undo.RecordObject(_map, "Paint Tile");
            _dragPainting = true;
        }
        else if (e.type == EventType.MouseDrag)
        {
            // MouseDown 없이 Drag가 오는 경우 방어
            if (!_dragPainting)
            {
                UnityEditor.Undo.RecordObject(_map, "Paint Tile");
                _dragPainting = true;
            }
        }

        if (e.button == 0)
            _map.Set(x, y, new TileCell { Kind = _paintKind, Variant = (byte)_paintVariant });
        else if (e.button == 1)
            _map.Set(x, y, default);

        EditorUtility.SetDirty(_map);
        e.Use();
        Repaint(); // 입력 시 즉시 갱신
    }

    private void DrawCellFast(Rect r, TileCell cell)
    {
        Color baseColor = cell.Kind switch
        {
            TileKind.None => new Color(0.2f, 0.2f, 0.2f, 0.35f),
            TileKind.Floor => new Color(0.3f, 0.75f, 0.35f, 0.85f),
            TileKind.Wall => new Color(0.55f, 0.55f, 0.55f, 0.9f),
            TileKind.Spawn => new Color(0.35f, 0.55f, 0.85f, 0.9f),
            _ => new Color(1, 0, 1, 0.9f),
        };

        float t = (cell.Variant & 0x0F) / 15f; // % 대신 비트 & (미세하지만 싸게)
        Color vColor = Color.Lerp(baseColor, Color.white, 0.15f * t);
        EditorGUI.DrawRect(r, vColor);

        // 라벨은 비용 큼: 필요하면 옵션으로 끄는 게 더 좋음
        if (cell.Kind != TileKind.None)
        {
            GUI.Label(r, $"{(byte)cell.Kind}:{cell.Variant}", CellLabelStyle);
        }
    }

    // ==== 기존 함수들(그대로 써도 됨) ====

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
                    _map.Cells = new TileCell[w * h];
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

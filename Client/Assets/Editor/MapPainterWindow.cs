#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class MapPainterWindow : EditorWindow
{
    private enum PaintLayer
    {
        Logic = 0,
        Appearance = 1,
    }

    private enum BrushTool
    {
        Paint = 0,
        Erase = 1,
        Pick = 2,
        Paste = 3,
    }

    private enum TextureSheetMaskOrder
    {
        Blob47Numeric = 0,
        Sequential = 1,
    }

    private struct QuickAppearanceAsset
    {
        public string Name;
        public UnityEngine.Object Source;
        public Material Material;
        public GameObject Prefab;
        public Texture2D Texture;
        public Sprite Sprite;
        public Rect TextureRect;
        public bool HasTextureRect;
        public int ExplicitMask;
        public bool HasExplicitMask;

        public bool HasRenderableAsset => Material != null || Prefab != null || Texture != null || Sprite != null;
    }

    private struct AppearancePaletteItem
    {
        public AppearanceTileKind Kind;
        public string Label;
        public Color Color;

        public AppearancePaletteItem(AppearanceTileKind kind, string label, Color color)
        {
            Kind = kind;
            Label = label;
            Color = color;
        }
    }

    private const string DefaultMapAssetFolder = "Assets/Resources/Data/Map";
    private const string DefaultAppearancePaletteFolder = "Assets/Resources/Data/Map/AppearancePalettes";
    private const float DefaultCellSize = 24f;
    private const float MinCellSize = 14f;
    private const float MaxCellSize = 34f;
    private const float MinViewHeight = 160f;
    private const float ControlPanelMinWidth = 420f;
    private const float ControlPanelMaxWidth = 480f;
    private const float DefaultScrollbarSize = 16f;
    private const float MapScrollWheelStep = 24f;
    private const float MapScrollbarButtonSize = 18f;
    private const float MapViewportRightPadding = 8f;
    private const int MaxBrushSize = 8;

    private static readonly string[] LayerTabLabels = { "Logic", "Appearance Auto Tile" };
    private static readonly string[] ToolLabels = { "Paint", "Erase", "Pick", "Paste" };
    private static readonly int[] Blob47Masks = CreateBlob47Masks();

    private static readonly Color GridLineColor = new Color(0, 0, 0, 0.15f);
    private static readonly Color BrushLogicColor = new Color(1f, 0.82f, 0.18f, 0.9f);
    private static readonly Color BrushAppearanceColor = new Color(0.2f, 0.85f, 1f, 0.95f);
    private static readonly Color PaletteSelectedColor = new Color(0.18f, 0.34f, 0.42f, 1f);
    private static readonly Color PaletteNormalColor = new Color(0.17f, 0.17f, 0.17f, 1f);

    private static readonly AppearancePaletteItem[] AppearancePalette =
    {
        new AppearancePaletteItem(AppearanceTileKind.GrassBorder, "Grass Border", new Color(0.25f, 0.68f, 0.30f, 1f)),
        new AppearancePaletteItem(AppearanceTileKind.StonePath, "Stone Path", new Color(0.58f, 0.58f, 0.55f, 1f)),
        new AppearancePaletteItem(AppearanceTileKind.BrickBorder, "Brick Border", new Color(0.64f, 0.28f, 0.20f, 1f)),
        new AppearancePaletteItem(AppearanceTileKind.WaterEdge, "Water Edge", new Color(0.18f, 0.55f, 0.86f, 1f)),
        new AppearancePaletteItem(AppearanceTileKind.WoodDeck, "Wood Deck", new Color(0.58f, 0.38f, 0.18f, 1f)),
        new AppearancePaletteItem(AppearanceTileKind.Carpet, "Carpet", new Color(0.68f, 0.18f, 0.36f, 1f)),
        new AppearancePaletteItem(AppearanceTileKind.FlowerBed, "Flower Bed", new Color(0.86f, 0.52f, 0.18f, 1f)),
        new AppearancePaletteItem(AppearanceTileKind.CustomA, "Custom A", new Color(0.46f, 0.33f, 0.78f, 1f)),
        new AppearancePaletteItem(AppearanceTileKind.CustomB, "Custom B", new Color(0.15f, 0.70f, 0.62f, 1f)),
    };

    private static GUIStyle _cellLabelStyle;
    private static GUIStyle CellLabelStyle
    {
        get
        {
            if (_cellLabelStyle == null)
            {
                _cellLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                _cellLabelStyle.normal.textColor = Color.black;
            }

            return _cellLabelStyle;
        }
    }

    private MapAsset _map;
    private PaintLayer _paintLayer = PaintLayer.Logic;
    private BrushTool _brushTool = BrushTool.Paint;

    private TileKind _paintKind = TileKind.Floor;
    private int _paintVariant = 0;

    private AppearanceTileKind _appearanceKind = AppearanceTileKind.StonePath;
    private bool _showAppearanceQuickSetup;
    private AppearanceTileKind _quickAppearanceKind = AppearanceTileKind.StonePath;
    private string _quickDisplayName = "";
    private Color _quickPreviewColor = Color.white;
    private Material _quickDefaultMaterial;
    private GameObject _quickDefaultPrefab;
    private bool _quickSingleSelectionAsDefault = true;
    private bool _quickReplaceVariants = true;
    private int _quickSheetColumns = 8;
    private int _quickSheetRows = 8;
    private int _quickSheetStartMask = 0;
    private int _quickSheetDefaultCell = 0;
    private bool _quickSheetAssignDefault = true;
    private TextureSheetMaskOrder _quickSheetMaskOrder = TextureSheetMaskOrder.Blob47Numeric;
    private int _brushSize = 1;
    private bool _showLogicLabels = true;
    private bool _showAppearanceLayer = true;
    private bool _showAppearanceMaskLabels = false;
    private float _cellSize = DefaultCellSize;

    private Vector2 _scroll;
    private Vector2 _controlScroll;
    private float _controlPanelWidth = ControlPanelMinWidth;
    private float _lastMapViewportWidth = 1f;
    private float _lastMapContentWidth = 1f;
    private bool _dragPainting;
    private int _lastPaintIndex = -1;
    private bool _hasHoveredCell;
    private int _hoveredX;
    private int _hoveredY;
    private bool _dragPasteActive;
    private bool _dragPasteHasPayload;
    private PaintLayer _dragPasteLayer;
    private TileCell[] _dragPasteLogicCells;
    private AppearanceTileCell[] _dragPasteAppearanceCells;
    private int _dragPasteWidth;
    private int _dragPasteHeight;
    private int _dragPasteSourceX;
    private int _dragPasteSourceY;
    private int _dragPasteSelectionStartX;
    private int _dragPasteSelectionStartY;
    private int _dragPasteSelectionEndX;
    private int _dragPasteSelectionEndY;
    private int _dragPasteTargetX;
    private int _dragPasteTargetY;

    [MenuItem("RhythmRPG/Editors/World/Map Painter")]
    public static void Open() => GetWindow<MapPainterWindow>("Map Painter");

    private void OnEnable()
    {
        wantsMouseMove = true;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);

        DrawAssetControls();

        if (_map == null)
        {
            EditorGUILayout.HelpBox("MapAsset을 만들고 여기 넣어줘. 새로 만들면 Resources/Data/Map 경로로 바로 저장됩니다.", MessageType.Info);
            return;
        }

        _map.EnsureSize();

        DrawEditorWorkspace();
    }

    private void DrawEditorWorkspace()
    {
        float workspaceTop = GUILayoutUtility.GetLastRect().yMax;
        if (workspaceTop <= 0f)
            workspaceTop = 92f;

        float workspaceHeight = Mathf.Max(MinViewHeight, position.height - workspaceTop - 8f);
        float panelWidth = GetControlPanelWidth();
        _controlPanelWidth = panelWidth - 18f;

        EditorGUILayout.BeginHorizontal(GUILayout.Height(workspaceHeight));
        GUILayout.Space(6f);

        _controlScroll = EditorGUILayout.BeginScrollView(
            _controlScroll,
            GUILayout.Width(panelWidth),
            GUILayout.Height(workspaceHeight));

        using (new EditorGUILayout.VerticalScope(GUILayout.Width(_controlPanelWidth)))
        {
            DrawModeToolbar();
            DrawSizeControls();
            EditorGUILayout.Space(6);
            DrawPalette();
            EditorGUILayout.Space(6);
            DrawExportHint();
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(6f);

        try { DrawGridOptimized(workspaceHeight); }
        catch (Exception ex) { EditorGUILayout.HelpBox(ex.ToString(), MessageType.Error); }

        EditorGUILayout.EndHorizontal();
    }

    private float GetControlPanelWidth()
    {
        if (position.width < 980f)
            return Mathf.Clamp(position.width * 0.46f, 320f, ControlPanelMinWidth);

        return Mathf.Clamp(position.width * 0.28f, ControlPanelMinWidth, ControlPanelMaxWidth);
    }

    private void DrawAssetControls()
    {
        EditorGUI.BeginChangeCheck();
        _map = (MapAsset)EditorGUILayout.ObjectField("Map Asset", _map, typeof(MapAsset), false);
        if (EditorGUI.EndChangeCheck() && _map != null)
            _map.EnsureSize();

        if (GUILayout.Button("New Map Asset"))
        {
            CreateNewMapAsset();
        }

        if (_map == null)
            return;

        EditorGUI.BeginChangeCheck();
        var nextPalette = (AppearanceAutoTilePalette)EditorGUILayout.ObjectField(
            "Appearance Palette",
            _map.AppearancePalette,
            typeof(AppearanceAutoTilePalette),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            UnityEditor.Undo.RecordObject(_map, "Set Appearance Palette");
            _map.AppearancePalette = nextPalette;
            EditorUtility.SetDirty(_map);
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(_map.AppearancePalette == null))
        {
            if (GUILayout.Button("Ping Palette"))
                EditorGUIUtility.PingObject(_map.AppearancePalette);
        }

        using (new EditorGUI.DisabledScope(_map.AppearancePalette == null))
        {
            if (GUILayout.Button("Edit Palette Setup"))
            {
                Selection.activeObject = _map.AppearancePalette;
                EditorGUIUtility.PingObject(_map.AppearancePalette);
            }
        }

        if (GUILayout.Button("Create Appearance Palette"))
        {
            CreateNewAppearancePalette();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawModeToolbar()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Editing Mode", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int nextLayer = GUILayout.Toolbar((int)_paintLayer, LayerTabLabels, GUILayout.Height(28));
            if (EditorGUI.EndChangeCheck())
            {
                _paintLayer = (PaintLayer)nextLayer;
                CancelDragPaste();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tool", GUILayout.Width(70));
            EditorGUI.BeginChangeCheck();
            int nextTool = GUILayout.Toolbar((int)_brushTool, ToolLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _brushTool = (BrushTool)nextTool;
                CancelDragPaste();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Brush", GUILayout.Width(70));
            _brushSize = EditorGUILayout.IntSlider(_brushSize, 1, MaxBrushSize);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Cell Size", GUILayout.Width(70));
            _cellSize = EditorGUILayout.Slider(_cellSize, MinCellSize, MaxCellSize);
            if (GUILayout.Button("Fit", GUILayout.Width(42)))
                FitCellSizeToVisibleMap();
            EditorGUILayout.EndHorizontal();

            DrawHorizontalMapScrollControls();

            if (_brushTool == BrushTool.Paste)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Move Area", GUILayout.Width(70));
                EditorGUILayout.LabelField(GetDragPasteStatusText(), EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            _showAppearanceLayer = EditorGUILayout.ToggleLeft("Show Appearance", _showAppearanceLayer);
            _showLogicLabels = EditorGUILayout.ToggleLeft("Logic Labels", _showLogicLabels);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _showAppearanceMaskLabels = EditorGUILayout.ToggleLeft("Auto Mask Labels", _showAppearanceMaskLabels);
            EditorGUILayout.EndHorizontal();
        }
    }

    private void FitCellSizeToVisibleMap()
    {
        float availableMapWidth = Mathf.Max(120f, position.width - GetControlPanelWidth() - 34f);
        float availableMapHeight = Mathf.Max(MinViewHeight, position.height - 96f);
        float fitX = availableMapWidth / Mathf.Max(1, _map.Width);
        float fitY = availableMapHeight / Mathf.Max(1, _map.Height);
        _cellSize = Mathf.Clamp(Mathf.Floor(Mathf.Min(fitX, fitY)), MinCellSize, MaxCellSize);
        Repaint();
    }

    private void DrawHorizontalMapScrollControls()
    {
        float maxScrollX = GetHorizontalMapScrollMax();
        _scroll.x = Mathf.Clamp(_scroll.x, 0f, maxScrollX);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Map X", GUILayout.Width(70));
        using (new EditorGUI.DisabledScope(maxScrollX <= 0f))
        {
            if (GUILayout.Button("<<", GUILayout.Width(32)))
                SetHorizontalMapScroll(0f);

            if (GUILayout.Button("<", GUILayout.Width(28)))
                SetHorizontalMapScroll(_scroll.x - GetHorizontalMapPageStep());

            EditorGUI.BeginChangeCheck();
            float nextScrollX = GUILayout.HorizontalSlider(
                _scroll.x,
                0f,
                Mathf.Max(1f, maxScrollX),
                GUILayout.MinWidth(70f));
            if (EditorGUI.EndChangeCheck())
                SetHorizontalMapScroll(nextScrollX);

            if (GUILayout.Button(">", GUILayout.Width(28)))
                SetHorizontalMapScroll(_scroll.x + GetHorizontalMapPageStep());

            if (GUILayout.Button(">>", GUILayout.Width(32)))
                SetHorizontalMapScroll(maxScrollX);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            maxScrollX > 0f
                ? $"X Scroll: {_scroll.x:0} / {maxScrollX:0}"
                : "X Scroll: map fits current view",
            EditorStyles.miniLabel);
    }

    private float GetHorizontalMapScrollMax()
    {
        if (_map == null)
            return 0f;

        float cellSize = Mathf.Clamp(_cellSize, MinCellSize, MaxCellSize);
        float contentWidth = _lastMapContentWidth > 1f
            ? _lastMapContentWidth
            : _map.Width * cellSize;
        float viewportWidth = _lastMapViewportWidth > 1f
            ? _lastMapViewportWidth
            : GetVisibleMapAreaWidth(GetControlPanelWidth() + 12f) - GetVerticalScrollbarWidth();
        return Mathf.Max(0f, contentWidth - viewportWidth);
    }

    private float GetVisibleMapAreaWidth(float mapAreaX)
    {
        return Mathf.Max(1f, position.width - mapAreaX - MapViewportRightPadding);
    }

    private float GetHorizontalMapPageStep()
    {
        return Mathf.Max(MapScrollWheelStep, _lastMapViewportWidth * 0.5f);
    }

    private void SetHorizontalMapScroll(float scrollX)
    {
        _scroll.x = Mathf.Clamp(scrollX, 0f, GetHorizontalMapScrollMax());
        Repaint();
    }

    private void SetHorizontalMapScroll(float scrollX, float maxScrollX)
    {
        _scroll.x = Mathf.Clamp(scrollX, 0f, maxScrollX);
        Repaint();
    }

    private string GetDragPasteStatusText()
    {
        if (_dragPasteActive)
            return "Selecting";
        if (_dragPasteHasPayload)
            return $"{_dragPasteWidth}x{_dragPasteHeight} Ready - click destination";
        return "Drag source rectangle";
    }

    private void DrawGridOptimized(float availableHeight)
    {
        float cellSize = Mathf.Clamp(_cellSize, MinCellSize, MaxCellSize);
        int gridW = Mathf.RoundToInt(_map.Width * cellSize);
        int gridH = Mathf.RoundToInt(_map.Height * cellSize);
        float scrollbarWidth = GetVerticalScrollbarWidth();
        float scrollbarHeight = GetHorizontalScrollbarHeight();

        Rect outerRect = GUILayoutUtility.GetRect(
            1f,
            Mathf.Max(1f, position.width),
            availableHeight,
            availableHeight,
            GUILayout.ExpandWidth(true),
            GUILayout.Height(availableHeight));
        outerRect.width = GetVisibleMapAreaWidth(outerRect.x);
        Rect viewportRect = new Rect(
            outerRect.x,
            outerRect.y + scrollbarHeight,
            Mathf.Max(1f, outerRect.width - scrollbarWidth),
            Mathf.Max(1f, outerRect.height - scrollbarHeight));
        Rect contentRect = new Rect(0f, 0f, gridW, gridH);
        _lastMapViewportWidth = viewportRect.width;
        _lastMapContentWidth = contentRect.width;

        _scroll = ClampMapScroll(_scroll, contentRect, viewportRect.width, viewportRect.height);
        HandleMapScrollWheelInput(viewportRect, contentRect, viewportRect.width, viewportRect.height);
        Rect horizontalScrollbarRect = new Rect(
            outerRect.x,
            outerRect.y,
            viewportRect.width,
            scrollbarHeight);
        bool canHandleGridInput = IsMouseOverMapContentViewport(viewportRect);
        if (canHandleGridInput)
        {
            UpdateHoveredCell(viewportRect);
            HandlePaintOptimized(viewportRect);
        }
        else
        {
            ClearGridInteractionStateForNonContentInput();
        }

        Rect gridRect = new Rect(-_scroll.x, -_scroll.y, contentRect.width, contentRect.height);
        GUI.BeginGroup(viewportRect);
        GUI.Box(gridRect, GUIContent.none);

        Rect viewRect = new Rect(_scroll.x, _scroll.y, viewportRect.width, viewportRect.height);

        int xMin = Mathf.Clamp(Mathf.FloorToInt(viewRect.xMin / cellSize), 0, _map.Width - 1);
        int xMax = Mathf.Clamp(Mathf.CeilToInt(viewRect.xMax / cellSize), 0, _map.Width);
        int yMin = Mathf.Clamp(Mathf.FloorToInt(viewRect.yMin / cellSize), 0, _map.Height - 1);
        int yMax = Mathf.Clamp(Mathf.CeilToInt(viewRect.yMax / cellSize), 0, _map.Height);

        if (Event.current.type == EventType.Repaint)
        {
            for (int y = yMin; y < yMax; y++)
            {
                float cy = gridRect.y + y * cellSize;
                for (int x = xMin; x < xMax; x++)
                {
                    Rect cellRect = new Rect(
                        gridRect.x + x * cellSize,
                        cy,
                        cellSize,
                        cellSize
                    );

                    DrawCellFast(cellRect, x, y);
                }
            }

            Handles.BeginGUI();
            Handles.color = GridLineColor;

            for (int x = xMin; x <= xMax; x++)
            {
                float gx = gridRect.x + x * cellSize;
                Handles.DrawLine(
                    new Vector3(gx, gridRect.y + yMin * cellSize),
                    new Vector3(gx, gridRect.y + yMax * cellSize)
                );
            }

            for (int y = yMin; y <= yMax; y++)
            {
                float gy = gridRect.y + y * cellSize;
                Handles.DrawLine(
                    new Vector3(gridRect.x + xMin * cellSize, gy),
                    new Vector3(gridRect.x + xMax * cellSize, gy)
                );
            }

            DrawBrushPreview(gridRect);
            DrawDragPastePreview(gridRect);
            Handles.EndGUI();
        }

        GUI.EndGroup();
        DrawMapHorizontalControls(horizontalScrollbarRect, contentRect.width, viewportRect.width);
        DrawMapVerticalScrollbar(outerRect, viewportRect, contentRect, scrollbarWidth, scrollbarHeight);
        _scroll = ClampMapScroll(_scroll, contentRect, viewportRect.width, viewportRect.height);
    }

    private static float GetHorizontalScrollbarHeight()
    {
        float height = GUI.skin.horizontalScrollbar.fixedHeight;
        return height > 0f ? height : DefaultScrollbarSize;
    }

    private static float GetVerticalScrollbarWidth()
    {
        float width = GUI.skin.verticalScrollbar.fixedWidth;
        return width > 0f ? width : DefaultScrollbarSize;
    }

    private static Vector2 ClampMapScroll(Vector2 scroll, Rect contentRect, float viewportWidth, float viewportHeight)
    {
        scroll.x = Mathf.Clamp(scroll.x, 0f, Mathf.Max(0f, contentRect.width - viewportWidth));
        scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, contentRect.height - viewportHeight));
        return scroll;
    }

    private static bool IsMouseOverMapContentViewport(Rect viewportRect)
    {
        Event e = Event.current;
        return e != null && viewportRect.Contains(e.mousePosition);
    }

    private void ClearGridInteractionStateForNonContentInput()
    {
        _hasHoveredCell = false;

        Event e = Event.current;
        if (e == null || e.type != EventType.MouseUp)
            return;

        _dragPainting = false;
        _lastPaintIndex = -1;

        if (_brushTool == BrushTool.Paste && _dragPasteActive)
        {
            CaptureDragPasteArea();
            Repaint();
        }
    }

    private void HandleMapScrollWheelInput(Rect viewportRect, Rect contentRect, float viewportWidth, float viewportHeight)
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.ScrollWheel || !viewportRect.Contains(e.mousePosition))
            return;

        float maxScrollX = Mathf.Max(0f, contentRect.width - viewportWidth);
        float maxScrollY = Mathf.Max(0f, contentRect.height - viewportHeight);

        bool hasTrackpadHorizontalDelta = Mathf.Abs(e.delta.x) > 0.01f;
        bool useHorizontal = (e.shift || hasTrackpadHorizontalDelta) && maxScrollX > 0f;
        bool useVertical = !useHorizontal && maxScrollY > 0f;
        if (!useHorizontal && !useVertical)
            return;

        if (useHorizontal)
        {
            float delta = hasTrackpadHorizontalDelta ? e.delta.x : e.delta.y;
            _scroll.x = Mathf.Clamp(_scroll.x + delta * MapScrollWheelStep, 0f, maxScrollX);
        }
        else
        {
            _scroll.y = Mathf.Clamp(_scroll.y + e.delta.y * MapScrollWheelStep, 0f, maxScrollY);
        }

        e.Use();
        Repaint();
    }

    private void DrawMapVerticalScrollbar(
        Rect outerRect,
        Rect viewportRect,
        Rect contentRect,
        float scrollbarWidth,
        float scrollbarHeight)
    {
        Rect verticalRect = new Rect(
            outerRect.xMax - scrollbarWidth,
            viewportRect.y,
            scrollbarWidth,
            viewportRect.height);
        Rect cornerRect = new Rect(
            outerRect.xMax - scrollbarWidth,
            outerRect.y,
            scrollbarWidth,
            scrollbarHeight);

        if (contentRect.height > viewportRect.height)
        {
            _scroll.y = GUI.VerticalScrollbar(verticalRect, _scroll.y, viewportRect.height, 0f, contentRect.height);
        }
        else
        {
            _scroll.y = 0f;
            using (new EditorGUI.DisabledScope(true))
                GUI.VerticalScrollbar(verticalRect, 0f, viewportRect.height, 0f, viewportRect.height);
        }

        EditorGUI.DrawRect(cornerRect, new Color(0.16f, 0.16f, 0.16f, 1f));
    }

    private void DrawMapHorizontalControls(Rect rect, float contentWidth, float viewportWidth)
    {
        float maxScrollX = Mathf.Max(0f, contentWidth - viewportWidth);
        float buttonWidth = Mathf.Min(MapScrollbarButtonSize, rect.width * 0.12f);
        Rect jumpStartRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
        Rect pageLeftRect = new Rect(jumpStartRect.xMax + 2f, rect.y, buttonWidth, rect.height);
        Rect jumpEndRect = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);
        Rect pageRightRect = new Rect(jumpEndRect.xMin - buttonWidth - 2f, rect.y, buttonWidth, rect.height);
        Rect sliderRect = new Rect(
            pageLeftRect.xMax + 2f,
            rect.y,
            Mathf.Max(1f, pageRightRect.xMin - pageLeftRect.xMax - 4f),
            rect.height);

        if (maxScrollX <= 0f)
        {
            _scroll.x = 0f;
            DrawDisabledMapHorizontalControls(rect, jumpStartRect, pageLeftRect, sliderRect, pageRightRect, jumpEndRect);
            return;
        }

        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f, 1f));

        float pageStep = Mathf.Max(MapScrollWheelStep, viewportWidth * 0.85f);
        if (GUI.Button(jumpStartRect, "<<", EditorStyles.miniButton))
            SetHorizontalMapScroll(0f, maxScrollX);
        if (GUI.RepeatButton(pageLeftRect, "<", EditorStyles.miniButton))
            SetHorizontalMapScroll(_scroll.x - pageStep, maxScrollX);

        EditorGUI.BeginChangeCheck();
        float nextScrollX = GUI.HorizontalScrollbar(sliderRect, _scroll.x, viewportWidth, 0f, contentWidth);
        if (EditorGUI.EndChangeCheck())
            SetHorizontalMapScroll(nextScrollX, maxScrollX);

        if (GUI.RepeatButton(pageRightRect, ">", EditorStyles.miniButton))
            SetHorizontalMapScroll(_scroll.x + pageStep, maxScrollX);
        if (GUI.Button(jumpEndRect, ">>", EditorStyles.miniButton))
            SetHorizontalMapScroll(maxScrollX, maxScrollX);
    }

    private static void DrawDisabledMapHorizontalControls(
        Rect rect,
        Rect jumpStartRect,
        Rect pageLeftRect,
        Rect sliderRect,
        Rect pageRightRect,
        Rect jumpEndRect)
    {
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f, 1f));
        using (new EditorGUI.DisabledScope(true))
        {
            GUI.Button(jumpStartRect, "<<", EditorStyles.miniButton);
            GUI.Button(pageLeftRect, "<", EditorStyles.miniButton);
            GUI.HorizontalScrollbar(sliderRect, 0f, 1f, 0f, 1f);
            GUI.Button(pageRightRect, ">", EditorStyles.miniButton);
            GUI.Button(jumpEndRect, ">>", EditorStyles.miniButton);
        }
    }

    private void UpdateHoveredCell(Rect viewportRect)
    {
        _hasHoveredCell = false;

        if (!TryGetCellAtMouse(viewportRect, out int x, out int y))
            return;

        _hasHoveredCell = true;
        _hoveredX = x;
        _hoveredY = y;

        Event e = Event.current;
        if (e.type == EventType.MouseMove)
            Repaint();
    }

    private void HandlePaintOptimized(Rect viewportRect)
    {
        Event e = Event.current;
        if (e == null) return;

        if (_brushTool == BrushTool.Paste)
        {
            HandleDragPaste(viewportRect);
            return;
        }

        if (e.type == EventType.MouseUp)
        {
            _dragPainting = false;
            _lastPaintIndex = -1;
            return;
        }

        bool isPaintEvent = e.type == EventType.MouseDown || e.type == EventType.MouseDrag;
        if (!isPaintEvent) return;
        if (e.button != 0 && e.button != 1) return;
        if (!TryGetCellAtMouse(viewportRect, out int x, out int y)) return;

        int index = y * _map.Width + x;
        if (index == _lastPaintIndex && e.type == EventType.MouseDrag)
            return;
        _lastPaintIndex = index;

        bool pick = e.shift || _brushTool == BrushTool.Pick;
        if (!pick)
        {
            if (e.type == EventType.MouseDown)
            {
                UnityEditor.Undo.RecordObject(_map, GetUndoName(e.button));
                _dragPainting = true;
            }
            else if (e.type == EventType.MouseDrag && !_dragPainting)
            {
                UnityEditor.Undo.RecordObject(_map, GetUndoName(e.button));
                _dragPainting = true;
            }
        }

        bool changed = ApplyToolAt(x, y, e.button, pick);
        if (changed)
            EditorUtility.SetDirty(_map);

        e.Use();
        Repaint();
    }

    private bool TryGetCellAtMouse(Rect viewportRect, out int x, out int y)
    {
        x = 0;
        y = 0;

        Event e = Event.current;
        if (e == null || !viewportRect.Contains(e.mousePosition))
            return false;

        float cellSize = Mathf.Clamp(_cellSize, MinCellSize, MaxCellSize);
        float mapX = e.mousePosition.x - viewportRect.x + _scroll.x;
        float mapY = e.mousePosition.y - viewportRect.y + _scroll.y;
        x = Mathf.FloorToInt(mapX / cellSize);
        y = Mathf.FloorToInt(mapY / cellSize);
        return _map.InBounds(x, y);
    }

    private void HandleDragPaste(Rect viewportRect)
    {
        Event e = Event.current;
        if (e == null)
            return;

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            CancelDragPaste();
            e.Use();
            Repaint();
            return;
        }

        bool hasCell = TryGetCellAtMouse(viewportRect, out int x, out int y);

        if (_dragPasteHasPayload && hasCell)
        {
            _dragPasteTargetX = x;
            _dragPasteTargetY = y;

            if (e.type == EventType.MouseMove)
                Repaint();
        }

        if (e.button != 0)
            return;

        if (_dragPasteHasPayload)
        {
            if (e.type == EventType.MouseDown && hasCell)
            {
                UnityEditor.Undo.RecordObject(_map, GetDragPasteUndoName());
                bool changed = ApplyDragPasteAt(x, y);
                if (changed)
                    EditorUtility.SetDirty(_map);

                CancelDragPaste();
                e.Use();
                Repaint();
            }

            return;
        }

        if (e.type == EventType.MouseDown && hasCell)
        {
            BeginDragPasteSelection(x, y);
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseDrag && _dragPasteActive)
        {
            if (hasCell)
                UpdateDragPasteSelection(x, y);

            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseUp && _dragPasteActive)
        {
            if (hasCell)
                UpdateDragPasteSelection(x, y);

            bool changed = CaptureDragPasteArea();
            if (changed)
                EditorUtility.SetDirty(_map);

            e.Use();
            Repaint();
        }
    }

    private void BeginDragPasteSelection(int sourceX, int sourceY)
    {
        _dragPasteActive = true;
        _dragPasteHasPayload = false;
        _dragPasteLayer = _paintLayer;
        _dragPasteSelectionStartX = sourceX;
        _dragPasteSelectionStartY = sourceY;
        _dragPasteSelectionEndX = sourceX;
        _dragPasteSelectionEndY = sourceY;
        _dragPasteTargetX = sourceX;
        _dragPasteTargetY = sourceY;
    }

    private void UpdateDragPasteSelection(int x, int y)
    {
        _dragPasteSelectionEndX = x;
        _dragPasteSelectionEndY = y;
    }

    private void CancelDragPaste()
    {
        _dragPasteActive = false;
        _dragPasteHasPayload = false;
        _dragPasteLogicCells = null;
        _dragPasteAppearanceCells = null;
        _dragPasteWidth = 0;
        _dragPasteHeight = 0;
    }

    private string GetDragPasteUndoName()
    {
        return "Move Map Tile Area";
    }

    private bool ApplyDragPasteAt(int targetX, int targetY)
    {
        if (!_dragPasteHasPayload || _dragPasteWidth <= 0 || _dragPasteHeight <= 0)
            return false;

        return MoveCapturedAreaTo(targetX, targetY);
    }

    private bool CaptureDragPasteArea()
    {
        GetNormalizedDragPasteSelection(out int minX, out int minY, out int maxX, out int maxY);

        _dragPasteActive = false;
        _dragPasteHasPayload = true;
        _dragPasteLayer = _paintLayer;
        _dragPasteSourceX = minX;
        _dragPasteSourceY = minY;
        _dragPasteTargetX = minX;
        _dragPasteTargetY = minY;
        _dragPasteWidth = maxX - minX + 1;
        _dragPasteHeight = maxY - minY + 1;

        int count = _dragPasteWidth * _dragPasteHeight;
        _dragPasteLogicCells = new TileCell[count];
        _dragPasteAppearanceCells = new AppearanceTileCell[count];
        for (int yy = 0; yy < _dragPasteHeight; yy++)
        {
            for (int xx = 0; xx < _dragPasteWidth; xx++)
            {
                _dragPasteLogicCells[yy * _dragPasteWidth + xx] = _map.Get(minX + xx, minY + yy);
                _dragPasteAppearanceCells[yy * _dragPasteWidth + xx] = _map.GetAppearance(minX + xx, minY + yy);
            }
        }

        return false;
    }

    private void GetNormalizedDragPasteSelection(out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = Mathf.Min(_dragPasteSelectionStartX, _dragPasteSelectionEndX);
        minY = Mathf.Min(_dragPasteSelectionStartY, _dragPasteSelectionEndY);
        maxX = Mathf.Max(_dragPasteSelectionStartX, _dragPasteSelectionEndX);
        maxY = Mathf.Max(_dragPasteSelectionStartY, _dragPasteSelectionEndY);
    }

    private bool MoveCapturedAreaTo(int targetX, int targetY)
    {
        if (_dragPasteLogicCells == null || _dragPasteAppearanceCells == null)
            return false;

        int sourceMinX = _dragPasteSourceX;
        int sourceMinY = _dragPasteSourceY;
        int sourceMaxX = _dragPasteSourceX + _dragPasteWidth - 1;
        int sourceMaxY = _dragPasteSourceY + _dragPasteHeight - 1;

        bool changed = ClearLogicArea(sourceMinX, sourceMinY, sourceMaxX, sourceMaxY);
        changed |= ClearAppearanceArea(sourceMinX, sourceMinY, sourceMaxX, sourceMaxY);
        changed |= ApplyLogicPasteArea(targetX, targetY);
        changed |= ApplyAppearancePasteArea(targetX, targetY);
        return changed;
    }

    private bool ApplyLogicPasteArea(int targetX, int targetY)
    {
        if (_dragPasteLogicCells == null)
            return false;

        bool changed = false;
        for (int yy = 0; yy < _dragPasteHeight; yy++)
        {
            for (int xx = 0; xx < _dragPasteWidth; xx++)
            {
                int x = targetX + xx;
                int y = targetY + yy;
                if (!_map.InBounds(x, y))
                    continue;

                var next = _dragPasteLogicCells[yy * _dragPasteWidth + xx];
                var current = _map.Get(x, y);
                if (current.Kind == next.Kind && current.Variant == next.Variant)
                    continue;

                _map.Set(x, y, next);
                changed = true;
            }
        }

        return changed;
    }

    private bool ApplyAppearancePasteArea(int targetX, int targetY)
    {
        if (_dragPasteAppearanceCells == null)
            return false;

        bool changed = false;
        int minChangedX = int.MaxValue;
        int minChangedY = int.MaxValue;
        int maxChangedX = int.MinValue;
        int maxChangedY = int.MinValue;

        for (int yy = 0; yy < _dragPasteHeight; yy++)
        {
            for (int xx = 0; xx < _dragPasteWidth; xx++)
            {
                int x = targetX + xx;
                int y = targetY + yy;
                if (!_map.InBounds(x, y))
                    continue;

                var next = _dragPasteAppearanceCells[yy * _dragPasteWidth + xx];
                var current = _map.GetAppearance(x, y);
                if (current.Kind == next.Kind && current.Variant == next.Variant)
                    continue;

                _map.SetAppearance(x, y, next);
                changed = true;
                minChangedX = Mathf.Min(minChangedX, x);
                minChangedY = Mathf.Min(minChangedY, y);
                maxChangedX = Mathf.Max(maxChangedX, x);
                maxChangedY = Mathf.Max(maxChangedY, y);
            }
        }

        if (changed)
            RefreshAppearanceAutoTilesInBounds(minChangedX, minChangedY, maxChangedX, maxChangedY);

        return changed;
    }

    private bool ClearLogicArea(int minX, int minY, int maxX, int maxY)
    {
        bool changed = false;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var current = _map.Get(x, y);
                if (current.Kind == TileKind.None && current.Variant == 0)
                    continue;

                _map.Set(x, y, default);
                changed = true;
            }
        }

        return changed;
    }

    private bool ClearAppearanceArea(int minX, int minY, int maxX, int maxY)
    {
        bool changed = false;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var current = _map.GetAppearance(x, y);
                if (current.Kind == AppearanceTileKind.None && current.Variant == 0)
                    continue;

                _map.SetAppearance(x, y, default);
                changed = true;
            }
        }

        if (changed)
            RefreshAppearanceAutoTilesInBounds(minX, minY, maxX, maxY);

        return changed;
    }

    private void RefreshAppearanceAutoTilesInBounds(int minX, int minY, int maxX, int maxY)
    {
        for (int y = minY - 1; y <= maxY + 1; y++)
        {
            for (int x = minX - 1; x <= maxX + 1; x++)
                _map.RefreshAppearanceAutoTileAt(x, y);
        }
    }

    private string GetUndoName(int mouseButton)
    {
        bool erase = mouseButton == 1 || _brushTool == BrushTool.Erase;
        if (_paintLayer == PaintLayer.Appearance)
            return erase ? "Erase Appearance Auto Tile" : "Paint Appearance Auto Tile";

        return erase ? "Erase Logic Tile" : "Paint Logic Tile";
    }

    private bool ApplyToolAt(int x, int y, int mouseButton, bool pick)
    {
        if (pick)
        {
            PickAt(x, y);
            return false;
        }

        bool erase = mouseButton == 1 || _brushTool == BrushTool.Erase;
        return _paintLayer == PaintLayer.Appearance
            ? ApplyAppearanceBrush(x, y, erase)
            : ApplyLogicBrush(x, y, erase);
    }

    private void PickAt(int x, int y)
    {
        if (_paintLayer == PaintLayer.Appearance)
        {
            var appearance = _map.GetAppearance(x, y);
            if (appearance.Kind == AppearanceTileKind.None)
            {
                _brushTool = BrushTool.Erase;
                return;
            }

            _appearanceKind = appearance.Kind;
            _brushTool = BrushTool.Paint;
            return;
        }

        var cell = _map.Get(x, y);
        _paintKind = cell.Kind;
        _paintVariant = cell.Variant;
        _brushTool = cell.Kind == TileKind.None ? BrushTool.Erase : BrushTool.Paint;
    }

    private bool ApplyLogicBrush(int centerX, int centerY, bool erase)
    {
        bool changed = false;
        TileCell next = erase ? default : new TileCell { Kind = _paintKind, Variant = (byte)_paintVariant };

        ForEachBrushCell(centerX, centerY, (x, y) =>
        {
            var current = _map.Get(x, y);
            if (current.Kind == next.Kind && current.Variant == next.Variant)
                return;

            _map.Set(x, y, next);
            changed = true;
        });

        return changed;
    }

    private bool ApplyAppearanceBrush(int centerX, int centerY, bool erase)
    {
        bool changed = false;
        AppearanceTileCell next = erase
            ? default
            : new AppearanceTileCell { Kind = _appearanceKind, Variant = 0 };

        GetBrushBounds(centerX, centerY, out int minX, out int minY, out int maxX, out int maxY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!_map.InBounds(x, y))
                    continue;

                var current = _map.GetAppearance(x, y);
                if (current.Kind == next.Kind)
                    continue;

                _map.SetAppearance(x, y, next);
                changed = true;
            }
        }

        if (!changed)
            return false;

        for (int y = minY - 1; y <= maxY + 1; y++)
        {
            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                _map.RefreshAppearanceAutoTileAt(x, y);
            }
        }

        return true;
    }

    private void ForEachBrushCell(int centerX, int centerY, Action<int, int> action)
    {
        GetBrushBounds(centerX, centerY, out int minX, out int minY, out int maxX, out int maxY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (_map.InBounds(x, y))
                    action(x, y);
            }
        }
    }

    private void GetBrushBounds(int centerX, int centerY, out int minX, out int minY, out int maxX, out int maxY)
    {
        int before = (_brushSize - 1) / 2;
        int after = _brushSize - before - 1;

        minX = centerX - before;
        minY = centerY - before;
        maxX = centerX + after;
        maxY = centerY + after;
    }

    private void DrawCellFast(Rect r, int x, int y)
    {
        var cell = _map.Get(x, y);
        Color baseColor = GetLogicColor(cell.Kind);
        float t = (cell.Variant & 0x0F) / 15f;
        Color logicColor = Color.Lerp(baseColor, Color.white, 0.15f * t);

        EditorGUI.DrawRect(r, logicColor);

        var appearance = _map.GetAppearance(x, y);
        if (_showAppearanceLayer && appearance.Kind != AppearanceTileKind.None)
        {
            Rect appearanceRect = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);
            DrawAppearancePreview(appearanceRect, appearance.Kind, appearance.Variant, 0.95f);
        }

        if (_paintLayer == PaintLayer.Appearance && _showAppearanceMaskLabels && appearance.Kind != AppearanceTileKind.None)
        {
            GUI.Label(r, $"{(byte)appearance.Kind}:{appearance.Variant}", CellLabelStyle);
        }
        else if (_showLogicLabels && cell.Kind != TileKind.None)
        {
            GUI.Label(r, $"{(byte)cell.Kind}:{cell.Variant}", CellLabelStyle);
        }
    }

    private void DrawBrushPreview(Rect gridRect)
    {
        if (!_hasHoveredCell || _brushTool == BrushTool.Paste)
            return;

        GetBrushBounds(_hoveredX, _hoveredY, out int minX, out int minY, out int maxX, out int maxY);
        minX = Mathf.Clamp(minX, 0, _map.Width - 1);
        minY = Mathf.Clamp(minY, 0, _map.Height - 1);
        maxX = Mathf.Clamp(maxX, 0, _map.Width - 1);
        maxY = Mathf.Clamp(maxY, 0, _map.Height - 1);

        float cellSize = Mathf.Clamp(_cellSize, MinCellSize, MaxCellSize);
        Rect brushRect = new Rect(
            gridRect.x + minX * cellSize,
            gridRect.y + minY * cellSize,
            (maxX - minX + 1) * cellSize,
            (maxY - minY + 1) * cellSize
        );

        Color color = _paintLayer == PaintLayer.Appearance ? BrushAppearanceColor : BrushLogicColor;
        Handles.color = color;
        Handles.DrawAAPolyLine(
            3f,
            new Vector3(brushRect.xMin, brushRect.yMin),
            new Vector3(brushRect.xMax, brushRect.yMin),
            new Vector3(brushRect.xMax, brushRect.yMax),
            new Vector3(brushRect.xMin, brushRect.yMax),
            new Vector3(brushRect.xMin, brushRect.yMin)
        );
    }

    private void DrawDragPastePreview(Rect gridRect)
    {
        if (!_dragPasteActive && !_dragPasteHasPayload)
            return;

        float cellSize = Mathf.Clamp(_cellSize, MinCellSize, MaxCellSize);
        Color previewColor = _dragPasteLayer == PaintLayer.Appearance ? BrushAppearanceColor : BrushLogicColor;

        if (_dragPasteActive)
        {
            GetNormalizedDragPasteSelection(out int minX, out int minY, out int maxX, out int maxY);
            Rect selectionRect = GetCellAreaRect(
                gridRect,
                minX,
                minY,
                maxX - minX + 1,
                maxY - minY + 1,
                cellSize);
            Handles.DrawSolidRectangleWithOutline(selectionRect, WithAlpha(previewColor, 0.12f), previewColor);
            return;
        }

        Rect sourceRect = GetCellAreaRect(
            gridRect,
            _dragPasteSourceX,
            _dragPasteSourceY,
            _dragPasteWidth,
            _dragPasteHeight,
            cellSize);
        Rect targetRect = GetCellAreaRect(
            gridRect,
            _dragPasteTargetX,
            _dragPasteTargetY,
            _dragPasteWidth,
            _dragPasteHeight,
            cellSize);

        Handles.DrawSolidRectangleWithOutline(sourceRect, Color.clear, WithAlpha(Color.white, 0.95f));
        Handles.DrawSolidRectangleWithOutline(targetRect, WithAlpha(previewColor, 0.18f), previewColor);
        Handles.color = previewColor;
        Handles.DrawAAPolyLine(
            2f,
            new Vector3(sourceRect.center.x, sourceRect.center.y),
            new Vector3(targetRect.center.x, targetRect.center.y)
        );
    }

    private static Rect GetCellRect(Rect gridRect, int x, int y, float cellSize)
    {
        return new Rect(
            gridRect.x + x * cellSize,
            gridRect.y + y * cellSize,
            cellSize,
            cellSize
        );
    }

    private static Rect GetCellAreaRect(Rect gridRect, int x, int y, int width, int height, float cellSize)
    {
        return new Rect(
            gridRect.x + x * cellSize,
            gridRect.y + y * cellSize,
            Mathf.Max(1, width) * cellSize,
            Mathf.Max(1, height) * cellSize
        );
    }

    private void DrawAutoTilePreview(Rect r, AppearanceTileKind kind, byte mask, float alpha)
    {
        Color color = GetAppearanceColor(kind);
        EditorGUI.DrawRect(r, WithAlpha(color, 0.22f * alpha));

        Rect center = new Rect(
            r.x + r.width * 0.32f,
            r.y + r.height * 0.32f,
            r.width * 0.36f,
            r.height * 0.36f
        );

        Color strong = WithAlpha(color, 0.9f * alpha);

        if ((mask & MapAsset.AppearanceNorth) != 0)
            EditorGUI.DrawRect(new Rect(center.x, r.y, center.width, center.yMax - r.y), strong);
        if ((mask & MapAsset.AppearanceEast) != 0)
            EditorGUI.DrawRect(new Rect(center.x, center.y, r.xMax - center.x, center.height), strong);
        if ((mask & MapAsset.AppearanceSouth) != 0)
            EditorGUI.DrawRect(new Rect(center.x, center.y, center.width, r.yMax - center.y), strong);
        if ((mask & MapAsset.AppearanceWest) != 0)
            EditorGUI.DrawRect(new Rect(r.x, center.y, center.xMax - r.x, center.height), strong);

        float cornerW = r.width * 0.25f;
        float cornerH = r.height * 0.25f;
        if ((mask & MapAsset.AppearanceNorthEast) != 0)
            EditorGUI.DrawRect(new Rect(r.xMax - cornerW, r.y, cornerW, cornerH), strong);
        if ((mask & MapAsset.AppearanceSouthEast) != 0)
            EditorGUI.DrawRect(new Rect(r.xMax - cornerW, r.yMax - cornerH, cornerW, cornerH), strong);
        if ((mask & MapAsset.AppearanceSouthWest) != 0)
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - cornerH, cornerW, cornerH), strong);
        if ((mask & MapAsset.AppearanceNorthWest) != 0)
            EditorGUI.DrawRect(new Rect(r.x, r.y, cornerW, cornerH), strong);

        EditorGUI.DrawRect(center, strong);
    }

    private void DrawAppearancePreview(Rect r, AppearanceTileKind kind, byte mask, float alpha)
    {
        if (TryDrawAppearanceTexture(r, kind, mask, alpha))
            return;

        DrawAutoTilePreview(r, kind, mask, alpha);
    }

    private bool TryDrawAppearanceTexture(Rect r, AppearanceTileKind kind, byte mask, float alpha)
    {
        if (_map == null || _map.AppearancePalette == null)
            return false;

        if (!_map.AppearancePalette.TryGetMaterial(kind, mask, out var material) || material == null)
            return false;

        Texture texture = GetMaterialTexture(material);
        if (texture == null)
            return false;

        Rect texCoords = GetMaterialTexCoords(material);
        Color previousColor = GUI.color;
        GUI.color = WithAlpha(Color.white, alpha);
        GUI.DrawTextureWithTexCoords(r, texture, texCoords, true);
        GUI.color = previousColor;
        return true;
    }

    private static Texture GetMaterialTexture(Material material)
    {
        Texture texture = material.mainTexture;
        if (texture != null)
            return texture;

        if (material.HasProperty("_BaseMap"))
        {
            texture = material.GetTexture("_BaseMap");
            if (texture != null)
                return texture;
        }

        if (material.HasProperty("_MainTex"))
            return material.GetTexture("_MainTex");

        return null;
    }

    private static Rect GetMaterialTexCoords(Material material)
    {
        Vector2 scale = material.mainTextureScale;
        Vector2 offset = material.mainTextureOffset;

        if ((scale == Vector2.one && offset == Vector2.zero) && material.HasProperty("_BaseMap"))
        {
            scale = material.GetTextureScale("_BaseMap");
            offset = material.GetTextureOffset("_BaseMap");
        }

        if ((scale == Vector2.one && offset == Vector2.zero) && material.HasProperty("_MainTex"))
        {
            scale = material.GetTextureScale("_MainTex");
            offset = material.GetTextureOffset("_MainTex");
        }

        if (Mathf.Approximately(scale.x, 0f))
            scale.x = 1f;
        if (Mathf.Approximately(scale.y, 0f))
            scale.y = 1f;

        return new Rect(offset.x, offset.y, scale.x, scale.y);
    }

    private Color GetLogicColor(TileKind kind)
    {
        switch (kind)
        {
            case TileKind.None: return new Color(0.2f, 0.2f, 0.2f, 0.35f);
            case TileKind.Floor: return new Color(0.3f, 0.75f, 0.35f, 0.85f);
            case TileKind.Wall: return new Color(0.55f, 0.55f, 0.55f, 0.9f);
            case TileKind.Spawn: return new Color(0.35f, 0.55f, 0.85f, 0.9f);
            default: return new Color(1, 0, 1, 0.9f);
        }
    }

    private Color GetAppearanceColor(AppearanceTileKind kind)
    {
        if (_map != null && _map.AppearancePalette != null)
            return _map.AppearancePalette.GetPreviewColor(kind);

        for (int i = 0; i < AppearancePalette.Length; i++)
        {
            if (AppearancePalette[i].Kind == kind)
                return AppearancePalette[i].Color;
        }

        return AppearanceAutoTilePalette.GetBuiltInPreviewColor(kind);
    }

    private Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void DrawSizeControls()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Map Size", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            int newW = EditorGUILayout.DelayedIntField("Width", _map.Width);
            int newH = EditorGUILayout.DelayedIntField("Height", _map.Height);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                newW = Mathf.Clamp(newW, 1, 256);
                newH = Mathf.Clamp(newH, 1, 256);

                if (newW != _map.Width || newH != _map.Height)
                {
                    ResizeMap(newW, newH);
                }
            }

            EditorGUILayout.HelpBox("Width/Height 변경 시 즉시 반영됨 (로직/외관 데이터 유지)", MessageType.Info);
        }
    }

    private void ResizeMap(int w, int h)
    {
        _map.EnsureSize();
        UnityEditor.Undo.RecordObject(_map, "Resize Map");

        int oldW = _map.Width;
        int oldH = _map.Height;
        var oldCells = _map.Cells;
        var oldAppearanceCells = _map.AppearanceCells;

        _map.Width = w;
        _map.Height = h;
        _map.Cells = new TileCell[w * h];
        _map.AppearanceCells = new AppearanceTileCell[w * h];

        int copyW = Mathf.Min(oldW, w);
        int copyH = Mathf.Min(oldH, h);

        for (int y = 0; y < copyH; y++)
        {
            for (int x = 0; x < copyW; x++)
            {
                int oldIdx = y * oldW + x;
                int newIdx = y * w + x;

                if (oldCells != null && oldIdx < oldCells.Length)
                    _map.Cells[newIdx] = oldCells[oldIdx];
                if (oldAppearanceCells != null && oldIdx < oldAppearanceCells.Length)
                    _map.AppearanceCells[newIdx] = oldAppearanceCells[oldIdx];
            }
        }

        _map.RebuildAppearanceAutoTiles();
        EditorUtility.SetDirty(_map);
    }

    private void DrawPalette()
    {
        if (_paintLayer == PaintLayer.Appearance)
            DrawAppearancePalette();
        else
            DrawLogicPalette();
    }

    private void DrawLogicPalette()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Logic Palette", EditorStyles.boldLabel);

            _paintKind = (TileKind)EditorGUILayout.EnumPopup("TileKind", _paintKind);
            _paintVariant = EditorGUILayout.IntSlider("Variant", _paintVariant, 0, 15);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill Logic"))
            {
                UnityEditor.Undo.RecordObject(_map, "Fill Logic");
                var v = new TileCell { Kind = _paintKind, Variant = (byte)_paintVariant };
                for (int i = 0; i < _map.Cells.Length; i++) _map.Cells[i] = v;
                EditorUtility.SetDirty(_map);
            }
            if (GUILayout.Button("Clear Logic"))
            {
                UnityEditor.Undo.RecordObject(_map, "Clear Logic");
                for (int i = 0; i < _map.Cells.Length; i++) _map.Cells[i] = default;
                EditorUtility.SetDirty(_map);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "로직 타일은 서버 이동/스폰 판정용입니다. 외관 장식은 위의 Appearance Auto Tile 탭에서 별도 레이어로 칠합니다.",
                MessageType.None
            );
        }
    }

    private void DrawAppearancePalette()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Appearance Auto Tile Palette", EditorStyles.boldLabel);

            if (_map.AppearancePalette == null)
            {
                EditorGUILayout.HelpBox(
                    "Appearance Palette Asset이 아직 없습니다. Create Appearance Palette를 눌러 만든 뒤 Material/Prefab을 할당하면 실제 외관 Asset이 적용됩니다.",
                    MessageType.Warning);
            }

            DrawAppearanceQuickSetup();
            EditorGUILayout.Space(6);

            DrawAppearanceSwatches();
            DrawSelectedAppearancePreview();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill Appearance"))
            {
                UnityEditor.Undo.RecordObject(_map, "Fill Appearance");
                var v = new AppearanceTileCell { Kind = _appearanceKind, Variant = 0 };
                for (int i = 0; i < _map.AppearanceCells.Length; i++) _map.AppearanceCells[i] = v;
                _map.RebuildAppearanceAutoTiles();
                EditorUtility.SetDirty(_map);
            }
            if (GUILayout.Button("Clear Appearance"))
            {
                UnityEditor.Undo.RecordObject(_map, "Clear Appearance");
                for (int i = 0; i < _map.AppearanceCells.Length; i++) _map.AppearanceCells[i] = default;
                EditorUtility.SetDirty(_map);
            }
            if (GUILayout.Button("Rebuild Auto Tiles"))
            {
                UnityEditor.Undo.RecordObject(_map, "Rebuild Appearance Auto Tiles");
                _map.RebuildAppearanceAutoTiles();
                EditorUtility.SetDirty(_map);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "왼쪽 드래그는 선택한 외관 타일을 칠하고, 오른쪽 드래그는 지웁니다. Pick 또는 Shift+클릭으로 이미 칠한 외관 타일을 샘플링합니다. Variant는 주변 연결에 맞춰 자동 갱신됩니다.",
                MessageType.None
            );
        }
    }

    private void DrawAppearanceQuickSetup()
    {
        _showAppearanceQuickSetup = EditorGUILayout.Foldout(_showAppearanceQuickSetup, "Quick Setup", true);
        if (!_showAppearanceQuickSetup)
            return;

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.HelpBox(
                "선택한 Material/Prefab/Sprite/Texture를 현재 Palette에 바로 등록합니다. 1개 선택 시 Default로, 여러 개 선택 시 이름 끝 숫자(0-255) 또는 정렬 순서로 Auto Mask Variant에 들어갑니다.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _quickAppearanceKind = (AppearanceTileKind)EditorGUILayout.EnumPopup("Target Kind", _quickAppearanceKind);
                if (EditorGUI.EndChangeCheck())
                {
                    _appearanceKind = _quickAppearanceKind;
                    if (string.IsNullOrWhiteSpace(_quickDisplayName))
                        _quickDisplayName = _quickAppearanceKind.ToString();
                    _quickPreviewColor = GetAppearanceColor(_quickAppearanceKind);
                }

                if (GUILayout.Button("Load", GUILayout.Width(64f)))
                    LoadQuickSetupFromPalette();
            }

            _quickDisplayName = EditorGUILayout.DelayedTextField("Display Name", GetQuickDisplayName());
            _quickPreviewColor = EditorGUILayout.ColorField("Preview Color", _quickPreviewColor);
            _quickDefaultMaterial = (Material)EditorGUILayout.ObjectField("Default Material", _quickDefaultMaterial, typeof(Material), false);
            _quickDefaultPrefab = (GameObject)EditorGUILayout.ObjectField("Default Prefab", _quickDefaultPrefab, typeof(GameObject), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                _quickSingleSelectionAsDefault = EditorGUILayout.ToggleLeft("Single asset becomes Default", _quickSingleSelectionAsDefault, GUILayout.Width(190f));
                _quickReplaceVariants = EditorGUILayout.ToggleLeft("Replace Variants", _quickReplaceVariants, GUILayout.Width(140f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_map == null || _map.AppearancePalette == null))
                {
                    if (GUILayout.Button("Apply Manual Slot"))
                        ApplyQuickManualSlot();

                    if (GUILayout.Button("Use Selected Assets"))
                        ApplyQuickSetupFromSelection();
                }

                if (_map != null && _map.AppearancePalette == null && GUILayout.Button("Create Palette"))
                    CreateNewAppearancePalette();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Texture Sheet Grid", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _quickSheetColumns = Mathf.Clamp(EditorGUILayout.IntField("Columns", _quickSheetColumns), 1, 32);
                _quickSheetRows = Mathf.Clamp(EditorGUILayout.IntField("Rows", _quickSheetRows), 1, 32);
            }

            _quickSheetMaskOrder = (TextureSheetMaskOrder)EditorGUILayout.EnumPopup("Mask Order", _quickSheetMaskOrder);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_quickSheetMaskOrder == TextureSheetMaskOrder.Blob47Numeric))
                {
                    _quickSheetStartMask = Mathf.Clamp(EditorGUILayout.IntField("Start Mask", _quickSheetStartMask), 0, 255);
                }

                int maxCell = Mathf.Max(0, _quickSheetColumns * _quickSheetRows - 1);
                _quickSheetDefaultCell = Mathf.Clamp(EditorGUILayout.IntField("Default Cell", _quickSheetDefaultCell), 0, maxCell);
            }

            _quickSheetAssignDefault = EditorGUILayout.ToggleLeft("Assign Default from grid cell", _quickSheetAssignDefault);

            using (new EditorGUI.DisabledScope(_map == null || _map.AppearancePalette == null))
            {
                if (GUILayout.Button("Use Selected Texture Grid"))
                    ApplyQuickTextureGridFromSelection();
            }
        }
    }

    private void DrawAppearanceSwatches()
    {
        if (_map != null && _map.AppearancePalette != null && _map.AppearancePalette.Tiles != null && _map.AppearancePalette.Tiles.Length > 0)
        {
            DrawAppearanceAssetSwatches(_map.AppearancePalette.Tiles);
            return;
        }

        int columns = Mathf.Max(1, Mathf.FloorToInt((_controlPanelWidth - 16f) / 132f));
        bool rowOpen = false;

        for (int i = 0; i < AppearancePalette.Length; i++)
        {
            if (i % columns == 0)
            {
                if (rowOpen)
                    EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                rowOpen = true;
            }

            DrawAppearancePaletteButton(AppearancePalette[i]);
            GUILayout.Space(4);
        }

        if (rowOpen)
            EditorGUILayout.EndHorizontal();
    }

    private void DrawAppearanceAssetSwatches(AppearanceAutoTileDefinition[] definitions)
    {
        int columns = Mathf.Max(1, Mathf.FloorToInt((_controlPanelWidth - 16f) / 132f));
        bool rowOpen = false;
        int drawn = 0;

        for (int i = 0; i < definitions.Length; i++)
        {
            var definition = definitions[i];
            if (definition == null || definition.Kind == AppearanceTileKind.None)
                continue;

            if (drawn % columns == 0)
            {
                if (rowOpen)
                    EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                rowOpen = true;
            }

            string label = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.Kind.ToString()
                : definition.DisplayName;
            DrawAppearancePaletteButton(
                definition.Kind,
                label,
                definition.Kind.ToString(),
                definition.PreviewColor,
                definition.DefaultMaterial != null || definition.DefaultPrefab != null);
            GUILayout.Space(4);
            drawn++;
        }

        if (rowOpen)
            EditorGUILayout.EndHorizontal();
    }

    private void DrawAppearancePaletteButton(AppearancePaletteItem item)
    {
        DrawAppearancePaletteButton(item.Kind, item.Label, item.Kind.ToString(), item.Color, false);
    }

    private void DrawAppearancePaletteButton(AppearanceTileKind kind, string label, string subLabel, Color color, bool hasAsset)
    {
        Rect r = GUILayoutUtility.GetRect(124f, 46f, GUILayout.ExpandWidth(false));
        if (GUI.Button(r, GUIContent.none, GUIStyle.none))
        {
            _appearanceKind = kind;
            _brushTool = BrushTool.Paint;
            CancelDragPaste();
        }

        bool selected = _appearanceKind == kind;
        EditorGUI.DrawRect(r, selected ? PaletteSelectedColor : PaletteNormalColor);

        Rect swatch = new Rect(r.x + 6f, r.y + 6f, 34f, 34f);
        DrawAppearancePreview(swatch, kind, byte.MaxValue, 1f);

        Rect labelRect = new Rect(r.x + 46f, r.y + 7f, r.width - 52f, 18f);
        GUI.Label(labelRect, label, selected ? EditorStyles.whiteBoldLabel : EditorStyles.boldLabel);

        Rect enumRect = new Rect(r.x + 46f, r.y + 25f, r.width - 52f, 16f);
        GUI.Label(enumRect, hasAsset ? $"{subLabel}  Asset" : subLabel, EditorStyles.miniLabel);
    }

    private void DrawSelectedAppearancePreview()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();

        Rect preview = GUILayoutUtility.GetRect(54f, 54f, GUILayout.Width(54f), GUILayout.Height(54f));
        DrawAppearancePreview(preview, _appearanceKind, byte.MaxValue, 1f);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(_appearanceKind.ToString(), EditorStyles.boldLabel);
        EditorGUILayout.LabelField(GetSelectedAppearanceAssetStatus(), EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private string GetSelectedAppearanceAssetStatus()
    {
        if (_map == null || _map.AppearancePalette == null)
            return "No palette asset assigned. The grid uses color preview only.";

        if (!_map.AppearancePalette.TryGetDefinition(_appearanceKind, out var definition))
            return "This kind is not defined in the assigned palette.";

        bool hasDefaultMaterial = definition.DefaultMaterial != null;
        bool hasDefaultPrefab = definition.DefaultPrefab != null;
        int variantCount = definition.Variants != null ? definition.Variants.Length : 0;

        return $"Default Material: {(hasDefaultMaterial ? definition.DefaultMaterial.name : "None")}, Default Prefab: {(hasDefaultPrefab ? definition.DefaultPrefab.name : "None")}, Mask Overrides: {variantCount}";
    }

    private string GetQuickDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(_quickDisplayName))
            return _quickDisplayName;

        return _quickAppearanceKind == AppearanceTileKind.None
            ? "Appearance Tile"
            : _quickAppearanceKind.ToString();
    }

    private void LoadQuickSetupFromPalette()
    {
        _appearanceKind = _quickAppearanceKind;
        _quickDisplayName = _quickAppearanceKind.ToString();
        _quickPreviewColor = GetAppearanceColor(_quickAppearanceKind);
        _quickDefaultMaterial = null;
        _quickDefaultPrefab = null;

        if (_map == null || _map.AppearancePalette == null)
            return;

        if (!_map.AppearancePalette.TryGetDefinition(_quickAppearanceKind, out var definition))
            return;

        _quickDisplayName = string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.Kind.ToString()
            : definition.DisplayName;
        _quickPreviewColor = definition.PreviewColor;
        _quickDefaultMaterial = definition.DefaultMaterial;
        _quickDefaultPrefab = definition.DefaultPrefab;
    }

    private void ApplyQuickManualSlot()
    {
        if (!TryGetQuickPalette(out var palette))
            return;

        UnityEditor.Undo.RecordObject(palette, "Apply Appearance Palette Slot");
        var definition = GetOrCreateAppearanceDefinition(palette, _quickAppearanceKind);
        ApplyQuickDefinitionMetadata(definition);

        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
        _appearanceKind = _quickAppearanceKind;
        Repaint();
    }

    private void ApplyQuickSetupFromSelection()
    {
        if (!TryGetQuickPalette(out var palette))
            return;

        var assets = CollectQuickAppearanceAssetsFromSelection();
        if (assets.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No Supported Assets",
                "Project 창에서 Material, Prefab, Sprite, Texture 또는 폴더를 선택한 뒤 다시 눌러줘.",
                "OK");
            return;
        }

        UnityEditor.Undo.RecordObject(palette, "Use Selected Appearance Assets");
        var definition = GetOrCreateAppearanceDefinition(palette, _quickAppearanceKind);
        ApplyQuickDefinitionMetadata(definition);

        if (assets.Count == 1 && _quickSingleSelectionAsDefault)
        {
            ApplyQuickAssetAsDefault(definition, assets[0]);
        }
        else
        {
            ApplyQuickAssetsAsVariants(palette, definition, assets);
        }

        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _appearanceKind = _quickAppearanceKind;
        _quickDefaultMaterial = definition.DefaultMaterial;
        _quickDefaultPrefab = definition.DefaultPrefab;
        Repaint();
    }

    private void ApplyQuickTextureGridFromSelection()
    {
        if (!TryGetQuickPalette(out var palette))
            return;

        Texture2D texture = GetSelectedTexture();
        if (texture == null)
        {
            EditorUtility.DisplayDialog(
                "Texture Required",
                "Project 창에서 PNG/JPG Texture 또는 해당 Texture의 Sprite를 하나 선택한 뒤 다시 눌러줘.",
                "OK");
            return;
        }

        var assets = BuildQuickGridAssets(texture);
        if (assets.Count == 0)
            return;

        UnityEditor.Undo.RecordObject(palette, "Use Selected Texture Grid");
        var definition = GetOrCreateAppearanceDefinition(palette, _quickAppearanceKind);
        ApplyQuickDefinitionMetadata(definition);
        ApplyQuickAssetsAsVariants(palette, definition, assets);

        if (_quickSheetAssignDefault && _quickSheetDefaultCell >= 0 && _quickSheetDefaultCell < assets.Count)
            ApplyQuickAssetAsDefault(definition, assets[_quickSheetDefaultCell]);

        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _appearanceKind = _quickAppearanceKind;
        _quickDefaultMaterial = definition.DefaultMaterial;
        _quickDefaultPrefab = definition.DefaultPrefab;
        Repaint();
    }

    private bool TryGetQuickPalette(out AppearanceAutoTilePalette palette)
    {
        palette = _map != null ? _map.AppearancePalette : null;
        if (palette != null && _quickAppearanceKind != AppearanceTileKind.None)
            return true;

        EditorUtility.DisplayDialog(
            "Palette Required",
            _quickAppearanceKind == AppearanceTileKind.None
                ? "None은 외관 타일로 등록할 수 없습니다. Target Kind를 다른 값으로 골라줘."
                : "먼저 Appearance Palette를 만들거나 Map Asset에 할당해줘.",
            "OK");
        return false;
    }

    private void ApplyQuickDefinitionMetadata(AppearanceAutoTileDefinition definition)
    {
        definition.Kind = _quickAppearanceKind;
        definition.DisplayName = GetQuickDisplayName();
        definition.PreviewColor = _quickPreviewColor;
        definition.DefaultMaterial = _quickDefaultMaterial;
        definition.DefaultPrefab = _quickDefaultPrefab;
    }

    private void ApplyQuickAssetAsDefault(AppearanceAutoTileDefinition definition, QuickAppearanceAsset asset)
    {
        if (asset.Prefab != null)
        {
            definition.DefaultPrefab = asset.Prefab;
            _quickDefaultPrefab = asset.Prefab;
            return;
        }

        Material material = asset.Material;
        if (material == null && (asset.Sprite != null || asset.Texture != null))
            material = CreateMaterialForQuickAsset(_map.AppearancePalette, definition, asset, -1);

        if (material != null)
        {
            definition.DefaultMaterial = material;
            _quickDefaultMaterial = material;
        }
    }

    private Texture2D GetSelectedTexture()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected is Texture2D texture)
            return texture;
        if (selected is Sprite sprite)
            return sprite.texture;

        string path = AssetDatabase.GetAssetPath(selected);
        if (!string.IsNullOrEmpty(path))
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        return null;
    }

    private List<QuickAppearanceAsset> BuildQuickGridAssets(Texture2D texture)
    {
        var assets = new List<QuickAppearanceAsset>();
        int columns = Mathf.Max(1, _quickSheetColumns);
        int rows = Mathf.Max(1, _quickSheetRows);
        int[] maskOrder = _quickSheetMaskOrder == TextureSheetMaskOrder.Blob47Numeric
            ? Blob47Masks
            : null;
        int maxCount = maskOrder != null
            ? Mathf.Min(columns * rows, maskOrder.Length)
            : Mathf.Min(columns * rows, 256 - _quickSheetStartMask);
        if (maxCount <= 0)
            return assets;

        float cellW = texture.width / (float)columns;
        float cellH = texture.height / (float)rows;
        string baseName = SanitizeFileName(texture.name);

        for (int i = 0; i < maxCount; i++)
        {
            int col = i % columns;
            int rowFromTop = i / columns;
            float x = col * cellW;
            float y = texture.height - ((rowFromTop + 1) * cellH);

            assets.Add(new QuickAppearanceAsset
            {
                Name = $"{baseName}_{i:000}",
                Source = texture,
                Texture = texture,
                TextureRect = new Rect(x, y, cellW, cellH),
                HasTextureRect = true,
                ExplicitMask = maskOrder != null ? maskOrder[i] : _quickSheetStartMask + i,
                HasExplicitMask = true
            });
        }

        return assets;
    }

    private void ApplyQuickAssetsAsVariants(
        AppearanceAutoTilePalette palette,
        AppearanceAutoTileDefinition definition,
        List<QuickAppearanceAsset> assets)
    {
        var variants = _quickReplaceVariants
            ? new List<AppearanceAutoTileVariant>()
            : new List<AppearanceAutoTileVariant>(definition.Variants ?? Array.Empty<AppearanceAutoTileVariant>());
        variants.RemoveAll(variant => variant == null);

        for (int i = 0; i < assets.Count; i++)
        {
            var asset = assets[i];
            int mask = ResolveQuickMask(asset, i);
            if (mask < 0 || mask > 255)
                continue;

            Material material = asset.Material;
            if (material == null && (asset.Sprite != null || asset.Texture != null))
                material = CreateMaterialForQuickAsset(palette, definition, asset, mask);

            GameObject prefab = asset.Prefab;
            if (material == null && prefab == null)
                continue;

            var variant = GetOrCreateVariant(variants, mask);
            if (material != null)
                variant.Material = material;
            if (prefab != null)
                variant.Prefab = prefab;
        }

        variants.Sort((a, b) => a.Mask.CompareTo(b.Mask));
        definition.Variants = variants.ToArray();
    }

    private static AppearanceAutoTileVariant GetOrCreateVariant(List<AppearanceAutoTileVariant> variants, int mask)
    {
        for (int i = 0; i < variants.Count; i++)
        {
            var variant = variants[i];
            if (variant != null && variant.Mask == mask)
                return variant;
        }

        var created = new AppearanceAutoTileVariant { Mask = mask };
        variants.Add(created);
        return created;
    }

    private static AppearanceAutoTileDefinition GetOrCreateAppearanceDefinition(
        AppearanceAutoTilePalette palette,
        AppearanceTileKind kind)
    {
        if (palette.Tiles != null)
        {
            for (int i = 0; i < palette.Tiles.Length; i++)
            {
                var definition = palette.Tiles[i];
                if (definition != null && definition.Kind == kind)
                    return definition;
            }
        }

        var list = new List<AppearanceAutoTileDefinition>(palette.Tiles ?? Array.Empty<AppearanceAutoTileDefinition>());
        var created = new AppearanceAutoTileDefinition
        {
            Kind = kind,
            DisplayName = kind.ToString(),
            PreviewColor = AppearanceAutoTilePalette.GetBuiltInPreviewColor(kind),
            Variants = Array.Empty<AppearanceAutoTileVariant>()
        };
        list.Add(created);
        palette.Tiles = list.ToArray();
        return created;
    }

    private List<QuickAppearanceAsset> CollectQuickAppearanceAssetsFromSelection()
    {
        var assets = new List<QuickAppearanceAsset>();
        var selectedObjects = Selection.objects;
        if (selectedObjects == null)
            return assets;

        for (int i = 0; i < selectedObjects.Length; i++)
            AppendQuickAppearanceAssets(selectedObjects[i], assets);

        assets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return assets;
    }

    private void AppendQuickAppearanceAssets(UnityEngine.Object selectedObject, List<QuickAppearanceAsset> assets)
    {
        if (selectedObject == null)
            return;

        string path = AssetDatabase.GetAssetPath(selectedObject);
        if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
        {
            string[] guids = AssetDatabase.FindAssets("", new[] { path });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                AppendQuickAppearanceAssets(asset, assets);
            }
            return;
        }

        if (selectedObject is Material material)
        {
            assets.Add(new QuickAppearanceAsset
            {
                Name = material.name,
                Source = material,
                Material = material
            });
            return;
        }

        if (selectedObject is GameObject prefab)
        {
            assets.Add(new QuickAppearanceAsset
            {
                Name = prefab.name,
                Source = prefab,
                Prefab = prefab
            });
            return;
        }

        if (selectedObject is Sprite sprite)
        {
            assets.Add(new QuickAppearanceAsset
            {
                Name = sprite.name,
                Source = sprite,
                Sprite = sprite,
                Texture = sprite.texture
            });
            return;
        }

        if (selectedObject is Texture2D texture)
        {
            AppendSpritesOrTextureFromPath(texture, path, assets);
        }
    }

    private void AppendSpritesOrTextureFromPath(Texture2D texture, string path, List<QuickAppearanceAsset> assets)
    {
        bool addedSprites = false;
        if (!string.IsNullOrEmpty(path))
        {
            var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite sprite)
                {
                    assets.Add(new QuickAppearanceAsset
                    {
                        Name = sprite.name,
                        Source = sprite,
                        Sprite = sprite,
                        Texture = sprite.texture
                    });
                    addedSprites = true;
                }
            }
        }

        if (addedSprites)
            return;

        assets.Add(new QuickAppearanceAsset
        {
            Name = texture.name,
            Source = texture,
            Texture = texture
        });
    }

    private Material CreateMaterialForQuickAsset(
        AppearanceAutoTilePalette palette,
        AppearanceAutoTileDefinition definition,
        QuickAppearanceAsset asset,
        int mask)
    {
        var texture = asset.Sprite != null ? asset.Sprite.texture : asset.Texture;
        if (texture == null)
            return null;

        string folder = EnsureGeneratedMaterialFolder(palette, definition);
        string maskPrefix = mask >= 0 ? mask.ToString("000") : "Default";
        string materialPath = $"{folder}/{SanitizeFileName(definition.Kind + "_" + maskPrefix + "_" + asset.Name)}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = FindAppearanceTextureShader();
            if (shader == null)
            {
                Debug.LogError("[MapPainter] No compatible texture shader found for Appearance Auto Tile material generation.");
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(materialPath));
        }

        material.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material));
        Rect? textureRect = asset.HasTextureRect ? asset.TextureRect : null;
        ConfigureTextureMaterial(material, texture, asset.Sprite, textureRect);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader FindAppearanceTextureShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Texture")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");
    }

    private static void ConfigureTextureMaterial(Material material, Texture2D texture, Sprite sprite, Rect? textureRect)
    {
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (textureRect.HasValue && texture.width > 0 && texture.height > 0)
        {
            Rect rect = textureRect.Value;
            scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
            offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
        }
        else if (sprite != null && texture.width > 0 && texture.height > 0)
        {
            Rect rect = sprite.textureRect;
            scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
            offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
        }

        material.mainTextureScale = scale;
        material.mainTextureOffset = offset;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
    }

    private static string EnsureGeneratedMaterialFolder(
        AppearanceAutoTilePalette palette,
        AppearanceAutoTileDefinition definition)
    {
        string palettePath = AssetDatabase.GetAssetPath(palette);
        string root = string.IsNullOrEmpty(palettePath)
            ? DefaultAppearancePaletteFolder
            : Path.GetDirectoryName(palettePath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(root))
            root = DefaultAppearancePaletteFolder;

        string generatedRoot = $"{root}/GeneratedMaterials";
        EnsureAssetFolder(generatedRoot);

        string paletteFolder = $"{generatedRoot}/{SanitizeFileName(palette.name)}";
        EnsureAssetFolder(paletteFolder);

        string kindFolder = $"{paletteFolder}/{SanitizeFileName(definition.Kind.ToString())}";
        EnsureAssetFolder(kindFolder);
        return kindFolder;
    }

    private static void EnsureAssetFolder(string path)
    {
        path = path.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureAssetFolder(parent);

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static int ResolveQuickMask(QuickAppearanceAsset asset, int fallbackIndex)
    {
        if (asset.HasExplicitMask && asset.ExplicitMask >= 0 && asset.ExplicitMask <= 255)
            return asset.ExplicitMask;

        if (TryParseTrailingMask(asset.Name, out int mask))
            return mask;

        return Mathf.Clamp(fallbackIndex, 0, 255);
    }

    private static bool TryParseTrailingMask(string name, out int mask)
    {
        mask = 0;
        if (string.IsNullOrEmpty(name))
            return false;

        int end = name.Length - 1;
        while (end >= 0 && !char.IsDigit(name[end]))
            end--;
        if (end < 0)
            return false;

        int start = end;
        while (start >= 0 && char.IsDigit(name[start]))
            start--;

        string number = name.Substring(start + 1, end - start);
        return int.TryParse(number, out mask) && mask >= 0 && mask <= 255;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "AppearanceTile";

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            for (int j = 0; j < invalidChars.Length; j++)
            {
                if (chars[i] == invalidChars[j])
                {
                    chars[i] = '_';
                    break;
                }
            }
        }

        return new string(chars).Replace(' ', '_');
    }

    private static int[] CreateBlob47Masks()
    {
        var masks = new List<int>();
        for (int mask = 0; mask <= 255; mask++)
        {
            if (IsValidBlobMask(mask))
                masks.Add(mask);
        }

        return masks.ToArray();
    }

    private static bool IsValidBlobMask(int mask)
    {
        bool north = (mask & MapAsset.AppearanceNorth) != 0;
        bool east = (mask & MapAsset.AppearanceEast) != 0;
        bool south = (mask & MapAsset.AppearanceSouth) != 0;
        bool west = (mask & MapAsset.AppearanceWest) != 0;

        if ((mask & MapAsset.AppearanceNorthEast) != 0 && (!north || !east))
            return false;
        if ((mask & MapAsset.AppearanceSouthEast) != 0 && (!south || !east))
            return false;
        if ((mask & MapAsset.AppearanceSouthWest) != 0 && (!south || !west))
            return false;
        if ((mask & MapAsset.AppearanceNorthWest) != 0 && (!north || !west))
            return false;

        return true;
    }

    private void DrawExportHint()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Next Step", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Export 시 로직(k/v)과 외관 Auto Tile(a/av)이 함께 JSON으로 저장됩니다.");
            EditorGUILayout.LabelField($"Server map id/file name: {_map.name}");

            if (GUILayout.Button("Export Current Map JSON", GUILayout.Height(24)))
                MapExportUtility.Export(_map);
        }
    }

    private void CreateNewMapAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create New Map Asset",
            "NewMap",
            "asset",
            "Save Map Asset",
            DefaultMapAssetFolder);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var asset = CreateInstance<MapAsset>();
        asset.EnsureSize();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _map = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private void CreateNewAppearancePalette()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Appearance Auto Tile Palette",
            "NewAppearanceAutoTilePalette",
            "asset",
            "Save Appearance Auto Tile Palette",
            DefaultAppearancePaletteFolder);
        if (string.IsNullOrEmpty(path))
            return;

        var palette = CreateInstance<AppearanceAutoTilePalette>();
        palette.Tiles = AppearanceAutoTilePalette.CreateDefaultDefinitions();
        AssetDatabase.CreateAsset(palette, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (_map != null)
        {
            UnityEditor.Undo.RecordObject(_map, "Assign Appearance Palette");
            _map.AppearancePalette = palette;
            EditorUtility.SetDirty(_map);
        }

        EditorGUIUtility.PingObject(palette);
    }
}
#endif

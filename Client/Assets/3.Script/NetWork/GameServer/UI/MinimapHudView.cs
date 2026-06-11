using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MinimapHudView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MinimapMapGraphic _mapGraphic;
    [SerializeField] private RectTransform _mapViewport;

    [Header("Layout")]
    [SerializeField] private Vector2 _compactSize = new Vector2(304f, 304f);
    [SerializeField] private Vector2 _compactAnchoredPosition = new Vector2(-56f, -52f);

    [Header("Grid Zoom")]
    [SerializeField, Min(0.25f)] private float _x1GridScale = 0.9f;
    [SerializeField, Min(0.25f)] private float _x2GridScale = 1.6f;
    [SerializeField, Min(0.25f)] private float _x3GridScale = 2.3f;
    [SerializeField, Range(0, 2)] private int _defaultZoomLevel;

    [Header("Style")]
    [SerializeField] private Color _panelColor = new Color(0.015f, 0.04f, 0.055f, 0.88f);
    [SerializeField] private Color _viewportColor = new Color(0.01f, 0.025f, 0.032f, 0.92f);
    [SerializeField] private Color _frameColor = new Color(0.44f, 0.58f, 0.62f, 0.86f);
    [SerializeField] private Color _accentColor = new Color(0.23f, 0.96f, 1f, 0.95f);

    private readonly List<MinimapMapGraphic.EntityMarker> _markerBuffer = new List<MinimapMapGraphic.EntityMarker>(32);
    private readonly Button[] _zoomButtons = new Button[ZoomLevelCount];
    private readonly Image[] _zoomButtonImages = new Image[ZoomLevelCount];
    private ClientGameState _boundState;
    private bool _zoomButtonsBound;
    private int _selectedZoomLevel = -1;
    private float _nextBindAttemptTime;
    private bool _entitiesDirty;
    private float _nextEntityRefreshTime;

    private const int ZoomLevelCount = 3;
    private const float EntityRefreshInterval = 0.05f;

    private void Awake()
    {
        EnsureRuntimeUi();
    }

    private void OnEnable()
    {
        EnsureRuntimeUi();
        BindZoomButtons();

        if (Application.isPlaying)
            TryBindState();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        TryBindState();
        RefreshFromState();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (_boundState == null)
        {
            if (Time.unscaledTime >= _nextBindAttemptTime)
            {
                _nextBindAttemptTime = Time.unscaledTime + 0.25f;
                TryBindState();
            }

            return;
        }

        if (_entitiesDirty && Time.unscaledTime >= _nextEntityRefreshTime)
        {
            _entitiesDirty = false;
            _nextEntityRefreshTime = Time.unscaledTime + EntityRefreshInterval;
            RefreshEntities();
        }
    }

    private void OnDisable()
    {
        UnbindState();
        UnbindZoomButtons();
    }

    public void EnsureRuntimeUi()
    {
        RectTransform root = RequireRect(gameObject);
        ConfigureRootRect(root);

        Image panel = GetOrAdd<Image>(gameObject);
        panel.color = _panelColor;
        panel.raycastTarget = false;

        EnsureFrame(root, "PanelFrame", 2f, _frameColor);
        EnsureTitle(root);
        EnsureHeaderLine(root);
        RemoveLegacyExpandButton(root);
        EnsureZoomButtons(root);
        EnsureViewport(root);

        ApplyZoomLevel();
    }

    private void TryBindState()
    {
        ClientGameState state = ClientGameState.Instance;
        if (state == null)
            return;

        if (_boundState == state)
            return;

        UnbindState();
        _boundState = state;
        _boundState.MapCreated += HandleMapCreated;
        _boundState.TileChanged += HandleTileChanged;
        _boundState.EntityChanged += HandleEntityChanged;
        _boundState.EntityRemoved += HandleEntityRemoved;
        _boundState.EntitiesCleared += HandleEntitiesCleared;
        _boundState.PartyStateChanged += HandlePartyStateChanged;
        RefreshFromState();
    }

    private void UnbindState()
    {
        if (_boundState == null)
            return;

        _boundState.MapCreated -= HandleMapCreated;
        _boundState.TileChanged -= HandleTileChanged;
        _boundState.EntityChanged -= HandleEntityChanged;
        _boundState.EntityRemoved -= HandleEntityRemoved;
        _boundState.EntitiesCleared -= HandleEntitiesCleared;
        _boundState.PartyStateChanged -= HandlePartyStateChanged;
        _boundState = null;
        _entitiesDirty = false;
    }

    private void HandleMapCreated(int width, int height)
    {
        if (_mapGraphic == null)
            return;

        _mapGraphic.SetMapSize(width, height);
        RefreshEntities();
    }

    private void HandleTileChanged(int x, int y, int tileKind)
    {
        _mapGraphic?.SetTile(x, y, tileKind);
    }

    private void HandleEntityChanged(ClientEntityInfo info)
    {
        MarkEntitiesDirty();
    }

    private void HandleEntityRemoved(int entityId)
    {
        MarkEntitiesDirty();
    }

    private void HandleEntitiesCleared()
    {
        _mapGraphic?.SetFocus(0, 0, false);
        _mapGraphic?.ClearMarkers();
    }

    private void HandlePartyStateChanged()
    {
        MarkEntitiesDirty();
    }

    private void MarkEntitiesDirty()
    {
        if (!Application.isPlaying)
        {
            RefreshEntities();
            return;
        }

        _entitiesDirty = true;
    }

    private void RefreshFromState()
    {
        if (_boundState == null || _mapGraphic == null)
            return;

        int width = _boundState.MapWidth;
        int height = _boundState.MapHeight;
        if (width <= 0 || height <= 0)
        {
            _mapGraphic.SetMapSize(0, 0);
            _mapGraphic.SetFocus(0, 0, false);
            _mapGraphic.ClearMarkers();
            return;
        }

        _mapGraphic.SetMapSize(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                _mapGraphic.SetTile(x, y, _boundState.GetTileKind(x, y));
        }

        RefreshEntities();
    }

    private void RefreshEntities()
    {
        if (_boundState == null || _mapGraphic == null)
            return;

        _markerBuffer.Clear();
        foreach (ClientEntityInfo info in _boundState.EnumerateEntities())
        {
            if (!ShouldShowEntity(info))
                continue;

            _markerBuffer.Add(new MinimapMapGraphic.EntityMarker
            {
                EntityId = info.EntityId,
                Kind = ResolveMarkerKind(info),
                X = info.X,
                Y = info.Y,
                SizeX = Mathf.Max(1, info.SizeX),
                SizeY = Mathf.Max(1, info.SizeY),
                Rotation = info.Rotation
            });
        }

        UpdateMapFocus();
        _mapGraphic.SetMarkers(_markerBuffer);
    }

    private void UpdateMapFocus()
    {
        if (_boundState == null || _mapGraphic == null)
            return;

        if (_boundState.TryGetMyEntity(out ClientEntityInfo info) && ShouldShowEntity(info))
        {
            _mapGraphic.SetFocus(info.X, info.Y, true);
            return;
        }

        _mapGraphic.SetFocus(0, 0, false);
    }

    private bool ShouldShowEntity(ClientEntityInfo info)
    {
        if (_boundState == null)
            return false;

        if (info.X < 0 || info.X >= _boundState.MapWidth || info.Y < 0 || info.Y >= _boundState.MapHeight)
            return false;

        if ((TileKind)_boundState.GetTileKind(info.X, info.Y) == TileKind.Wall)
            return false;

        if (info.EntityType != (int)EntityType.Object && info.Hp <= 0)
            return false;

        return info.EntityType == (int)EntityType.Player
            || info.EntityType == (int)EntityType.Monster
            || info.EntityType == (int)EntityType.Object;
    }

    private MinimapMapGraphic.MarkerKind ResolveMarkerKind(ClientEntityInfo info)
    {
        if (info.EntityType == (int)EntityType.Player)
            return info.EntityId == _boundState.MyActorId
                ? MinimapMapGraphic.MarkerKind.LocalPlayer
                : MinimapMapGraphic.MarkerKind.Ally;

        if (info.EntityType == (int)EntityType.Monster)
            return MinimapMapGraphic.MarkerKind.Enemy;

        return MinimapMapGraphic.MarkerKind.Object;
    }

    private void ConfigureRootRect(RectTransform root)
    {
        if (root.parent == null)
            return;

        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);

        if (root.anchoredPosition == Vector2.zero)
            root.anchoredPosition = _compactAnchoredPosition;

        root.sizeDelta = _compactSize;
    }

    private void EnsureTitle(RectTransform root)
    {
        TextMeshProUGUI title = GetOrAdd<TextMeshProUGUI>(FindOrCreateRect("Title", root).gameObject);
        title.text = "MINIMAP";
        title.fontSize = 18f;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 6f;
        title.alignment = TextAlignmentOptions.Left;
        title.enableWordWrapping = false;
        title.raycastTarget = false;
        title.color = _accentColor;
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(116f, 26f), new Vector2(0f, 1f));
    }

    private void EnsureHeaderLine(RectTransform root)
    {
        RectTransform line = FindOrCreateImage("HeaderLine", root, new Color(_frameColor.r, _frameColor.g, _frameColor.b, 0.5f));
        StretchHorizontalAtTop(line, 14f, 14f, 44f, 1.5f);
    }

    private void RemoveLegacyExpandButton(RectTransform root)
    {
        Transform legacy = root.Find("ExpandButton");
        if (legacy == null)
            return;

        if (Application.isPlaying)
            Destroy(legacy.gameObject);
        else
            DestroyImmediate(legacy.gameObject);
    }

    private void EnsureZoomButtons(RectTransform root)
    {
        for (int i = 0; i < ZoomLevelCount; i++)
        {
            RectTransform buttonRect = FindOrCreateImage($"ZoomButton_X{i + 1}", root, GetZoomButtonNormalColor(i));
            Anchor(buttonRect, new Vector2(1f, 1f), new Vector2(-14f - 46f * i, -12f), new Vector2(42f, 28f), new Vector2(1f, 1f));

            Image image = buttonRect.GetComponent<Image>();
            image.raycastTarget = true;
            _zoomButtonImages[i] = image;

            Button button = GetOrAdd<Button>(buttonRect.gameObject);
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            _zoomButtons[i] = button;

            RectTransform labelRect = FindOrCreateRect("Label", buttonRect);
            Stretch(labelRect, 0f);
            GetOrAdd<CanvasRenderer>(labelRect.gameObject);
            TextMeshProUGUI label = GetOrAdd<TextMeshProUGUI>(labelRect.gameObject);
            label.text = $"X{i + 1}";
            label.fontSize = 13f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.color = _accentColor;
        }

        UpdateZoomButtonStates();
        BindZoomButtons();
    }

    private void EnsureViewport(RectTransform root)
    {
        _mapViewport = FindOrCreateImage("MapViewport", root, _viewportColor);
        Stretch(_mapViewport, 16f, 50f, 16f, 16f);
        GetOrAdd<RectMask2D>(_mapViewport.gameObject);
        EnsureFrame(_mapViewport, "MapFrame", 1.5f, new Color(_frameColor.r, _frameColor.g, _frameColor.b, 0.7f));

        RectTransform graphicRect = FindOrCreateRect("MapGraphic", _mapViewport);
        Stretch(graphicRect, 8f);
        GetOrAdd<CanvasRenderer>(graphicRect.gameObject);
        _mapGraphic = GetOrAdd<MinimapMapGraphic>(graphicRect.gameObject);
        _mapGraphic.raycastTarget = false;
    }

    private void SetZoomLevel1()
    {
        SetZoomLevel(0);
    }

    private void SetZoomLevel2()
    {
        SetZoomLevel(1);
    }

    private void SetZoomLevel3()
    {
        SetZoomLevel(2);
    }

    private void SetZoomLevel(int level)
    {
        int clampedLevel = Mathf.Clamp(level, 0, ZoomLevelCount - 1);
        if (_selectedZoomLevel == clampedLevel)
        {
            UpdateZoomButtonStates();
            return;
        }

        _selectedZoomLevel = clampedLevel;
        ApplyZoomLevel();
    }

    private void ApplyZoomLevel()
    {
        if (_selectedZoomLevel < 0)
            _selectedZoomLevel = Mathf.Clamp(_defaultZoomLevel, 0, ZoomLevelCount - 1);

        _mapGraphic?.SetZoomScale(GetZoomScale(_selectedZoomLevel));
        UpdateZoomButtonStates();
    }

    private float GetZoomScale(int level)
    {
        switch (level)
        {
            case 1: return Mathf.Max(0.25f, _x2GridScale);
            case 2: return Mathf.Max(0.25f, _x3GridScale);
            default: return Mathf.Max(0.25f, _x1GridScale);
        }
    }

    private void BindZoomButtons()
    {
        if (_zoomButtonsBound)
            return;

        if (_zoomButtons[0] != null)
            _zoomButtons[0].onClick.AddListener(SetZoomLevel1);
        if (_zoomButtons[1] != null)
            _zoomButtons[1].onClick.AddListener(SetZoomLevel2);
        if (_zoomButtons[2] != null)
            _zoomButtons[2].onClick.AddListener(SetZoomLevel3);

        _zoomButtonsBound = true;
    }

    private void UnbindZoomButtons()
    {
        if (!_zoomButtonsBound)
            return;

        if (_zoomButtons[0] != null)
            _zoomButtons[0].onClick.RemoveListener(SetZoomLevel1);
        if (_zoomButtons[1] != null)
            _zoomButtons[1].onClick.RemoveListener(SetZoomLevel2);
        if (_zoomButtons[2] != null)
            _zoomButtons[2].onClick.RemoveListener(SetZoomLevel3);

        _zoomButtonsBound = false;
    }

    private void UpdateZoomButtonStates()
    {
        for (int i = 0; i < ZoomLevelCount; i++)
        {
            bool selected = i == Mathf.Max(0, _selectedZoomLevel);
            Color normalColor = selected ? GetZoomButtonSelectedColor() : GetZoomButtonNormalColor(i);
            Color highlightedColor = selected
                ? new Color(0.13f, 0.42f, 0.46f, 1f)
                : new Color(0.09f, 0.17f, 0.19f, 0.96f);

            if (_zoomButtonImages[i] != null)
                _zoomButtonImages[i].color = normalColor;

            if (_zoomButtons[i] == null)
                continue;

            ColorBlock colors = _zoomButtons[i].colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = selected
                ? new Color(0.08f, 0.32f, 0.36f, 1f)
                : new Color(0.04f, 0.13f, 0.16f, 1f);
            colors.selectedColor = highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            _zoomButtons[i].colors = colors;
        }
    }

    private Color GetZoomButtonNormalColor(int index)
    {
        float step = Mathf.Clamp01(index * 0.08f);
        return new Color(0.045f + step, 0.085f + step, 0.1f + step, 0.9f);
    }

    private Color GetZoomButtonSelectedColor()
    {
        return new Color(0.06f, 0.28f, 0.32f, 1f);
    }

    private static void EnsureFrame(RectTransform parent, string prefix, float thickness, Color color)
    {
        RectTransform top = FindOrCreateImage(prefix + "_Top", parent, color);
        RectTransform bottom = FindOrCreateImage(prefix + "_Bottom", parent, color);
        RectTransform left = FindOrCreateImage(prefix + "_Left", parent, color);
        RectTransform right = FindOrCreateImage(prefix + "_Right", parent, color);

        StretchHorizontalAtTop(top, 0f, 0f, 0f, thickness);
        StretchHorizontalAtBottom(bottom, 0f, 0f, 0f, thickness);
        StretchVerticalAtLeft(left, 0f, 0f, 0f, thickness);
        StretchVerticalAtRight(right, 0f, 0f, 0f, thickness);
    }

    private static RectTransform FindOrCreateRect(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return RequireRect(existing.gameObject);

        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static RectTransform FindOrCreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = FindOrCreateRect(name, parent);
        Image image = GetOrAdd<Image>(rect.gameObject);
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform RequireRect(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        return rect != null ? rect : target.AddComponent<RectTransform>();
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        Stretch(rect, inset, inset, inset, inset);
    }

    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void StretchHorizontalAtTop(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void StretchHorizontalAtBottom(RectTransform rect, float left, float right, float bottom, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
    }

    private static void StretchVerticalAtLeft(RectTransform rect, float top, float bottom, float left, float width)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(left + width, -top);
    }

    private static void StretchVerticalAtRight(RectTransform rect, float top, float bottom, float right, float width)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(-right - width, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}

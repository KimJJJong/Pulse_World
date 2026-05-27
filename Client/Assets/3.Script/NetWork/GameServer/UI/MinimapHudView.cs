using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MinimapHudView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MinimapMapGraphic _mapGraphic;
    [SerializeField] private RectTransform _mapViewport;
    [SerializeField] private Button _expandButton;
    [SerializeField] private MinimapExpandIconGraphic _expandIcon;

    [Header("Layout")]
    [SerializeField] private Vector2 _compactSize = new Vector2(304f, 304f);
    [SerializeField] private Vector2 _expandedSize = new Vector2(472f, 472f);
    [SerializeField] private Vector2 _compactAnchoredPosition = new Vector2(-56f, -52f);

    [Header("Style")]
    [SerializeField] private Color _panelColor = new Color(0.015f, 0.04f, 0.055f, 0.88f);
    [SerializeField] private Color _viewportColor = new Color(0.01f, 0.025f, 0.032f, 0.92f);
    [SerializeField] private Color _frameColor = new Color(0.44f, 0.58f, 0.62f, 0.86f);
    [SerializeField] private Color _accentColor = new Color(0.23f, 0.96f, 1f, 0.95f);

    private readonly List<MinimapMapGraphic.EntityMarker> _markerBuffer = new List<MinimapMapGraphic.EntityMarker>(32);
    private ClientGameState _boundState;
    private bool _buttonBound;
    private bool _expanded;
    private float _nextBindAttemptTime;

    private void Awake()
    {
        EnsureRuntimeUi();
    }

    private void OnEnable()
    {
        EnsureRuntimeUi();
        BindExpandButton();

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
        if (!Application.isPlaying || _boundState != null)
            return;

        if (Time.unscaledTime < _nextBindAttemptTime)
            return;

        _nextBindAttemptTime = Time.unscaledTime + 0.25f;
        TryBindState();
    }

    private void OnDisable()
    {
        UnbindState();
        UnbindExpandButton();
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
        EnsureExpandButton(root);
        EnsureViewport(root);

        ApplyExpandedState();
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
        RefreshEntities();
    }

    private void HandleEntityRemoved(int entityId)
    {
        RefreshEntities();
    }

    private void HandleEntitiesCleared()
    {
        _mapGraphic?.SetFocus(0, 0, false);
        _mapGraphic?.ClearMarkers();
    }

    private void HandlePartyStateChanged()
    {
        RefreshEntities();
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

    private void ToggleExpanded()
    {
        _expanded = !_expanded;
        ApplyExpandedState();
    }

    private void ApplyExpandedState()
    {
        RectTransform root = transform as RectTransform;
        if (root != null)
            root.sizeDelta = _expanded ? _expandedSize : _compactSize;

        if (_expandIcon != null)
            _expandIcon.SetExpanded(_expanded);

        _mapGraphic?.SetVerticesDirty();
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

        if (root.sizeDelta.sqrMagnitude <= 1f)
            root.sizeDelta = _compactSize;
    }

    private void EnsureTitle(RectTransform root)
    {
        TextMeshProUGUI title = GetOrAdd<TextMeshProUGUI>(FindOrCreateRect("Title", root).gameObject);
        title.text = "MINIMAP";
        title.fontSize = 18f;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 9f;
        title.alignment = TextAlignmentOptions.Left;
        title.enableWordWrapping = false;
        title.raycastTarget = false;
        title.color = _accentColor;
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(180f, 26f), new Vector2(0f, 1f));
    }

    private void EnsureHeaderLine(RectTransform root)
    {
        RectTransform line = FindOrCreateImage("HeaderLine", root, new Color(_frameColor.r, _frameColor.g, _frameColor.b, 0.5f));
        StretchHorizontalAtTop(line, 14f, 14f, 44f, 1.5f);
    }

    private void EnsureExpandButton(RectTransform root)
    {
        Color normalButtonColor = new Color(0.05f, 0.09f, 0.105f, 0.9f);
        RectTransform buttonRect = FindOrCreateImage("ExpandButton", root, normalButtonColor);
        Anchor(buttonRect, new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(36f, 36f), new Vector2(1f, 1f));

        _expandButton = GetOrAdd<Button>(buttonRect.gameObject);
        _expandButton.transition = Selectable.Transition.ColorTint;
        _expandButton.targetGraphic = buttonRect.GetComponent<Image>();
        ColorBlock colors = _expandButton.colors;
        colors.normalColor = normalButtonColor;
        colors.highlightedColor = new Color(0.09f, 0.17f, 0.19f, 0.96f);
        colors.pressedColor = new Color(0.04f, 0.13f, 0.16f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        _expandButton.colors = colors;

        RectTransform iconRect = FindOrCreateRect("Icon", buttonRect);
        Stretch(iconRect, 8f);
        GetOrAdd<CanvasRenderer>(iconRect.gameObject);
        _expandIcon = GetOrAdd<MinimapExpandIconGraphic>(iconRect.gameObject);
        _expandIcon.color = _accentColor;
        _expandIcon.raycastTarget = false;

        BindExpandButton();
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

    private void BindExpandButton()
    {
        if (_expandButton == null || _buttonBound)
            return;

        _expandButton.onClick.AddListener(ToggleExpanded);
        _buttonBound = true;
    }

    private void UnbindExpandButton()
    {
        if (_expandButton == null || !_buttonBound)
            return;

        _expandButton.onClick.RemoveListener(ToggleExpanded);
        _buttonBound = false;
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

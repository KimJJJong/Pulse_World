using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MonsterHealthBarView : MonoBehaviour
{
    public const string ChildName = "__MonsterHealthBar";

    private const float DefaultSecondsPerBeat = 0.5f;

    [SerializeField] private Vector2 _normalSize = new Vector2(118f, 18f);
    [SerializeField] private Vector2 _eliteSize = new Vector2(182f, 36f);
    [SerializeField] private float _worldScale = 0.01f;
    [SerializeField] private float _normalHeightPadding = 0.24f;
    [SerializeField] private float _eliteHeightPadding = 0.34f;
    [SerializeField] private Color _panelColor = new Color(0.015f, 0.018f, 0.022f, 0.86f);
    [SerializeField] private Color _trackColor = new Color(0.03f, 0.035f, 0.04f, 0.95f);
    [SerializeField] private Color _frameColor = new Color(0.68f, 0.72f, 0.72f, 0.9f);
    [SerializeField] private Color _eliteAccentColor = new Color(0.38f, 1f, 0.98f, 0.95f);
    [SerializeField] private Color _fillColor = new Color(0.88f, 0.08f, 0.07f, 0.98f);
    [SerializeField] private Color _lowFillColor = new Color(1f, 0.55f, 0.12f, 1f);
    [SerializeField] private Color _damageTrailColor = new Color(1f, 0.72f, 0.22f, 0.5f);

    private Transform _followTarget;
    private RectTransform _rect;
    private RectTransform _trackRect;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _fillRect;
    private RectTransform _damageTrailRect;
    private Image _fillImage;
    private Image _damageTrailImage;
    private TextMeshProUGUI _nameLabel;
    private bool _isElite;
    private bool _initialized;
    private bool _visible;
    private long _visibleUntilBeat = long.MinValue;
    private float _visibleUntilTime = -1f;
    private int _normalVisibleBeats = 8;
    private float _targetFill = 1f;
    private float _trailFill = 1f;
    private Camera _cachedCamera;
    private float _nextCameraLookupTime;

    public static MonsterHealthBarView GetOrCreate(Transform owner)
    {
        if (owner == null)
            return null;

        Transform existing = owner.Find(ChildName);
        if (existing != null)
        {
            MonsterHealthBarView existingView = existing.GetComponent<MonsterHealthBarView>();
            if (existingView != null)
                return existingView;
        }

        GameObject go = new GameObject(ChildName, typeof(RectTransform));
        go.transform.SetParent(owner, false);
        return go.AddComponent<MonsterHealthBarView>();
    }

    public void Bind(Transform followTarget, string displayName, bool isElite, int hp, int maxHp, int normalVisibleBeats)
    {
        _followTarget = followTarget != null ? followTarget : transform.parent;
        _isElite = isElite;
        _normalVisibleBeats = Mathf.Max(1, normalVisibleBeats);

        EnsureRuntimeUi();
        ApplyLayout(displayName);
        PositionAboveTarget();
        SetHealth(hp, maxHp);

        bool keepNormalVisible = !_isElite && _visible && !HasExpired();
        SetVisible(hp > 0 && (_isElite || keepNormalVisible), immediate: true);
        if (_isElite)
        {
            _visibleUntilBeat = long.MaxValue;
            _visibleUntilTime = float.PositiveInfinity;
        }
    }

    public void ShowForHit(int hp, int maxHp)
    {
        EnsureRuntimeUi();
        SetHealth(hp, maxHp);

        if (_isElite)
        {
            _visibleUntilBeat = long.MaxValue;
            _visibleUntilTime = float.PositiveInfinity;
        }
        else
        {
            long currentBeat = GetCurrentBeatIndex();
            _visibleUntilBeat = currentBeat >= 0
                ? currentBeat + _normalVisibleBeats
                : long.MinValue;
            _visibleUntilTime = Time.unscaledTime + GetSecondsPerBeat() * _normalVisibleBeats;
        }

        SetVisible(true, immediate: false);
    }

    public void HideImmediate()
    {
        SetVisible(false, immediate: true);
    }

    private void Awake()
    {
        EnsureRuntimeUi();
    }

    private void LateUpdate()
    {
        if (!_initialized)
            return;

        if (_followTarget == null && transform.parent != null)
            _followTarget = transform.parent;

        FaceCamera();

        if (!_isElite && _visible && HasExpired())
            SetVisible(false, immediate: false);

        if (_damageTrailImage != null && _trailFill > _targetFill)
        {
            _trailFill = Mathf.MoveTowards(_trailFill, _targetFill, Time.unscaledDeltaTime * 0.55f);
            SetBarFill(_damageTrailRect, _trailFill);
        }
    }

    private void EnsureRuntimeUi()
    {
        if (_initialized)
            return;

        _rect = GetOrAdd<RectTransform>(gameObject);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.localScale = Vector3.one * _worldScale;

        _canvas = GetOrAdd<Canvas>(gameObject);
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 45;
        _canvas.worldCamera = ResolveCamera();

        _canvasGroup = GetOrAdd<CanvasGroup>(gameObject);
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        RectTransform panel = FindOrCreateImage("Panel", _rect, _panelColor);
        Stretch(panel, 0f);

        _trackRect = FindOrCreateImage("Track", _rect, _trackColor);

        _damageTrailRect = FindOrCreateImage("DamageTrail", _trackRect, _damageTrailColor);
        _damageTrailImage = GetOrAdd<Image>(_damageTrailRect.gameObject);
        ConfigureBarImage(_damageTrailImage);
        SetBarFill(_damageTrailRect, 1f);

        _fillRect = FindOrCreateImage("Fill", _trackRect, _fillColor);
        _fillImage = GetOrAdd<Image>(_fillRect.gameObject);
        ConfigureBarImage(_fillImage);
        SetBarFill(_fillRect, 1f);

        EnsureFrame(_trackRect, "TrackFrame", 1.5f, _frameColor);

        _nameLabel = GetOrAdd<TextMeshProUGUI>(FindOrCreateRect("Name", _rect).gameObject);
        _nameLabel.alignment = TextAlignmentOptions.Center;
        _nameLabel.enableWordWrapping = false;
        _nameLabel.fontSize = 13f;
        _nameLabel.fontStyle = FontStyles.Bold;
        _nameLabel.color = new Color(0.92f, 1f, 0.98f, 1f);
        _nameLabel.raycastTarget = false;

        CreateEliteBrackets();

        _initialized = true;
        SetVisible(false, immediate: true);
    }

    private void ApplyLayout(string displayName)
    {
        Vector2 size = _isElite ? _eliteSize : _normalSize;
        _rect.sizeDelta = size;
        _rect.localScale = Vector3.one * _worldScale;

        if (_isElite)
        {
            Anchor(_trackRect, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(size.x - 24f, 11f), new Vector2(0.5f, 0f));
            Anchor(_nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(size.x - 20f, 16f), new Vector2(0.5f, 1f));
            _nameLabel.text = string.IsNullOrWhiteSpace(displayName) ? "Elite Monster" : displayName;
            _nameLabel.gameObject.SetActive(true);
        }
        else
        {
            Anchor(_trackRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x - 16f, 8f), new Vector2(0.5f, 0.5f));
            _nameLabel.text = string.Empty;
            _nameLabel.gameObject.SetActive(false);
        }

        SetEliteBracketsActive(_isElite);
    }

    private void SetHealth(int hp, int maxHp)
    {
        int safeMaxHp = Mathf.Max(1, maxHp);
        int safeHp = Mathf.Clamp(hp, 0, safeMaxHp);
        float fill = Mathf.Clamp01((float)safeHp / safeMaxHp);

        if (_fillImage != null)
        {
            _fillImage.color = fill <= 0.3f ? _lowFillColor : _fillColor;
        }
        SetBarFill(_fillRect, fill);

        if (_damageTrailImage != null)
        {
            if (fill > _trailFill)
                _trailFill = fill;
        }
        SetBarFill(_damageTrailRect, _trailFill);

        _targetFill = fill;
    }

    private void SetVisible(bool visible, bool immediate)
    {
        _visible = visible;
        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = visible ? 1f : 0f;

        if (immediate || visible)
            return;

        _visibleUntilBeat = long.MinValue;
        _visibleUntilTime = -1f;
    }

    private bool HasExpired()
    {
        if (_isElite)
            return false;

        bool beatExpired = false;
        if (_visibleUntilBeat != long.MinValue)
        {
            long currentBeat = GetCurrentBeatIndex();
            beatExpired = currentBeat >= 0 && currentBeat >= _visibleUntilBeat;
        }

        bool timeExpired = _visibleUntilTime >= 0f && Time.unscaledTime >= _visibleUntilTime;
        return beatExpired || timeExpired;
    }

    private void PositionAboveTarget()
    {
        if (_followTarget == null)
            return;

        if (TryGetRendererBounds(_followTarget, out Bounds bounds))
        {
            Vector3 worldPos = new Vector3(
                bounds.center.x,
                bounds.max.y + (_isElite ? _eliteHeightPadding : _normalHeightPadding),
                bounds.center.z);
            transform.localPosition = _followTarget.InverseTransformPoint(worldPos);
            return;
        }

        transform.localPosition = new Vector3(0f, _isElite ? 2.5f : 2.1f, 0f);
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null
                || renderer is ParticleSystemRenderer
                || renderer is TrailRenderer
                || renderer is LineRenderer)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private void FaceCamera()
    {
        Camera camera = ResolveCamera();
        if (camera == null)
            return;

        transform.rotation = camera.transform.rotation;
        if (_canvas != null && _canvas.worldCamera == null)
            _canvas.worldCamera = camera;
    }

    private Camera ResolveCamera()
    {
        if (_cachedCamera != null)
            return _cachedCamera;

        if (Time.unscaledTime < _nextCameraLookupTime)
            return null;

        _nextCameraLookupTime = Time.unscaledTime + 0.5f;
        _cachedCamera = Camera.main;
        return _cachedCamera;
    }

    private static long GetCurrentBeatIndex()
    {
        RhythmClient rhythm = RhythmClient.Instance;
        if (rhythm == null || rhythm.ServerSongStartMs <= 0)
            return -1;

        return rhythm.GetCurrentBeatIndex();
    }

    private static float GetSecondsPerBeat()
    {
        RhythmClient rhythm = RhythmClient.Instance;
        if (rhythm == null)
            return DefaultSecondsPerBeat;

        double beatMs = rhythm.GetBeatDurationMs();
        if (beatMs <= 0)
            return DefaultSecondsPerBeat;

        return Mathf.Max(0.05f, (float)(beatMs / 1000.0));
    }

    private void CreateEliteBrackets()
    {
        CreateBracket("EliteBracket_TopLeft_H");
        CreateBracket("EliteBracket_TopLeft_V");
        CreateBracket("EliteBracket_TopRight_H");
        CreateBracket("EliteBracket_TopRight_V");
        CreateBracket("EliteBracket_BottomLeft_H");
        CreateBracket("EliteBracket_BottomLeft_V");
        CreateBracket("EliteBracket_BottomRight_H");
        CreateBracket("EliteBracket_BottomRight_V");
    }

    private void CreateBracket(string name)
    {
        RectTransform rect = FindOrCreateImage(name, _rect, _eliteAccentColor);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = false;
    }

    private void SetEliteBracketsActive(bool active)
    {
        ConfigureBracket("EliteBracket_TopLeft_H", active, new Vector2(0f, 1f), new Vector2(8f, -3f), new Vector2(18f, 2f), new Vector2(0f, 1f));
        ConfigureBracket("EliteBracket_TopLeft_V", active, new Vector2(0f, 1f), new Vector2(3f, -8f), new Vector2(2f, 18f), new Vector2(0f, 1f));
        ConfigureBracket("EliteBracket_TopRight_H", active, new Vector2(1f, 1f), new Vector2(-8f, -3f), new Vector2(18f, 2f), new Vector2(1f, 1f));
        ConfigureBracket("EliteBracket_TopRight_V", active, new Vector2(1f, 1f), new Vector2(-3f, -8f), new Vector2(2f, 18f), new Vector2(1f, 1f));
        ConfigureBracket("EliteBracket_BottomLeft_H", active, new Vector2(0f, 0f), new Vector2(8f, 3f), new Vector2(18f, 2f), new Vector2(0f, 0f));
        ConfigureBracket("EliteBracket_BottomLeft_V", active, new Vector2(0f, 0f), new Vector2(3f, 8f), new Vector2(2f, 18f), new Vector2(0f, 0f));
        ConfigureBracket("EliteBracket_BottomRight_H", active, new Vector2(1f, 0f), new Vector2(-8f, 3f), new Vector2(18f, 2f), new Vector2(1f, 0f));
        ConfigureBracket("EliteBracket_BottomRight_V", active, new Vector2(1f, 0f), new Vector2(-3f, 8f), new Vector2(2f, 18f), new Vector2(1f, 0f));
    }

    private void ConfigureBracket(string name, bool active, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        Transform child = _rect.Find(name);
        if (child == null)
            return;

        child.gameObject.SetActive(active);
        Anchor((RectTransform)child, anchor, position, size, pivot);
    }

    private static void ConfigureBarImage(Image image)
    {
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
    }

    private static void SetBarFill(RectTransform rect, float fill)
    {
        if (rect == null)
            return;

        fill = Mathf.Clamp01(fill);
        rect.gameObject.SetActive(fill > 0.001f);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(fill, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
            return GetOrAdd<RectTransform>(existing.gameObject);

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
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
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

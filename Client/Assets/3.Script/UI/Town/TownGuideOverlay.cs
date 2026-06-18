using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class TownGuideOverlay : MonoBehaviour
{
    private const string DefaultGuideResourcePath = "UI/TownGuide/TownControlsGuide";
    private const int CanvasSortingOrder = 29000;

    [SerializeField] private Sprite _guideSprite;
    [SerializeField] private string _guideResourcePath = DefaultGuideResourcePath;
    [SerializeField] private bool _showOnStart = true;

    private static TMP_FontAsset _preferredFont;

    private GameObject _windowRoot;
    private GameObject _guideButtonRoot;
    private bool _built;
    private bool _started;

    private void Awake()
    {
        BuildIfNeeded();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        BuildIfNeeded();
        SetGuideVisible(_showOnStart);
    }

    private void Start()
    {
        if (_started)
            return;

        _started = true;
        SetGuideVisible(_showOnStart);
    }

    public void ShowGuide()
    {
        BuildIfNeeded();
        SetGuideVisible(true);
    }

    public void HideGuide()
    {
        BuildIfNeeded();
        SetGuideVisible(false);
    }

    public void Configure(Sprite guideSprite, string guideResourcePath, bool showOnStart)
    {
        _guideSprite = guideSprite;
        _guideResourcePath = string.IsNullOrWhiteSpace(guideResourcePath)
            ? DefaultGuideResourcePath
            : guideResourcePath;
        _showOnStart = showOnStart;
        _built = false;
        BuildIfNeeded();
        SetGuideVisible(_showOnStart);
    }

    private void BuildIfNeeded()
    {
        if (_built)
            return;

        ApplyCanvasSettings();
        if (Application.isPlaying)
            EnsureEventSystem();
        ResolveGuideSprite();
        CreateGuideButton();
        CreateGuideWindow();
        _built = true;
    }

    private void ApplyCanvasSettings()
    {
        if (transform is RectTransform rootRect)
            Stretch(rootRect);

        var canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = CanvasSortingOrder;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private void CreateGuideButton()
    {
        _guideButtonRoot = CreateRect(gameObject.transform, "Button_Guide", out bool createdButton).gameObject;
        var rect = (RectTransform)_guideButtonRoot.transform;
        if (createdButton)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(22f, 0f);
            rect.sizeDelta = new Vector2(96f, 76f);
        }

        var image = Require<Image>(_guideButtonRoot);
        image.color = new Color(0.035f, 0.105f, 0.14f, 0.92f);
        image.raycastTarget = true;

        var outline = Require<Outline>(_guideButtonRoot);
        outline.effectColor = new Color(0.15f, 0.82f, 1f, 0.82f);
        outline.effectDistance = new Vector2(2f, -2f);

        var button = Require<Button>(_guideButtonRoot);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.75f, 0.96f, 1f, 1f);
        colors.pressedColor = new Color(0.42f, 0.78f, 0.95f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.RemoveListener(ShowGuide);
        button.onClick.AddListener(ShowGuide);

        var feedback = _guideButtonRoot.GetComponent<HomeUIButtonFeedback>();
        if (feedback == null)
            feedback = _guideButtonRoot.AddComponent<HomeUIButtonFeedback>();
        feedback.Configure(rect, image);

        var icon = CreateText(rect, "Icon", "?", 28f, TextAlignmentOptions.Center, new Color(0.52f, 0.93f, 1f, 1f), out bool createdIcon);
        if (createdIcon)
            SetAnchored(icon.rectTransform, new Vector2(0.5f, 0.66f), new Vector2(42f, 34f), Vector2.zero);
        icon.fontStyle = FontStyles.Bold;

        var label = CreateText(rect, "Label", "Guide", 16f, TextAlignmentOptions.Center, new Color(0.92f, 0.99f, 1f, 1f), out bool createdLabel);
        if (createdLabel)
            SetAnchored(label.rectTransform, new Vector2(0.5f, 0.28f), new Vector2(80f, 28f), Vector2.zero);
        label.fontStyle = FontStyles.Bold;
    }

    private void CreateGuideWindow()
    {
        _windowRoot = CreateRect(gameObject.transform, "TownGuideWindow", out bool createdWindow).gameObject;
        var windowRect = (RectTransform)_windowRoot.transform;
        if (createdWindow)
            Stretch(windowRect);

        var dim = Require<Image>(_windowRoot);
        dim.color = new Color(0f, 0f, 0f, 0.68f);
        dim.raycastTarget = true;

        var imageRect = CreateRect(windowRect, "GuideImage", out bool createdImage);
        if (createdImage)
            SetAnchored(imageRect, new Vector2(0.5f, 0.56f), new Vector2(860f, 484f), Vector2.zero);
        var image = Require<Image>(imageRect.gameObject);
        image.sprite = _guideSprite;
        image.color = _guideSprite != null ? Color.white : new Color(0.04f, 0.09f, 0.12f, 1f);
        image.preserveAspect = true;
        image.raycastTarget = false;

        var imageOutline = Require<Outline>(imageRect.gameObject);
        imageOutline.effectColor = new Color(0.12f, 0.82f, 1f, 0.6f);
        imageOutline.effectDistance = new Vector2(2f, -2f);

        if (_guideSprite == null)
        {
            var fallback = CreateText(imageRect, "MissingImageText", "Guide image missing", 24f, TextAlignmentOptions.Center, Color.white, out bool createdFallback);
            if (createdFallback)
                Stretch(fallback.rectTransform);
        }
        else
        {
            DestroyChildIfExists(imageRect, "MissingImageText");
        }

        var backButtonRect = CreateRect(windowRect, "Button_BackGuide", out bool createdBackButton);
        if (createdBackButton)
            SetAnchored(backButtonRect, new Vector2(0.5f, 0.11f), new Vector2(190f, 52f), Vector2.zero);
        var backImage = Require<Image>(backButtonRect.gameObject);
        backImage.color = new Color(0.035f, 0.11f, 0.15f, 0.96f);
        backImage.raycastTarget = true;

        var backOutline = Require<Outline>(backButtonRect.gameObject);
        backOutline.effectColor = new Color(0.18f, 0.85f, 1f, 0.72f);
        backOutline.effectDistance = new Vector2(2f, -2f);

        var backButton = Require<Button>(backButtonRect.gameObject);
        backButton.targetGraphic = backImage;
        backButton.transition = Selectable.Transition.ColorTint;
        var colors = backButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 0.96f, 1f, 1f);
        colors.pressedColor = new Color(0.42f, 0.78f, 0.95f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        backButton.colors = colors;
        backButton.onClick.RemoveListener(HideGuide);
        backButton.onClick.AddListener(HideGuide);

        var backFeedback = backButtonRect.GetComponent<HomeUIButtonFeedback>();
        if (backFeedback == null)
            backFeedback = backButtonRect.gameObject.AddComponent<HomeUIButtonFeedback>();
        backFeedback.Configure(backButtonRect, backImage);

        var backLabel = CreateText(backButtonRect, "Label", "돌아가기", 22f, TextAlignmentOptions.Center, new Color(0.92f, 0.99f, 1f, 1f), out bool createdBackLabel);
        if (createdBackLabel)
            Stretch(backLabel.rectTransform);
        backLabel.fontStyle = FontStyles.Bold;
    }

    private void SetGuideVisible(bool visible)
    {
        if (_windowRoot != null)
            _windowRoot.SetActive(visible);

        if (_guideButtonRoot != null)
            _guideButtonRoot.SetActive(!visible);

        if (visible && _windowRoot != null)
            _windowRoot.transform.SetAsLastSibling();
    }

    private void ResolveGuideSprite()
    {
        if (_guideSprite != null || string.IsNullOrWhiteSpace(_guideResourcePath))
            return;

        _guideSprite = Resources.Load<Sprite>(_guideResourcePath);
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        return CreateRect(parent, name, out _);
    }

    private static RectTransform CreateRect(Transform parent, string name, out bool created)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null)
        {
            created = false;
            return existing;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        created = true;
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        return CreateText(parent, name, value, size, alignment, color, out _);
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Color color,
        out bool created)
    {
        var rect = CreateRect(parent, name, out created);
        var text = rect.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.text = value;
        text.fontSize = size;
        text.fontSizeMin = Mathf.Max(10f, size - 8f);
        text.fontSizeMax = size;
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        ApplyPreferredFont(text);
        return text;
    }

    private static T Require<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component == null)
            component = go.AddComponent<T>();

        return component;
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        var child = parent.Find(childName);
        if (child == null)
            return;

        if (Application.isPlaying)
            Destroy(child.gameObject);
        else
            DestroyImmediate(child.gameObject);
    }

    private static void ApplyPreferredFont(TMP_Text text)
    {
        var font = LoadPreferredFont();
        if (font == null)
            return;

        text.font = font;
        text.fontSharedMaterial = font.material;
    }

    private static TMP_FontAsset LoadPreferredFont()
    {
        if (_preferredFont != null)
            return _preferredFont;

        _preferredFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Gowun Batang");
        if (_preferredFont == null)
            _preferredFont = Resources.Load<TMP_FontAsset>("Gowun Batang");
        if (_preferredFont == null)
            _preferredFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/NanumGothic SDF");
        if (_preferredFont == null)
            _preferredFont = Resources.Load<TMP_FontAsset>("NanumGothic SDF");
        return _preferredFont;
    }

    private static void EnsureEventSystem()
    {
        var systems = Resources.FindObjectsOfTypeAll<EventSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            var system = systems[i];
            if (system == null || !system.gameObject.scene.IsValid())
                continue;

            if (!system.gameObject.activeSelf)
                system.gameObject.SetActive(true);

            system.enabled = true;
            EnsureInputModule(system.gameObject);
            return;
        }

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EnsureInputModule(eventSystemGo);
    }

    private static void EnsureInputModule(GameObject eventSystemGo)
    {
        if (eventSystemGo == null)
            return;

        var inputModule = eventSystemGo.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();

        inputModule.enabled = true;

        var standaloneModule = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null)
            standaloneModule.enabled = false;
    }
}

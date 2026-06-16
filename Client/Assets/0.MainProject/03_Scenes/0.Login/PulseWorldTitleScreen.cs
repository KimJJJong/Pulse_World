using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PulseWorldTitleScreen : MonoBehaviour
{
    private const string AssetPath = "UI/UI_InitTitle/pulse_world_transparent_png_assets/";
    private const string GlowShaderPath = "UI/UI_InitTitle/PulseWorldGlow";
    private const string SharedOptionsPanelResourcePath = "UI/Options/PF_GameOptionsPanel";
    private const string RuntimeRootName = "PulseWorldTitleRuntime";

    private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
    private readonly List<TitleButton> _titleButtons = new List<TitleButton>();

    private Canvas _canvas;
    private LoginView _view;
    private ConfirmPopup _confirm;
    private RectTransform _runtimeRoot;
    private RectTransform _titleRoot;
    private RectTransform _promptRoot;
    private CanvasGroup _promptGroup;
    private CanvasGroup _buttonGroup;
    private CanvasGroup _settingsGroup;
    private CanvasGroup _statusGroup;
    private RawImage _backgroundGlow;
    private RawImage _backgroundSparkle;
    private RawImage _titleGlow;
    private TMP_Text _statusText;
    private TMP_Text _settingsDeviceText;
    private GameOptionsPanel _titleOptionsPanel;
    private Material _backgroundGlowMaterial;
    private Material _backgroundSparkleMaterial;
    private Material _titleGlowMaterial;
    private Button _startButton;
    private Button _settingsButton;
    private Button _exitButton;

    private bool _revealed;
    private bool _settingsVisible;
    private float _revealTime = -1f;
    private string _lastStatus = "";

    private static readonly Color Cyan = new Color(0.12f, 1f, 0.93f, 1f);
    private static readonly Color WarmText = new Color(1f, 0.88f, 0.62f, 1f);
    private static readonly Color PanelDark = new Color(0.02f, 0.09f, 0.10f, 0.86f);
    private static readonly Color PanelLine = new Color(0.12f, 0.96f, 0.92f, 0.52f);

    private sealed class TitleButton
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public Button Button;
        public Vector2 HiddenPosition;
        public Vector2 ShownPosition;
        public float Delay;
    }

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
            _canvas = FindSceneObject<Canvas>();

        if (_canvas == null)
        {
            Debug.LogWarning("PulseWorldTitleScreen requires a Canvas in the Login scene.");
            enabled = false;
            return;
        }

        _view = FindSceneObject<LoginView>();
        _confirm = FindSceneObject<ConfirmPopup>();

        ConfigureCanvas();
        HideLegacyPanel();
        if (!TryUseExistingRuntimeTitle())
            BuildRuntimeTitle();
        BringConfirmPopupForward();
    }

    private void Start()
    {
        WireButtons();
        SyncActionButtonState();
    }

    private void Update()
    {
        var time = Time.unscaledTime;

        if (!_revealed && Input.anyKeyDown)
            RevealButtons();

        AnimateAmbient(time);
        AnimatePrompt(time);
        AnimateButtons(time);
        SyncStatus();
        SyncActionButtonState();
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(_backgroundGlowMaterial);
        DestroyRuntimeMaterial(_backgroundSparkleMaterial);
        DestroyRuntimeMaterial(_titleGlowMaterial);
    }

    private void ConfigureCanvas()
    {
        var scaler = _canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        var camera = FindSceneObject<Camera>();
        if (camera == null)
            return;

        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 5f;

        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = camera;
        _canvas.planeDistance = 10f;
    }

    private void HideLegacyPanel()
    {
        var panel = _canvas.transform.Find("Panel");
        if (panel == null)
            return;

        var group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = panel.gameObject.AddComponent<CanvasGroup>();

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var image = panel.GetComponent<Image>();
        if (image != null)
            image.enabled = false;
    }

    private void BuildRuntimeTitle()
    {
        _titleButtons.Clear();

        var existing = _canvas.transform.Find(RuntimeRootName);
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        _runtimeRoot = CreateRect(RuntimeRootName, _canvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(_runtimeRoot);
        _runtimeRoot.SetAsLastSibling();

        BuildBackground();
        BuildTitle();
        BuildPrompt();
        BuildButtons();
        BuildStatus();
        BuildSettingsPanel();
    }

    private bool TryUseExistingRuntimeTitle()
    {
        var existing = _canvas.transform.Find(RuntimeRootName) as RectTransform;
        if (existing == null)
            return false;

        _runtimeRoot = existing;
        CacheRuntimeReferences();
        ResetInitialPresentation();
        return true;
    }

    private void CacheRuntimeReferences()
    {
        _titleButtons.Clear();

        _titleRoot = FindRuntimeRect("TitlePulseRoot");
        _promptRoot = FindRuntimeRect("PressAnyPrompt");
        _promptGroup = EnsureCanvasGroup(_promptRoot);
        _buttonGroup = EnsureCanvasGroup(FindRuntimeRect("ButtonDock"));
        var optionPanel = FindRuntimeRect("OptionsPanel");
        if (optionPanel == null)
            optionPanel = FindRuntimeRect("SettingsPanel");
        _settingsGroup = EnsureCanvasGroup(optionPanel);
        _statusGroup = EnsureCanvasGroup(FindRuntimeRect("Status"));

        _backgroundGlow = FindRuntimeComponent<RawImage>("BackgroundGlowBreath");
        _backgroundSparkle = FindRuntimeComponent<RawImage>("BackgroundGlowSparkle");
        _titleGlow = FindRuntimeComponent<RawImage>("TitleCyanGlow");
        _titleOptionsPanel = _runtimeRoot != null ? _runtimeRoot.GetComponentInChildren<GameOptionsPanel>(true) : null;
        if (_titleOptionsPanel != null)
        {
            _titleOptionsPanel.CloseRequested -= HandleTitleOptionsCloseRequested;
            _titleOptionsPanel.CloseRequested += HandleTitleOptionsCloseRequested;
        }
        else
        {
            TryUseSharedOptionsPanel();
        }
        _statusText = FindRuntimeComponent<TMP_Text>("Text", _statusGroup != null ? _statusGroup.transform : null);
        _settingsDeviceText = FindRuntimeComponent<TMP_Text>("DeviceId", _settingsGroup != null ? _settingsGroup.transform : null);
        _startButton = FindRuntimeComponent<Button>("StartGameButton");
        _settingsButton = FindRuntimeComponent<Button>("SettingsButton");
        _exitButton = FindRuntimeComponent<Button>("ExitButton");

        DestroyRuntimeMaterial(_backgroundGlowMaterial);
        DestroyRuntimeMaterial(_backgroundSparkleMaterial);
        DestroyRuntimeMaterial(_titleGlowMaterial);

        _backgroundGlowMaterial = CreateGlowMaterial("PulseWorld Background Breath", Cyan, 0.55f, 0.10f);
        _backgroundSparkleMaterial = CreateGlowMaterial("PulseWorld Background Sparkle", Cyan, 0.35f, 0.55f);
        _titleGlowMaterial = CreateGlowMaterial("PulseWorld Title Glow", Cyan, 0.72f, 0.24f);

        if (_backgroundGlow != null)
            _backgroundGlow.material = _backgroundGlowMaterial;
        if (_backgroundSparkle != null)
            _backgroundSparkle.material = _backgroundSparkleMaterial;
        if (_titleGlow != null)
            _titleGlow.material = _titleGlowMaterial;

        CacheTitleButton("StartGameButton", new Vector2(-536f, 92f), 0f);
        CacheTitleButton("SettingsButton", new Vector2(0f, 92f), 0.08f);
        CacheTitleButton("ExitButton", new Vector2(536f, 92f), 0.16f);
    }

    private void CacheTitleButton(string name, Vector2 shownPosition, float delay)
    {
        var button = FindRuntimeComponent<Button>(name);
        if (button == null)
            return;

        var rect = button.transform as RectTransform;
        if (rect == null)
            return;

        var group = EnsureCanvasGroup(rect);
        _titleButtons.Add(new TitleButton
        {
            Rect = rect,
            Group = group,
            Button = button,
            HiddenPosition = new Vector2(shownPosition.x, -190f),
            ShownPosition = shownPosition,
            Delay = delay
        });
    }

    private void ResetInitialPresentation()
    {
        _revealed = false;
        _settingsVisible = false;
        _revealTime = -1f;

        if (_promptGroup != null)
        {
            _promptGroup.alpha = 1f;
            _promptGroup.interactable = false;
            _promptGroup.blocksRaycasts = false;
        }

        if (_buttonGroup != null)
        {
            _buttonGroup.interactable = false;
            _buttonGroup.blocksRaycasts = false;
        }

        for (var i = 0; i < _titleButtons.Count; i++)
        {
            var item = _titleButtons[i];
            item.Rect.anchoredPosition = item.HiddenPosition;
            item.Group.alpha = 0f;
        }

        if (_titleOptionsPanel != null)
        {
            _titleOptionsPanel.DiscardAndHide();
        }
        else if (_settingsGroup != null)
        {
            _settingsGroup.gameObject.SetActive(false);
            _settingsGroup.alpha = 0f;
            _settingsGroup.interactable = false;
            _settingsGroup.blocksRaycasts = false;
        }

        if (_statusGroup != null)
        {
            _statusGroup.alpha = 0f;
            _statusGroup.interactable = false;
            _statusGroup.blocksRaycasts = false;
        }
    }

    private void BuildBackground()
    {
        var background = CreateRawImage("Background", _runtimeRoot, "BG_Base");
        background.raycastTarget = false;
        Stretch(background.rectTransform);

        _backgroundGlowMaterial = CreateGlowMaterial("PulseWorld Background Breath", Cyan, 0.55f, 0.10f);
        _backgroundGlow = CreateRawImage("BackgroundGlowBreath", _runtimeRoot, "BG_Pulse_Glow");
        _backgroundGlow.raycastTarget = false;
        _backgroundGlow.material = _backgroundGlowMaterial;
        _backgroundGlow.color = new Color(1f, 1f, 1f, 0.28f);
        Stretch(_backgroundGlow.rectTransform);

        _backgroundSparkleMaterial = CreateGlowMaterial("PulseWorld Background Sparkle", Cyan, 0.35f, 0.55f);
        _backgroundSparkle = CreateRawImage("BackgroundGlowSparkle", _runtimeRoot, "BG_Pulse_Glow");
        _backgroundSparkle.raycastTarget = false;
        _backgroundSparkle.material = _backgroundSparkleMaterial;
        _backgroundSparkle.color = new Color(1f, 1f, 1f, 0.10f);
        Stretch(_backgroundSparkle.rectTransform);
    }

    private void BuildTitle()
    {
        _titleRoot = CreateRect("TitlePulseRoot", _runtimeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        _titleRoot.anchoredPosition = new Vector2(0f, 126f);
        _titleRoot.sizeDelta = new Vector2(920f, 690f);

        _titleGlowMaterial = CreateGlowMaterial("PulseWorld Title Glow", Cyan, 0.72f, 0.24f);
        _titleGlow = CreateRawImage("TitleCyanGlow", _titleRoot, "Title_Glow_Cyan_FullCanvas");
        _titleGlow.raycastTarget = false;
        _titleGlow.material = _titleGlowMaterial;
        _titleGlow.color = new Color(1f, 1f, 1f, 0.42f);
        Stretch(_titleGlow.rectTransform);

        var title = CreateRawImage("TitleLogo", _titleRoot, "Title_Logo_FullCanvas");
        title.raycastTarget = false;
        title.color = new Color(1f, 0.98f, 0.92f, 0.98f);
        Stretch(title.rectTransform);
    }

    private void BuildPrompt()
    {
        _promptRoot = CreateRect("PressAnyPrompt", _runtimeRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        _promptRoot.anchoredPosition = new Vector2(0f, 108f);
        _promptRoot.sizeDelta = new Vector2(560f, 76f);

        _promptGroup = _promptRoot.gameObject.AddComponent<CanvasGroup>();
        _promptGroup.alpha = 1f;
        _promptGroup.interactable = false;
        _promptGroup.blocksRaycasts = false;

        var prompt = CreateRawImage("Text", _promptRoot, "Press_Any_Key_Text");
        prompt.raycastTarget = false;
        prompt.color = new Color(1f, 0.94f, 0.72f, 1f);
        Stretch(prompt.rectTransform);
    }

    private void BuildButtons()
    {
        var root = CreateRect("ButtonDock", _runtimeRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(root);

        _buttonGroup = root.gameObject.AddComponent<CanvasGroup>();
        _buttonGroup.alpha = 1f;
        _buttonGroup.interactable = false;
        _buttonGroup.blocksRaycasts = false;

        _startButton = CreateTitleButton(root, "StartGameButton", "Button_Start", new Vector2(-536f, 92f), new Vector2(500f, 170f), 0f);
        _settingsButton = CreateTitleButton(root, "SettingsButton", "Button_Settings", new Vector2(0f, 92f), new Vector2(500f, 166f), 0.08f);
        _exitButton = CreateTitleButton(root, "ExitButton", "Button_Exit", new Vector2(536f, 92f), new Vector2(500f, 158f), 0.16f);
    }

    private void BuildStatus()
    {
        var statusRoot = CreateRect("Status", _runtimeRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        statusRoot.anchoredPosition = new Vector2(0f, 202f);
        statusRoot.sizeDelta = new Vector2(900f, 42f);

        _statusGroup = statusRoot.gameObject.AddComponent<CanvasGroup>();
        _statusGroup.alpha = 0f;
        _statusGroup.interactable = false;
        _statusGroup.blocksRaycasts = false;

        _statusText = CreateText("Text", statusRoot, "", 25f, TextAlignmentOptions.Center, WarmText);
        Stretch(_statusText.rectTransform);
    }

    private void BuildSettingsPanel()
    {
        if (TryUseSharedOptionsPanel())
            return;

        var existingPanel = FindRuntimeRect("OptionsPanel");
        if (existingPanel == null)
            existingPanel = FindRuntimeRect("SettingsPanel");

        if (existingPanel != null)
        {
            _settingsGroup = EnsureCanvasGroup(existingPanel);
            _settingsDeviceText = FindRuntimeComponent<TMP_Text>("DeviceId", existingPanel);
            existingPanel.gameObject.SetActive(false);
            return;
        }

        var panel = CreateRect("OptionsPanel", _runtimeRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        panel.anchoredPosition = new Vector2(0f, 286f);
        panel.sizeDelta = new Vector2(780f, 210f);

        _settingsGroup = panel.gameObject.AddComponent<CanvasGroup>();
        _settingsGroup.alpha = 0f;
        _settingsGroup.interactable = false;
        _settingsGroup.blocksRaycasts = false;

        var back = panel.gameObject.AddComponent<Image>();
        back.color = PanelDark;
        back.raycastTarget = true;

        var outline = CreateRect("Outline", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(outline);
        var outlineImage = outline.gameObject.AddComponent<Image>();
        outlineImage.color = PanelLine;
        outlineImage.raycastTarget = false;

        var inner = CreateRect("Inner", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        inner.offsetMin = new Vector2(2f, 2f);
        inner.offsetMax = new Vector2(-2f, -2f);
        var innerImage = inner.gameObject.AddComponent<Image>();
        innerImage.color = PanelDark;
        innerImage.raycastTarget = false;

        var title = CreateText("Title", panel, "Options", 30f, TextAlignmentOptions.MidlineLeft, WarmText);
        SetAnchored(title.rectTransform, new Vector2(34f, -22f), new Vector2(300f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        _settingsDeviceText = CreateText("DeviceId", panel, "Device ID", 20f, TextAlignmentOptions.MidlineLeft, new Color(0.74f, 1f, 0.96f, 0.92f));
        SetAnchored(_settingsDeviceText.rectTransform, new Vector2(34f, -78f), new Vector2(700f, 52f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        CreateTextButton(panel, "CopyButton", "Copy", new Vector2(-224f, -60f), new Vector2(168f, 48f), () => _view?.CopyDeviceIdButton?.onClick.Invoke());
        CreateTextButton(panel, "ResetButton", "Reset", new Vector2(0f, -60f), new Vector2(168f, 48f), () =>
        {
            BringConfirmPopupForward();
            _view?.ResetDeviceIdButton?.onClick.Invoke();
        });
        CreateTextButton(panel, "CloseButton", "Close", new Vector2(224f, -60f), new Vector2(168f, 48f), () => SetSettingsVisible(false));

        panel.gameObject.SetActive(false);
    }

    private bool TryUseSharedOptionsPanel()
    {
        if (_runtimeRoot == null)
            return false;

        _titleOptionsPanel = _runtimeRoot.GetComponentInChildren<GameOptionsPanel>(true);
        if (_titleOptionsPanel == null)
        {
            var prefab = Resources.Load<GameOptionsPanel>(SharedOptionsPanelResourcePath);
            if (prefab == null)
                return false;

            _titleOptionsPanel = Instantiate(prefab, _runtimeRoot, false);
            _titleOptionsPanel.gameObject.name = "UI_Home_Options";
        }

        if (_titleOptionsPanel.transform is RectTransform rect)
            Stretch(rect);

        _titleOptionsPanel.CloseRequested -= HandleTitleOptionsCloseRequested;
        _titleOptionsPanel.CloseRequested += HandleTitleOptionsCloseRequested;
        HideLegacySettingsPanelWhenShared();
        _titleOptionsPanel.HideImmediate();
        _settingsGroup = null;
        _settingsDeviceText = null;
        return true;
    }

    private void HideLegacySettingsPanelWhenShared()
    {
        if (_titleOptionsPanel == null)
            return;

        HideLegacySettingsRect(FindRuntimeRect("SettingsPanel"));
        HideLegacySettingsRect(FindRuntimeRect("OptionsPanel"));
    }

    private void HideLegacySettingsRect(RectTransform rect)
    {
        if (rect == null || rect.transform.IsChildOf(_titleOptionsPanel.transform))
            return;

        rect.gameObject.SetActive(false);
    }

    private void HandleTitleOptionsCloseRequested()
    {
        _settingsVisible = false;
    }

    private void WireButtons()
    {
        if (_startButton != null)
            _startButton.onClick.AddListener(StartLogin);

        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(() => SetSettingsVisible(!_settingsVisible));

        if (_exitButton != null)
            _exitButton.onClick.AddListener(QuitGame);
    }

    private void RevealButtons()
    {
        _revealed = true;
        _revealTime = Time.unscaledTime;
        _buttonGroup.interactable = true;
        _buttonGroup.blocksRaycasts = true;
        SetSettingsVisible(false);

        if (EventSystem.current != null && _startButton != null)
            EventSystem.current.SetSelectedGameObject(_startButton.gameObject);
    }

    private void StartLogin()
    {
        if (_view == null || _view.LoginButton == null || !_view.LoginButton.interactable)
            return;

        SetSettingsVisible(false);
        _view.LoginButton.onClick.Invoke();
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetSettingsVisible(bool visible)
    {
        _settingsVisible = visible;
        if (_titleOptionsPanel != null)
        {
            if (visible)
                _titleOptionsPanel.Open();
            else
                _titleOptionsPanel.DiscardAndHide();
            return;
        }

        if (_settingsGroup == null)
            return;

        _settingsGroup.gameObject.SetActive(visible);
        _settingsGroup.alpha = visible ? 1f : 0f;
        _settingsGroup.interactable = visible;
        _settingsGroup.blocksRaycasts = visible;
        UpdateSettingsDeviceText();
    }

    private void UpdateSettingsDeviceText()
    {
        if (_settingsDeviceText == null)
            return;

        var deviceId = _view != null && _view.DeviceIdText != null ? _view.DeviceIdText.text : "";
        _settingsDeviceText.text = string.IsNullOrWhiteSpace(deviceId) ? "Device ID" : $"Device ID  {deviceId}";
    }

    private void AnimateAmbient(float time)
    {
        var slow = Mathf.Sin(time * 1.65f) * 0.5f + 0.5f;
        var titlePulse = Mathf.Sin(time * 2.95f) * 0.5f + 0.5f;
        var flicker = Mathf.PerlinNoise(time * 0.78f, 6.37f);
        var sparkle = Mathf.PerlinNoise(9.41f, time * 1.55f);

        if (_titleRoot != null)
        {
            var scale = 1f + 0.018f * Mathf.Sin(time * 2.65f);
            _titleRoot.localScale = new Vector3(scale, scale, 1f);
        }

        if (_backgroundGlow != null)
            _backgroundGlow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.18f, 0.38f, slow));

        if (_backgroundSparkle != null)
            _backgroundSparkle.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.04f, 0.18f, sparkle));

        if (_titleGlow != null)
            _titleGlow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.24f, 0.66f, titlePulse));

        SetGlowIntensity(_backgroundGlowMaterial, Mathf.Lerp(0.35f, 0.74f, slow), Mathf.Lerp(0.06f, 0.18f, flicker));
        SetGlowIntensity(_backgroundSparkleMaterial, Mathf.Lerp(0.24f, 0.50f, sparkle), Mathf.Lerp(0.35f, 0.80f, flicker));
        SetGlowIntensity(_titleGlowMaterial, Mathf.Lerp(0.48f, 0.92f, titlePulse), Mathf.Lerp(0.16f, 0.36f, sparkle));
    }

    private void AnimatePrompt(float time)
    {
        if (_promptGroup == null)
            return;

        if (_revealed)
        {
            var fade = Mathf.Clamp01((time - _revealTime) / 0.26f);
            _promptGroup.alpha = 1f - Smooth01(fade);
            return;
        }

        var blink = Mathf.Sin(time * 4.7f) * 0.5f + 0.5f;
        _promptGroup.alpha = Mathf.Lerp(0.18f, 1f, Smooth01(blink));
    }

    private void AnimateButtons(float time)
    {
        if (!_revealed)
            return;

        for (var i = 0; i < _titleButtons.Count; i++)
        {
            var item = _titleButtons[i];
            var t = Mathf.Clamp01((time - _revealTime - item.Delay) / 0.46f);
            var eased = EaseOutBack(t);
            item.Rect.anchoredPosition = Vector2.LerpUnclamped(item.HiddenPosition, item.ShownPosition, eased);
            item.Group.alpha = Smooth01(t);
        }
    }

    private void SyncStatus()
    {
        if (_statusText == null || _statusGroup == null)
            return;

        var status = "";
        if (_view != null)
        {
            if (_view.Busy != null && _view.Busy.activeSelf)
                status = "Signing in...";
            else if (_view.ErrorText != null && _view.ErrorText.gameObject.activeSelf)
                status = _view.ErrorText.text;
        }

        if (_lastStatus != status)
        {
            _lastStatus = status;
            _statusText.text = status;
            UpdateSettingsDeviceText();
        }

        var target = string.IsNullOrWhiteSpace(status) ? 0f : 1f;
        _statusGroup.alpha = Mathf.MoveTowards(_statusGroup.alpha, target, Time.unscaledDeltaTime * 5f);
    }

    private void SyncActionButtonState()
    {
        var canUse = _revealed;
        var loginInteractable = _view == null || _view.LoginButton == null || _view.LoginButton.interactable;

        if (_startButton != null)
            _startButton.interactable = canUse && loginInteractable;

        if (_settingsButton != null)
            _settingsButton.interactable = canUse && loginInteractable;

        if (_exitButton != null)
            _exitButton.interactable = canUse;
    }

    private Button CreateTitleButton(RectTransform parent, string name, string textureName, Vector2 shownPosition, Vector2 size, float delay)
    {
        var rect = CreateRect(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(shownPosition.x, -190f);

        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = LoadTexture(textureName);
        image.raycastTarget = true;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.86f, 1f, 0.98f, 1f),
            pressedColor = new Color(0.62f, 0.95f, 0.90f, 1f),
            selectedColor = new Color(0.86f, 1f, 0.98f, 1f),
            disabledColor = new Color(0.45f, 0.48f, 0.50f, 0.68f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        var feedback = rect.gameObject.AddComponent<PulseWorldTitleButtonFeedback>();
        feedback.Initialize(rect);

        _titleButtons.Add(new TitleButton
        {
            Rect = rect,
            Group = group,
            Button = button,
            HiddenPosition = new Vector2(shownPosition.x, -190f),
            ShownPosition = shownPosition,
            Delay = delay
        });

        return button;
    }

    private void CreateTextButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.05f, 0.22f, 0.24f, 0.96f);
        image.raycastTarget = true;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.78f, 1f, 0.96f, 1f),
            pressedColor = new Color(0.55f, 0.92f, 0.88f, 1f),
            selectedColor = new Color(0.78f, 1f, 0.96f, 1f),
            disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
        button.onClick.AddListener(onClick);

        var text = CreateText("Label", rect, label, 21f, TextAlignmentOptions.Center, WarmText);
        Stretch(text.rectTransform);
    }

    private RawImage CreateRawImage(string name, RectTransform parent, string textureName)
    {
        var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = LoadTexture(textureName);
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text CreateText(string name, RectTransform parent, string value, float size, TextAlignmentOptions alignment, Color color)
    {
        var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private Texture2D LoadTexture(string textureName)
    {
        if (_textures.TryGetValue(textureName, out var texture))
            return texture;

        texture = Resources.Load<Texture2D>(AssetPath + textureName);
        if (texture == null)
        {
            Debug.LogWarning($"PulseWorldTitleScreen missing texture: {AssetPath}{textureName}");
            return null;
        }

        _textures[textureName] = texture;
        return texture;
    }

    private Material CreateGlowMaterial(string name, Color tint, float intensity, float sparkle)
    {
        var shader = Resources.Load<Shader>(GlowShaderPath);
        if (shader == null)
            shader = Shader.Find("UI/Default");

        if (shader == null)
            return null;

        var material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave
        };

        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", tint);
        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", intensity);
        if (material.HasProperty("_Sparkle"))
            material.SetFloat("_Sparkle", sparkle);
        if (material.HasProperty("_TimeOffset"))
            material.SetFloat("_TimeOffset", Random.Range(0f, 12f));

        return material;
    }

    private static void SetGlowIntensity(Material material, float intensity, float sparkle)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", intensity);
        if (material.HasProperty("_Sparkle"))
            material.SetFloat("_Sparkle", sparkle);
    }

    private void BringConfirmPopupForward()
    {
        if (_confirm != null)
            _confirm.transform.SetAsLastSibling();
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private RectTransform FindRuntimeRect(string objectName)
    {
        return FindRuntimeComponent<RectTransform>(objectName);
    }

    private T FindRuntimeComponent<T>(string objectName, Transform scope = null) where T : Component
    {
        var root = scope != null ? scope : _runtimeRoot;
        if (root == null)
            return null;

        var components = root.GetComponentsInChildren<T>(true);
        for (var i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].name == objectName)
                return components[i];
        }

        return null;
    }

    private static CanvasGroup EnsureCanvasGroup(RectTransform rect)
    {
        if (rect == null)
            return null;

        var group = rect.GetComponent<CanvasGroup>();
        if (group == null)
            group = rect.gameObject.AddComponent<CanvasGroup>();

        return group;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static T FindSceneObject<T>() where T : Component
    {
        var objects = Resources.FindObjectsOfTypeAll<T>();
        for (var i = 0; i < objects.Length; i++)
        {
            var item = objects[i];
            if (item != null && item.gameObject.scene.IsValid())
                return item;
        }

        return null;
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Pulse World Title Preview")]
    private void RebuildPulseWorldTitlePreview()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
            _canvas = FindSceneObject<Canvas>();

        if (_canvas == null)
            return;

        _view = FindSceneObject<LoginView>();
        _confirm = FindSceneObject<ConfirmPopup>();

        ConfigureCanvas();
        HideLegacyPanel();
        BuildRuntimeTitle();
        ResetInitialPresentation();
        BringConfirmPopupForward();

        UnityEditor.EditorUtility.SetDirty(_canvas.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_canvas.gameObject.scene);
    }
#endif
}

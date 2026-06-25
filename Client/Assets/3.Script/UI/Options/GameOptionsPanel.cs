using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameOptionsPanel : MonoBehaviour
{
    private const string HomeOptionResourcePrefix = "UI/UI_Home_Option/";
    private const string BackButtonResourcePath = "UI/UI_Map/UI_Button_Back";
    private const string MasterVolumeKey = "Options.MasterVolume";
    private const string InGameVolumeKey = "Options.InGameVolume";
    private const string SfxVolumeKey = "Options.SfxVolume";
    private const string FullscreenKey = "Options.Fullscreen";
    private const string LegacySoundMutedKey = "Options.SoundMuted";
    private const string DeviceIdKey = "client.deviceId";
    private const float DefaultMasterVolume = 0.85f;
    private const float DefaultInGameVolume = 0.80f;
    private const float DefaultSfxVolume = 0.75f;
    private static readonly Rect BackButtonRect = new(28f, 24f, 76f, 58f);

    private static TMP_FontAsset _koreanFont;

    [SerializeField] private RectTransform _panel;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _inGameVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private TMP_Text _masterVolumeValueText;
    [SerializeField] private TMP_Text _inGameVolumeValueText;
    [SerializeField] private TMP_Text _sfxVolumeValueText;
    [SerializeField] private Image _fullscreenToggleImage;
    [SerializeField] private Image _windowedToggleImage;
    [SerializeField] private Button _fullscreenButton;
    [SerializeField] private Button _windowedButton;
    [SerializeField] private Button _resetDefaultsButton;
    [SerializeField] private Button _resetGuestButton;
    [SerializeField] private Button _applyButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _closeButton;

    private float _pendingMasterVolume = DefaultMasterVolume;
    private float _pendingInGameVolume = DefaultInGameVolume;
    private float _pendingSfxVolume = DefaultSfxVolume;
    private bool _pendingFullscreen;
    private bool _suppressCallbacks;

    public event Action CloseRequested;

    public bool IsOpen => gameObject.activeSelf;
    public RectTransform PanelRect => _panel;

    private void Awake()
    {
        ResolveReferences();
        BindControls();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindControls();
        LoadPendingOptionsFromPrefs();
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
    }

    public void Open()
    {
        ResolveReferences();
        BindControls();
        gameObject.SetActive(true);
        LoadPendingOptionsFromPrefs();
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
        transform.SetAsLastSibling();
    }

    public void Toggle()
    {
        if (IsOpen)
            DiscardAndHide();
        else
            Open();
    }

    public void HideImmediate()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public void DiscardAndHide()
    {
        LoadPendingOptionsFromPrefs();
        ApplyRuntimeOptionPreview();
        SetOptionsStatus("");
        HideImmediate();
    }

    public void RebuildDefaultLayout()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        var rootRect = (RectTransform)transform;
        Stretch(rootRect);

        var rootImage = GetComponent<Image>();
        if (rootImage == null)
            rootImage = gameObject.AddComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.52f);
        rootImage.raycastTarget = true;

        _panel = CreatePanel(rootRect, "OptionsPanel", new Vector2(940f, 620f), new Color(0.60f, 0.43f, 0.24f, 1f));
        _backButton = CreateBackButton(rootRect, "Button_Back");
        var panelSize = ResolvePanelSize(_panel);

        var titleColor = new Color(0.18f, 0.09f, 0.025f, 1f);
        var textColor = new Color(0.22f, 0.10f, 0.025f, 1f);
        var mutedColor = new Color(0.34f, 0.20f, 0.08f, 1f);
        var lightText = new Color(1f, 0.91f, 0.70f, 1f);

        var title = CreateText(_panel, "Title", "옵션", 38f, TextAlignmentOptions.Center, titleColor, new Rect(330f, 72f, 280f, 44f), panelSize);
        title.fontStyle = FontStyles.Bold;
        CreateSeparator(_panel, "TitleSeparatorLeft", new Rect(118f, 84f, 200f, 24f), panelSize);
        CreateSeparator(_panel, "TitleSeparatorRight", new Rect(622f, 84f, 200f, 24f), panelSize);

        CreateSectionHeader(_panel, "SoundHeader", "사운드", new Rect(118f, 140f, 158f, 42f), panelSize, lightText);
        CreateSeparator(_panel, "SoundSeparator", new Rect(266f, 147f, 590f, 28f), panelSize);

        CreateText(_panel, "MasterVolumeLabel", "마스터 음량", 21f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(164f, 204f, 180f, 34f), panelSize).fontStyle = FontStyles.Bold;
        _masterVolumeSlider = CreateVolumeSlider(_panel, "MasterVolumeSlider", new Rect(360f, 202f, 390f, 38f), panelSize);
        _masterVolumeValueText = CreateText(_panel, "MasterVolumeValue", "", 15f, TextAlignmentOptions.Center, mutedColor, new Rect(768f, 208f, 52f, 24f), panelSize);

        CreateText(_panel, "InGameVolumeLabel", "인게임 음량", 21f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(164f, 262f, 180f, 34f), panelSize).fontStyle = FontStyles.Bold;
        _inGameVolumeSlider = CreateVolumeSlider(_panel, "InGameVolumeSlider", new Rect(360f, 260f, 390f, 38f), panelSize);
        _inGameVolumeValueText = CreateText(_panel, "InGameVolumeValue", "", 15f, TextAlignmentOptions.Center, mutedColor, new Rect(768f, 266f, 52f, 24f), panelSize);

        CreateText(_panel, "SfxVolumeLabel", "효과음", 21f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(164f, 320f, 180f, 34f), panelSize).fontStyle = FontStyles.Bold;
        _sfxVolumeSlider = CreateVolumeSlider(_panel, "SfxVolumeSlider", new Rect(360f, 318f, 390f, 38f), panelSize);
        _sfxVolumeValueText = CreateText(_panel, "SfxVolumeValue", "", 15f, TextAlignmentOptions.Center, mutedColor, new Rect(768f, 324f, 52f, 24f), panelSize);

        CreateSectionHeader(_panel, "ScreenHeader", "화면", new Rect(118f, 392f, 158f, 42f), panelSize, lightText);
        CreateSeparator(_panel, "ScreenSeparator", new Rect(266f, 399f, 590f, 28f), panelSize);

        _fullscreenButton = CreateScreenModeOption(_panel, "FullscreenMode", "전체화면", new Rect(642f, 452f, 178f, 36f), panelSize, true);
        _windowedButton = CreateScreenModeOption(_panel, "WindowedMode", "창화면", new Rect(642f, 496f, 178f, 36f), panelSize, false);

        CreateSeparator(_panel, "BottomSeparator", new Rect(118f, 536f, 704f, 20f), panelSize);
        _statusText = CreateText(_panel, "Status", "", 13f, TextAlignmentOptions.Center, mutedColor, new Rect(286f, 534f, 368f, 20f), panelSize);

        _resetDefaultsButton = CreateOptionButton(_panel, "Button_ResetDefaults", "기본값", new Rect(118f, 562f, 154f, 48f), panelSize);
        _resetGuestButton = CreateOptionButton(_panel, "Button_ResetGuest", "Guest 초기화", new Rect(302f, 562f, 154f, 48f), panelSize);
        _applyButton = CreateOptionButton(_panel, "Button_Apply", "적용", new Rect(486f, 562f, 154f, 48f), panelSize);
        _closeButton = CreateOptionButton(_panel, "Button_Close", "닫기", new Rect(670f, 562f, 154f, 48f), panelSize);

        ResolveReferences();
        BindControls();
        LoadPendingOptionsFromPrefs();
        RefreshOptionsUi();
    }

    private void ResolveReferences()
    {
        if (_panel == null)
            _panel = FindChildRect(transform, "OptionsPanel");

        var panel = _panel != null ? _panel : transform as RectTransform;
        if (panel == null)
            return;

        _statusText ??= FindChildComponent<TMP_Text>(panel, "Status");
        _masterVolumeSlider ??= FindChildComponent<Slider>(panel, "MasterVolumeSlider");
        _inGameVolumeSlider ??= FindChildComponent<Slider>(panel, "InGameVolumeSlider");
        _sfxVolumeSlider ??= FindChildComponent<Slider>(panel, "SfxVolumeSlider");
        _masterVolumeValueText ??= FindChildComponent<TMP_Text>(panel, "MasterVolumeValue");
        _inGameVolumeValueText ??= FindChildComponent<TMP_Text>(panel, "InGameVolumeValue");
        _sfxVolumeValueText ??= FindChildComponent<TMP_Text>(panel, "SfxVolumeValue");

        var fullscreen = FindChild(panel, "FullscreenMode");
        var windowed = FindChild(panel, "WindowedMode");
        _fullscreenButton ??= fullscreen != null ? fullscreen.GetComponent<Button>() : null;
        _windowedButton ??= windowed != null ? windowed.GetComponent<Button>() : null;
        _fullscreenToggleImage ??= FindChildComponent<Image>(fullscreen, "Icon");
        _windowedToggleImage ??= FindChildComponent<Image>(windowed, "Icon");
        _resetDefaultsButton ??= FindChildComponent<Button>(panel, "Button_ResetDefaults");
        _resetGuestButton ??= FindChildComponent<Button>(panel, "Button_ResetGuest");
        _applyButton ??= FindChildComponent<Button>(panel, "Button_Apply");
        _closeButton ??= FindChildComponent<Button>(panel, "Button_Close");
        EnsureBackButton();
    }

    private void BindControls()
    {
        BindSlider(_masterVolumeSlider, HandleMasterVolumeChanged);
        BindSlider(_inGameVolumeSlider, HandleInGameVolumeChanged);
        BindSlider(_sfxVolumeSlider, HandleSfxVolumeChanged);
        Bind(_fullscreenButton, HandleFullscreenClicked);
        Bind(_windowedButton, HandleWindowedClicked);
        Bind(_resetDefaultsButton, HandleResetDefaultsClicked);
        Bind(_resetGuestButton, HandleResetGuestClicked);
        Bind(_applyButton, HandleApplyOptionsClicked);
        Bind(_backButton, HandleCloseOptionsClicked);
        Bind(_closeButton, HandleCloseOptionsClicked);
    }

    private void HandleMasterVolumeChanged(float value)
    {
        if (_suppressCallbacks)
            return;

        _pendingMasterVolume = Mathf.Clamp01(value);
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
        SetOptionsStatus("마스터 음량 미리 적용 중");
    }

    private void HandleInGameVolumeChanged(float value)
    {
        if (_suppressCallbacks)
            return;

        _pendingInGameVolume = Mathf.Clamp01(value);
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
        SetOptionsStatus("인게임 음량 미리 적용 중");
    }

    private void HandleSfxVolumeChanged(float value)
    {
        if (_suppressCallbacks)
            return;

        _pendingSfxVolume = Mathf.Clamp01(value);
        RefreshOptionsUi();
        SetOptionsStatus("효과음 음량 미리 적용 중");
    }

    private void HandleFullscreenClicked()
    {
        HandleScreenModeClicked(true);
    }

    private void HandleWindowedClicked()
    {
        HandleScreenModeClicked(false);
    }

    private void HandleScreenModeClicked(bool fullscreen)
    {
        _pendingFullscreen = fullscreen;
        RefreshOptionsUi();
        SetOptionsStatus(fullscreen ? "전체화면으로 적용 대기" : "창화면으로 적용 대기");
    }

    private void HandleResetDefaultsClicked()
    {
        _pendingMasterVolume = DefaultMasterVolume;
        _pendingInGameVolume = DefaultInGameVolume;
        _pendingSfxVolume = DefaultSfxVolume;
        _pendingFullscreen = true;
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
        SetOptionsStatus("기본값으로 되돌렸습니다. 적용을 누르면 저장됩니다.");
    }

    private void HandleResetGuestClicked()
    {
        var newDeviceId = ResetGuestIdentity();
        SetOptionsStatus($"Guest 번호를 초기화했습니다. 다시 로그인하면 적용됩니다. ({ShortDeviceId(newDeviceId)})");
    }

    private void HandleApplyOptionsClicked()
    {
        SavePendingOptions();
        ApplyRuntimeOptionPreview();
        ApplyScreenMode(_pendingFullscreen);
        SetOptionsStatus("설정이 적용되었습니다.");
    }

    private void HandleCloseOptionsClicked()
    {
        DiscardAndHide();
        CloseRequested?.Invoke();
    }

    private void LoadPendingOptionsFromPrefs()
    {
        var legacyMuted = PlayerPrefs.GetInt(LegacySoundMutedKey, 0) == 1;
        _pendingMasterVolume = ClampSavedVolume(MasterVolumeKey, legacyMuted ? 0f : DefaultMasterVolume);
        _pendingInGameVolume = ClampSavedVolume(InGameVolumeKey, DefaultInGameVolume);
        _pendingSfxVolume = ClampSavedVolume(SfxVolumeKey, DefaultSfxVolume);
        _pendingFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
    }

    private void SavePendingOptions()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(_pendingMasterVolume));
        PlayerPrefs.SetFloat(InGameVolumeKey, Mathf.Clamp01(_pendingInGameVolume));
        PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(_pendingSfxVolume));
        PlayerPrefs.SetInt(FullscreenKey, _pendingFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(LegacySoundMutedKey, _pendingMasterVolume <= 0.001f ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void RefreshOptionsUi()
    {
        _suppressCallbacks = true;
        SetSliderValue(_masterVolumeSlider, _pendingMasterVolume);
        SetSliderValue(_inGameVolumeSlider, _pendingInGameVolume);
        SetSliderValue(_sfxVolumeSlider, _pendingSfxVolume);
        _suppressCallbacks = false;

        SetVolumeText(_masterVolumeValueText, _pendingMasterVolume);
        SetVolumeText(_inGameVolumeValueText, _pendingInGameVolume);
        SetVolumeText(_sfxVolumeValueText, _pendingSfxVolume);

        var active = LoadOptionSprite("Circle_Button_Active");
        var inactive = LoadOptionSprite("Circle_Button_Deactive");
        if (_fullscreenToggleImage != null)
            _fullscreenToggleImage.sprite = _pendingFullscreen ? active : inactive;
        if (_windowedToggleImage != null)
            _windowedToggleImage.sprite = _pendingFullscreen ? inactive : active;
    }

    private void ApplyRuntimeOptionPreview()
    {
        ApplyAudioVolumes(_pendingMasterVolume, _pendingInGameVolume);
        FMODDrumSequencer.SetRuntimeVolumePreview(_pendingMasterVolume, _pendingInGameVolume);
        UiSfxService.SetRuntimeVolumePreview(_pendingMasterVolume, _pendingSfxVolume);
    }

    private void SetOptionsStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message ?? "";
    }

    private static float ClampSavedVolume(string key, float fallback)
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
    }

    private static void SetSliderValue(Slider slider, float value)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
    }

    private static void SetVolumeText(TMP_Text text, float value)
    {
        if (text != null)
            text.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}";
    }

    private static void ApplyAudioVolumes(float masterVolume, float inGameVolume)
    {
        AudioListener.volume = Mathf.Clamp01(masterVolume);
        ApplyInGameVolume(Mathf.Clamp01(inGameVolume));
    }

    private static void ApplyInGameVolume(float volume)
    {
        var sources = Resources.FindObjectsOfTypeAll<AudioSource>();
        foreach (var source in sources)
        {
            if (source == null || !source.gameObject.scene.IsValid())
                continue;

            if (!LooksLikeInGameAudioSource(source))
                continue;

            source.volume = volume;
        }
    }

    private static bool LooksLikeInGameAudioSource(AudioSource source)
    {
        var objectName = source.gameObject.name;
        var clipName = source.clip != null ? source.clip.name : "";
        return ContainsAudioToken(objectName) || ContainsAudioToken(clipName);
    }

    private static bool ContainsAudioToken(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.IndexOf("bgm", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("town_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApplyScreenMode(bool fullscreen)
    {
        Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.fullScreen = fullscreen;
    }

    public static string ResetGuestIdentity()
    {
        var root = AppBootstrap.Instance != null ? AppBootstrap.Instance.Root : null;
        if (root != null)
            root.Tokens.Clear();
        else
            new TokenStore().Clear();

        var newDeviceId = root != null
            ? root.Identity.ResetDeviceId()
            : GenerateAndSaveDeviceId();

        return newDeviceId;
    }

    private static string GenerateAndSaveDeviceId()
    {
        var deviceId = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(DeviceIdKey, deviceId);
        PlayerPrefs.Save();
        return deviceId;
    }

    private static string ShortDeviceId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return "-";

        return deviceId.Length <= 8 ? deviceId : deviceId.Substring(0, 8);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null || action == null)
            return;

        slider.onValueChanged.RemoveListener(action);
        slider.onValueChanged.AddListener(action);
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChild(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static RectTransform FindChildRect(Transform root, string name)
        => FindChild(root, name) as RectTransform;

    private static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        var child = FindChild(root, name);
        return child != null ? child.GetComponent<T>() : null;
    }

    private void EnsureBackButton()
    {
        if (_backButton != null)
        {
            ConfigureBackButton(_backButton);
            return;
        }

        var root = transform as RectTransform;
        if (root == null)
            return;

        var existing = FindChild(root, "Button_Back");
        _backButton = existing != null ? existing.GetComponent<Button>() : null;
        if (_backButton == null)
            _backButton = CreateBackButton(root, "Button_Back");

        ConfigureBackButton(_backButton);
    }

    private static Button CreateBackButton(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        SetRectFromTopLeft(rect, BackButtonRect, new Vector2(BackButtonRect.width, BackButtonRect.height));

        var image = go.GetComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>(BackButtonResourcePath);
        image.color = Color.white;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        ConfigureBackButton(button);
        return button;
    }

    private static void ConfigureBackButton(Button button)
    {
        if (button == null)
            return;

        var rect = button.transform as RectTransform;
        if (rect != null)
        {
            SetRectFromTopLeft(rect, BackButtonRect, new Vector2(BackButtonRect.width, BackButtonRect.height));
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
        }

        var image = button.GetComponent<RawImage>();
        if (image != null)
        {
            image.texture = Resources.Load<Texture2D>(BackButtonResourcePath);
            image.color = Color.white;
            image.raycastTarget = true;
            button.targetGraphic = image;
        }

        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };

        var feedback = button.GetComponent<HomeUIButtonFeedback>() ?? button.gameObject.AddComponent<HomeUIButtonFeedback>();
        feedback.Configure(rect, button.targetGraphic);
    }

    private static Vector2 ResolvePanelSize(RectTransform panel)
    {
        if (panel == null)
            return new Vector2(940f, 620f);

        var size = panel.sizeDelta;
        if (size.x <= 1f || size.y <= 1f)
            size = panel.rect.size;

        if (size.x <= 1f)
            size.x = 940f;
        if (size.y <= 1f)
            size.y = 620f;

        return size;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = LoadOptionSprite("Panel");
        image.color = image.sprite != null ? Color.white : color;
        image.raycastTarget = true;

        return rect;
    }

    private static void CreateSectionHeader(RectTransform parent, string name, string label, Rect rect, Vector2 sourceSize, Color textColor)
    {
        var image = CreateOptionImage(parent, name, "Section_TextBox", rect, sourceSize);
        image.raycastTarget = false;
        var text = CreateText(image.rectTransform, "Label", label, 20f, TextAlignmentOptions.Center, textColor, new Rect(0f, 0f, rect.width, rect.height), new Vector2(rect.width, rect.height));
        text.fontStyle = FontStyles.Bold;
    }

    private static void CreateSeparator(RectTransform parent, string name, Rect rect, Vector2 sourceSize)
    {
        var image = CreateOptionImage(parent, name, "Section_Seperater", rect, sourceSize);
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.92f);
    }

    private static Slider CreateVolumeSlider(RectTransform parent, string name, Rect rect, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Slider));
        go.transform.SetParent(parent, false);
        var root = (RectTransform)go.transform;
        SetRectFromTopLeft(root, rect, sourceSize);

        var sliderSize = new Vector2(rect.width, rect.height);
        const float trackLeft = 20f;
        const float trackTop = 11f;
        const float trackHeight = 16f;
        const float handleWidth = 22f;
        const float handleHeight = 32f;
        var trackWidth = rect.width - 58f;

        var background = CreateOptionImage(root, "Background", "Volume_Bar_Back", new Rect(trackLeft, trackTop, trackWidth, trackHeight), sliderSize);
        background.raycastTarget = false;

        var fillArea = CreateRect(root, "Fill Area");
        SetRectFromTopLeft(fillArea, new Rect(trackLeft + 5f, trackTop + 3f, trackWidth - 10f, trackHeight - 6f), sliderSize);

        var fill = CreateOptionImage(fillArea, "Fill", "Volume_Bar_Fill", new Rect(0f, 0f, trackWidth - 10f, trackHeight - 6f), new Vector2(trackWidth - 10f, trackHeight - 6f));
        Stretch(fill.rectTransform);
        fill.raycastTarget = false;

        var handleArea = CreateRect(root, "Handle Slide Area");
        SetRectFromTopLeft(handleArea, new Rect(trackLeft, 0f, trackWidth, rect.height), sliderSize);

        var handle = CreateOptionImage(handleArea, "Handle", "Volume_Button", new Rect(0f, 0f, handleWidth, handleHeight), new Vector2(trackWidth, rect.height));
        handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(handleWidth, handleHeight);
        handle.rectTransform.anchoredPosition = Vector2.zero;
        handle.raycastTarget = true;

        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    private Button CreateScreenModeOption(RectTransform parent, string name, string label, Rect rect, Vector2 sourceSize, bool fullscreen)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var root = (RectTransform)go.transform;
        SetRectFromTopLeft(root, rect, sourceSize);
        var hitArea = go.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0f);
        hitArea.raycastTarget = true;

        var optionSize = new Vector2(rect.width, rect.height);
        var icon = CreateOptionImage(root, "Icon", "Circle_Button_Deactive", new Rect(2f, 7f, 22f, 22f), optionSize);
        icon.raycastTarget = true;
        if (fullscreen)
            _fullscreenToggleImage = icon;
        else
            _windowedToggleImage = icon;

        var text = CreateText(root, "Label", label, 16f, TextAlignmentOptions.MidlineLeft, new Color(0.22f, 0.10f, 0.025f, 1f), new Rect(34f, 0f, rect.width - 34f, rect.height), optionSize);
        text.fontStyle = FontStyles.Bold;

        var button = go.GetComponent<Button>();
        button.targetGraphic = icon;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.92f, 0.68f, 1f);
        colors.pressedColor = new Color(0.86f, 0.62f, 0.30f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    private static Button CreateOptionButton(RectTransform parent, string name, string label, Rect rectValue, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var buttonRect = (RectTransform)go.transform;
        SetRectFromTopLeft(buttonRect, rectValue, sourceSize);

        var image = go.GetComponent<Image>();
        image.sprite = LoadOptionSprite("Button_Default");
        image.color = image.sprite != null ? Color.white : new Color(0.35f, 0.20f, 0.09f, 1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        var pressed = LoadOptionSprite("Button_Pressed");
        if (pressed != null)
        {
            var spriteState = button.spriteState;
            spriteState.highlightedSprite = pressed;
            spriteState.pressedSprite = pressed;
            spriteState.selectedSprite = pressed;
            button.spriteState = spriteState;
        }

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.82f, 1f);
        colors.pressedColor = new Color(0.86f, 0.76f, 0.58f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.58f, 0.48f, 0.36f, 0.82f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var text = CreateText(buttonRect, "Label", label, 24f, TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.72f, 1f), new Rect(0f, 0f, buttonRect.sizeDelta.x, buttonRect.sizeDelta.y), buttonRect.sizeDelta);
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, string value, float size, TextAlignmentOptions alignment, Color color, Rect rectValue, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        SetRectFromTopLeft(rect, rectValue, sourceSize);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Max(10f, size - 12f);
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        ApplyPreferredFont(text);
        return text;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image CreateOptionImage(Transform parent, string name, string resourceName, Rect rect, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rectTransform = (RectTransform)go.transform;
        SetRectFromTopLeft(rectTransform, rect, sourceSize);

        var image = go.GetComponent<Image>();
        image.sprite = LoadOptionSprite(resourceName);
        image.color = image.sprite != null ? Color.white : new Color(0.55f, 0.36f, 0.16f, 1f);
        image.preserveAspect = false;
        return image;
    }

    private static Sprite LoadOptionSprite(string resourceName)
    {
        return Resources.Load<Sprite>(HomeOptionResourcePrefix + resourceName);
    }

    private static void SetRectFromTopLeft(RectTransform rect, Rect rectValue, Vector2 sourceSize)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(rectValue.width, rectValue.height);
        rect.anchoredPosition = new Vector2(rectValue.x, -rectValue.y);
    }

    private static void ApplyPreferredFont(TMP_Text text)
    {
        if (text == null)
            return;

        var font = LoadKoreanFont();
        if (font == null)
            return;

        text.font = font;
        text.fontSharedMaterial = font.material;
    }

    private static TMP_FontAsset LoadKoreanFont()
    {
        if (_koreanFont != null)
            return _koreanFont;

        _koreanFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Gowun Batang");
        if (_koreanFont == null)
            _koreanFont = Resources.Load<TMP_FontAsset>("Gowun Batang");
        if (_koreanFont == null)
            _koreanFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/NanumGothic SDF");
        if (_koreanFont == null)
            _koreanFont = Resources.Load<TMP_FontAsset>("NanumGothic SDF");
        return _koreanFont;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}

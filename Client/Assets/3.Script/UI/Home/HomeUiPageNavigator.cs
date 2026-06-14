using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeUiPageNavigator : MonoBehaviour
{
    private const string HomeOptionResourcePrefix = "UI/UI_Home_Option/";
    private const string MasterVolumeKey = "Options.MasterVolume";
    private const string InGameVolumeKey = "Options.InGameVolume";
    private const string SfxVolumeKey = "Options.SfxVolume";
    private const string FullscreenKey = "Options.Fullscreen";
    private const string LegacySoundMutedKey = "Options.SoundMuted";
    private const float DefaultMasterVolume = 0.85f;
    private const float DefaultInGameVolume = 0.80f;
    private const float DefaultSfxVolume = 0.75f;
    private static TMP_FontAsset _koreanFont;

    [SerializeField] private GameObject _homeRoot;
    [SerializeField] private GameObject _equipmentRoot;
    [SerializeField] private GameObject _inventoryRoot;
    [SerializeField] private GameObject _appearanceRoot;
    [SerializeField] private GameObject _mapRoot;
    [SerializeField] private GameObject _detailRoot;
    [SerializeField] private Button _equipmentButton;
    [SerializeField] private Button _inventoryButton;
    [SerializeField] private Button _appearanceButton;
    [SerializeField] private Button _mapButton;
    [SerializeField] private Button _equipmentBackButton;
    [SerializeField] private Button[] _homeButtons;
    [SerializeField] private HomeSceneCameraDirector _cameraDirector;
    [SerializeField] private TownHomeUiController _townHomeController;
    [SerializeField] private bool _forceCameraPresentation;
    [SerializeField] private bool _mapOnlyMode;
    [SerializeField] private float _forcedPresentationScreenLeftOffset = 1.65f;
    [SerializeField] private float _appearancePresentationScreenLeftOffset = 1.65f;

    private GameObject _optionsRoot;
    private TextMeshProUGUI _optionsStatusText;
    private Slider _masterVolumeSlider;
    private Slider _inGameVolumeSlider;
    private Slider _sfxVolumeSlider;
    private TextMeshProUGUI _masterVolumeValueText;
    private TextMeshProUGUI _inGameVolumeValueText;
    private TextMeshProUGUI _sfxVolumeValueText;
    private Image _fullscreenToggleImage;
    private Image _windowedToggleImage;
    private float _pendingMasterVolume = DefaultMasterVolume;
    private float _pendingInGameVolume = DefaultInGameVolume;
    private float _pendingSfxVolume = DefaultSfxVolume;
    private bool _pendingFullscreen;
    private bool _suppressOptionCallbacks;

    private HomePage _currentPage = HomePage.Home;
    private bool IsMapOnlyMode => _mapOnlyMode
                                  || string.Equals(gameObject.scene.name, SceneNames.WorldMap, StringComparison.OrdinalIgnoreCase);

    private enum HomePage
    {
        Home,
        Equipment,
        Inventory,
        Appearance,
        Map,
        Options
    }

    private void Awake()
    {
        ResolveCameraDirector();

        if (IsMapOnlyMode)
        {
            SetHomeButtonsActive(false);
            SetActive(_homeRoot, false);
            SetActive(_equipmentRoot, false);
            SetActive(_inventoryRoot, false);
            SetActive(_appearanceRoot, false);
            SetActive(_detailRoot, false);
            return;
        }

        Bind(_equipmentButton, ShowEquipment);
        Bind(_inventoryButton, ShowInventory);
        Bind(_appearanceButton, ShowAppearance);
        Bind(_mapButton, ShowOptions);
        Bind(_equipmentBackButton, ShowHome);
        RetitleMapButtonAsOptions();
        EnsureOptionsPage();

        if (_homeButtons != null)
        {
            foreach (var button in _homeButtons)
                Bind(button, ShowHome);
        }
    }

    private void Start()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            WorldMapEntryOverlay.Play(GetComponentInParent<Canvas>());
        }
        else
        {
            ShowHome();
        }
    }

    public void ShowHome()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

        Show(HomePage.Home);
    }

    public void ShowEquipment()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

        Show(HomePage.Equipment);
    }

    public void ShowInventory()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

        if (OpenTownInventoryWindowIfAvailable())
            return;

        Show(HomePage.Inventory);
    }

    public void ShowAppearance()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

        Show(HomePage.Appearance);
    }

    public void ShowMap()
    {
        Show(HomePage.Map);
    }

    public void ShowOptions()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

        EnsureOptionsPage();
        LoadPendingOptionsFromPrefs();
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
        Show(HomePage.Options);
    }

    public void SetForceCameraPresentation(bool force)
    {
        _forceCameraPresentation = force;
        UpdateCameraPresentation(_currentPage);
    }

    public void SetCameraDirector(HomeSceneCameraDirector cameraDirector)
    {
        if (cameraDirector == null)
            return;

        _cameraDirector = cameraDirector;
    }

    private void Show(HomePage page)
    {
        if (IsMapOnlyMode && page != HomePage.Map)
            page = HomePage.Map;

        _currentPage = page;
        SetActive(_homeRoot, page == HomePage.Home);
        SetActive(_equipmentRoot, page == HomePage.Equipment);
        SetActive(_inventoryRoot, page == HomePage.Inventory);
        SetActive(_appearanceRoot, page == HomePage.Appearance);
        SetActive(_mapRoot, page == HomePage.Map);
        SetActive(_optionsRoot, page == HomePage.Options);
        SetActive(_detailRoot, false);

        UpdateCameraPresentation(page);
    }

    private void UpdateCameraPresentation(HomePage page)
    {
        ResolveCameraDirector();
        if (_cameraDirector == null)
            return;

        var active = _forceCameraPresentation || page == HomePage.Appearance;
        if (active)
        {
            var offset = page == HomePage.Appearance
                ? _appearancePresentationScreenLeftOffset
                : _forcedPresentationScreenLeftOffset;
            _cameraDirector.SetModelScreenLeftOffset(offset);
        }

        _cameraDirector.SetAppearancePresentation(active);
    }

    private void ResolveCameraDirector()
    {
        if (_cameraDirector != null)
            return;

        _cameraDirector = GetComponent<HomeSceneCameraDirector>();
        if (_cameraDirector != null)
            return;

        _cameraDirector = GetComponentInParent<HomeSceneCameraDirector>(true);
        if (_cameraDirector != null)
            return;

        var directors = Resources.FindObjectsOfTypeAll<HomeSceneCameraDirector>();
        foreach (var director in directors)
        {
            if (director == null || !director.gameObject.scene.IsValid())
                continue;

            if (director.gameObject.scene == gameObject.scene)
            {
                _cameraDirector = director;
                return;
            }
        }
    }

    private bool OpenTownInventoryWindowIfAvailable()
    {
        ResolveTownHomeController();
        return _townHomeController != null && _townHomeController.OpenTownInventoryWindow();
    }

    private void RetitleMapButtonAsOptions()
    {
        if (_mapButton == null)
            return;

        var card = _mapButton.transform.parent;
        SetChildText(card, "Title", "OPTIONS");
        SetChildText(card, "Subtitle", "사운드와 화면\n설정.");
        SetChildText(card, "ActionLabel", "Open Options");
    }

    private void EnsureOptionsPage()
    {
        if (_optionsRoot != null)
            return;

        var parent = _mapRoot != null && _mapRoot.transform.parent != null
            ? _mapRoot.transform.parent
            : transform;

        _optionsRoot = new GameObject("UI_Home_Options", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _optionsRoot.transform.SetParent(parent, false);
        var rootRect = (RectTransform)_optionsRoot.transform;
        Stretch(rootRect);
        var rootImage = _optionsRoot.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.52f);
        rootImage.raycastTarget = true;

        var panel = CreatePanel(rootRect, "OptionsPanel", new Vector2(980f, 652f), new Color(0.60f, 0.43f, 0.24f, 1f));
        var panelSize = panel.sizeDelta;

        var titleColor = new Color(0.18f, 0.09f, 0.025f, 1f);
        var textColor = new Color(0.22f, 0.10f, 0.025f, 1f);
        var mutedColor = new Color(0.34f, 0.20f, 0.08f, 1f);
        var lightText = new Color(1f, 0.91f, 0.70f, 1f);

        var title = CreateText(panel, "Title", "옵션", 44f, TextAlignmentOptions.Center, titleColor, new Rect(320f, 48f, 340f, 58f), panelSize);
        title.fontStyle = FontStyles.Bold;
        CreateSeparator(panel, "TitleSeparatorLeft", new Rect(225f, 76f, 230f, 28f), panelSize);
        CreateSeparator(panel, "TitleSeparatorRight", new Rect(525f, 76f, 230f, 28f), panelSize);

        CreateSectionHeader(panel, "SoundHeader", "사운드", new Rect(130f, 126f, 165f, 46f), panelSize, lightText);
        CreateSeparator(panel, "SoundSeparator", new Rect(282f, 132f, 620f, 36f), panelSize);

        CreateText(panel, "MasterVolumeLabel", "마스터 음량", 24f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(185f, 196f, 180f, 40f), panelSize).fontStyle = FontStyles.Bold;
        _masterVolumeSlider = CreateVolumeSlider(panel, "MasterVolumeSlider", new Rect(380f, 196f, 410f, 44f), panelSize, HandleMasterVolumeChanged);
        _masterVolumeValueText = CreateText(panel, "MasterVolumeValue", "", 17f, TextAlignmentOptions.Center, mutedColor, new Rect(805f, 202f, 56f, 28f), panelSize);

        CreateText(panel, "InGameVolumeLabel", "인게임 음량", 24f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(185f, 260f, 180f, 40f), panelSize).fontStyle = FontStyles.Bold;
        _inGameVolumeSlider = CreateVolumeSlider(panel, "InGameVolumeSlider", new Rect(380f, 260f, 410f, 44f), panelSize, HandleInGameVolumeChanged);
        _inGameVolumeValueText = CreateText(panel, "InGameVolumeValue", "", 17f, TextAlignmentOptions.Center, mutedColor, new Rect(805f, 266f, 56f, 28f), panelSize);

        CreateText(panel, "SfxVolumeLabel", "효과음", 24f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(185f, 324f, 180f, 40f), panelSize).fontStyle = FontStyles.Bold;
        _sfxVolumeSlider = CreateVolumeSlider(panel, "SfxVolumeSlider", new Rect(380f, 324f, 410f, 44f), panelSize, HandleSfxVolumeChanged);
        _sfxVolumeValueText = CreateText(panel, "SfxVolumeValue", "", 17f, TextAlignmentOptions.Center, mutedColor, new Rect(805f, 330f, 56f, 28f), panelSize);

        CreateSectionHeader(panel, "ScreenHeader", "화면", new Rect(130f, 406f, 165f, 46f), panelSize, lightText);
        CreateSeparator(panel, "ScreenSeparator", new Rect(282f, 412f, 620f, 36f), panelSize);

        CreateScreenModeOption(panel, "FullscreenMode", "전체화면", new Rect(690f, 468f, 190f, 44f), panelSize, true);
        CreateScreenModeOption(panel, "WindowedMode", "창화면", new Rect(690f, 520f, 190f, 44f), panelSize, false);

        CreateSeparator(panel, "BottomSeparator", new Rect(130f, 542f, 750f, 24f), panelSize);
        _optionsStatusText = CreateText(panel, "Status", "", 15f, TextAlignmentOptions.Center, mutedColor, new Rect(285f, 552f, 410f, 28f), panelSize);

        CreateOptionButton(panel, "Button_ResetDefaults", "기본값", new Rect(118f, 578f, 175f, 55f), panelSize, HandleResetDefaultsClicked);
        CreateOptionButton(panel, "Button_Apply", "적용", new Rect(403f, 578f, 175f, 55f), panelSize, HandleApplyOptionsClicked);
        CreateOptionButton(panel, "Button_Close", "닫기", new Rect(688f, 578f, 175f, 55f), panelSize, HandleCloseOptionsClicked);

        _optionsRoot.SetActive(false);
        LoadPendingOptionsFromPrefs();
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
    }

    private void HandleMasterVolumeChanged(float value)
    {
        if (_suppressOptionCallbacks)
            return;

        _pendingMasterVolume = Mathf.Clamp01(value);
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
        SetOptionsStatus("마스터 음량 미리 적용 중");
    }

    private void HandleInGameVolumeChanged(float value)
    {
        if (_suppressOptionCallbacks)
            return;

        _pendingInGameVolume = Mathf.Clamp01(value);
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
        SetOptionsStatus("인게임 음량 미리 적용 중");
    }

    private void HandleSfxVolumeChanged(float value)
    {
        if (_suppressOptionCallbacks)
            return;

        _pendingSfxVolume = Mathf.Clamp01(value);
        RefreshOptionsUi();
        SetOptionsStatus("효과음 음량 미리 적용 중");
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

    private void HandleApplyOptionsClicked()
    {
        SavePendingOptions();
        ApplyRuntimeOptionPreview();
        ApplyScreenMode(_pendingFullscreen);
        SetOptionsStatus("설정이 적용되었습니다.");
    }

    private void HandleCloseOptionsClicked()
    {
        LoadPendingOptionsFromPrefs();
        ApplyRuntimeOptionPreview();
        ShowHome();
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
        _suppressOptionCallbacks = true;
        SetSliderValue(_masterVolumeSlider, _pendingMasterVolume);
        SetSliderValue(_inGameVolumeSlider, _pendingInGameVolume);
        SetSliderValue(_sfxVolumeSlider, _pendingSfxVolume);
        _suppressOptionCallbacks = false;

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
        if (_optionsStatusText != null)
            _optionsStatusText.text = message ?? "";
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

    private static void SetVolumeText(TextMeshProUGUI text, float value)
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

    private void ResolveTownHomeController()
    {
        if (_townHomeController != null && _townHomeController.gameObject.scene.IsValid())
            return;

        _townHomeController = GetComponentInParent<TownHomeUiController>(true);
        if (_townHomeController != null)
            return;

        var controllers = Resources.FindObjectsOfTypeAll<TownHomeUiController>();
        foreach (var controller in controllers)
        {
            if (controller == null || !controller.gameObject.scene.IsValid())
                continue;

            if (controller.gameObject.scene == gameObject.scene)
            {
                _townHomeController = controller;
                return;
            }
        }
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static void SetChildText(Transform root, string childName, string value)
    {
        if (root == null)
            return;

        var labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var label in labels)
        {
            if (label != null && label.gameObject.name == childName)
            {
                ApplyPreferredFont(label);
                label.text = value;
                return;
            }
        }
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

    private static void CreateSectionHeader(
        RectTransform parent,
        string name,
        string label,
        Rect rect,
        Vector2 sourceSize,
        Color textColor)
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

    private static Slider CreateVolumeSlider(
        RectTransform parent,
        string name,
        Rect rect,
        Vector2 sourceSize,
        UnityEngine.Events.UnityAction<float> action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Slider));
        go.transform.SetParent(parent, false);
        var root = (RectTransform)go.transform;
        SetRectFromTopLeft(root, rect, sourceSize);

        var sliderSize = new Vector2(rect.width, rect.height);
        const float trackLeft = 22f;
        const float trackTop = 10f;
        const float trackHeight = 24f;
        var trackWidth = rect.width - 68f;

        var background = CreateOptionImage(root, "Background", "Volume_Bar_Back", new Rect(trackLeft, trackTop, trackWidth, trackHeight), sliderSize);
        background.raycastTarget = false;

        var fillArea = CreateRect(root, "Fill Area");
        SetRectFromTopLeft(fillArea, new Rect(trackLeft + 5f, trackTop + 3f, trackWidth - 10f, trackHeight - 6f), sliderSize);

        var fill = CreateOptionImage(fillArea, "Fill", "Volume_Bar_Fill", new Rect(0f, 0f, trackWidth - 10f, trackHeight - 6f), new Vector2(trackWidth - 10f, trackHeight - 6f));
        Stretch(fill.rectTransform);
        fill.raycastTarget = false;

        var handleArea = CreateRect(root, "Handle Slide Area");
        SetRectFromTopLeft(handleArea, new Rect(trackLeft, 0f, trackWidth, rect.height), sliderSize);

        var handle = CreateOptionImage(handleArea, "Handle", "Volume_Button", new Rect(0f, 0f, 34f, 46f), new Vector2(trackWidth, rect.height));
        handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(34f, 46f);
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
        slider.onValueChanged.AddListener(action);
        return slider;
    }

    private void CreateScreenModeOption(RectTransform parent, string name, string label, Rect rect, Vector2 sourceSize, bool fullscreen)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var root = (RectTransform)go.transform;
        SetRectFromTopLeft(root, rect, sourceSize);
        var hitArea = go.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0f);
        hitArea.raycastTarget = true;

        var optionSize = new Vector2(rect.width, rect.height);
        var icon = CreateOptionImage(root, "Icon", "Circle_Button_Deactive", new Rect(0f, 6f, 32f, 32f), optionSize);
        icon.raycastTarget = true;
        if (fullscreen)
            _fullscreenToggleImage = icon;
        else
            _windowedToggleImage = icon;

        var text = CreateText(root, "Label", label, 19f, TextAlignmentOptions.MidlineLeft, new Color(0.22f, 0.10f, 0.025f, 1f), new Rect(44f, 0f, rect.width - 44f, rect.height), optionSize);
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
        button.onClick.AddListener(() => HandleScreenModeClicked(fullscreen));
    }

    private static Button CreateOptionButton(
        RectTransform parent,
        string name,
        string label,
        Rect rectValue,
        Vector2 sourceSize,
        UnityEngine.Events.UnityAction action)
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
        Bind(button, action);

        var text = CreateText(buttonRect, "Label", label, 24f, TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.72f, 1f), new Rect(0f, 0f, buttonRect.sizeDelta.x, buttonRect.sizeDelta.y), buttonRect.sizeDelta);
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private static TextMeshProUGUI CreateText(
        RectTransform parent,
        string name,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Color color,
        Rect rectValue,
        Vector2 sourceSize)
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

    private void SetHomeButtonsActive(bool active)
    {
        if (_homeButtons == null)
            return;

        foreach (var button in _homeButtons)
        {
            if (button != null)
                SetActive(button.gameObject, active);
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeUiPageNavigator : MonoBehaviour
{
    private const string HomeOptionResourcePrefix = "UI/UI_Home_Option/";
    private const string SharedOptionsPanelResourcePath = "UI/Options/PF_GameOptionsPanel";
    private const string MasterVolumeKey = "Options.MasterVolume";
    private const string InGameVolumeKey = "Options.InGameVolume";
    private const string SfxVolumeKey = "Options.SfxVolume";
    private const string FullscreenKey = "Options.Fullscreen";
    private const string LegacySoundMutedKey = "Options.SoundMuted";
    private const string InventoryOpenActionText = "Open Inventory";
    private const string InventoryLockedActionText = "LOCKED";
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
    [SerializeField] private bool _inventoryLocked = true;
    [SerializeField] private GameObject _optionsRoot;
    [SerializeField] private RectTransform _optionsPanel;
    [SerializeField] private GameOptionsPanel _sharedOptionsPanel;
    [SerializeField] private float _forcedPresentationScreenLeftOffset = 1.65f;
    [SerializeField] private float _appearancePresentationScreenLeftOffset = 1.65f;

    private TextMeshProUGUI _optionsStatusText;
    private Slider _masterVolumeSlider;
    private Slider _inGameVolumeSlider;
    private Slider _sfxVolumeSlider;
    private TextMeshProUGUI _masterVolumeValueText;
    private TextMeshProUGUI _inGameVolumeValueText;
    private TextMeshProUGUI _sfxVolumeValueText;
    private Image _fullscreenToggleImage;
    private Image _windowedToggleImage;
    private Image _inventoryLockedWash;
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
        ApplyInventoryLockState();
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

        if (_inventoryLocked)
            return;

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
        if (_sharedOptionsPanel != null)
        {
            Show(HomePage.Options);
            _sharedOptionsPanel.Open();
            return;
        }

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

        var leavingOptions = _currentPage == HomePage.Options && page != HomePage.Options;
        _currentPage = page;
        if (leavingOptions && _sharedOptionsPanel != null)
            _sharedOptionsPanel.DiscardAndHide();

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
        SetChildText(card, "Subtitle", string.Empty);
        SetChildText(card, "ActionLabel", "Open Options");
    }

    private void ApplyInventoryLockState()
    {
        if (_inventoryButton == null)
            return;

        _inventoryButton.interactable = !_inventoryLocked;
        var card = _inventoryButton.transform.parent;
        SetChildText(card, "Subtitle", string.Empty);
        SetChildText(card, "ActionLabel", _inventoryLocked ? InventoryLockedActionText : InventoryOpenActionText);
        SetChildText(card, "ActionArrow", _inventoryLocked ? string.Empty : "\u203A");
        ApplyInventoryLockedVisual(card, _inventoryLocked);
    }

    private void EnsureOptionsPage()
    {
        if (TryEnsureSharedOptionsPage())
            return;

        if (_optionsRoot != null && _masterVolumeSlider != null)
            return;

        var parent = _mapRoot != null && _mapRoot.transform.parent != null
            ? _mapRoot.transform.parent
            : transform;

        bool createdRoot = false;
        if (_optionsRoot == null)
            _optionsRoot = parent.Find("UI_Home_Options")?.gameObject;

        if (_optionsRoot == null)
        {
            _optionsRoot = new GameObject("UI_Home_Options", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _optionsRoot.transform.SetParent(parent, false);
            createdRoot = true;
        }

        var rootRect = (RectTransform)_optionsRoot.transform;
        if (createdRoot)
            Stretch(rootRect);

        var rootImage = _optionsRoot.GetComponent<Image>();
        if (rootImage == null && createdRoot)
            rootImage = _optionsRoot.AddComponent<Image>();
        if (rootImage != null)
        {
            rootImage.color = new Color(0f, 0f, 0f, 0.52f);
            rootImage.raycastTarget = true;
        }

        if (_optionsPanel == null)
            _optionsPanel = FindChildRect(_optionsRoot.transform, "OptionsPanel");

        if (_optionsPanel == null)
            _optionsPanel = CreatePanel(rootRect, "OptionsPanel", new Vector2(940f, 620f), new Color(0.60f, 0.43f, 0.24f, 1f));

        var panel = _optionsPanel;
        if (TryCacheExistingOptionsControls(panel))
        {
            _optionsRoot.SetActive(false);
            LoadPendingOptionsFromPrefs();
            RefreshOptionsUi();
            ApplyRuntimeOptionPreview();
            return;
        }

        var panelSize = ResolveOptionsPanelSize(panel);

        var titleColor = new Color(0.18f, 0.09f, 0.025f, 1f);
        var textColor = new Color(0.22f, 0.10f, 0.025f, 1f);
        var mutedColor = new Color(0.34f, 0.20f, 0.08f, 1f);
        var lightText = new Color(1f, 0.91f, 0.70f, 1f);

        var title = CreateText(panel, "Title", "옵션", 38f, TextAlignmentOptions.Center, titleColor, new Rect(330f, 72f, 280f, 44f), panelSize);
        title.fontStyle = FontStyles.Bold;
        CreateSeparator(panel, "TitleSeparatorLeft", new Rect(118f, 84f, 200f, 24f), panelSize);
        CreateSeparator(panel, "TitleSeparatorRight", new Rect(622f, 84f, 200f, 24f), panelSize);

        CreateSectionHeader(panel, "SoundHeader", "사운드", new Rect(118f, 140f, 158f, 42f), panelSize, lightText);
        CreateSeparator(panel, "SoundSeparator", new Rect(266f, 147f, 590f, 28f), panelSize);

        CreateText(panel, "MasterVolumeLabel", "마스터 음량", 21f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(164f, 204f, 180f, 34f), panelSize).fontStyle = FontStyles.Bold;
        _masterVolumeSlider = CreateVolumeSlider(panel, "MasterVolumeSlider", new Rect(360f, 202f, 390f, 38f), panelSize, HandleMasterVolumeChanged);
        _masterVolumeValueText = CreateText(panel, "MasterVolumeValue", "", 15f, TextAlignmentOptions.Center, mutedColor, new Rect(768f, 208f, 52f, 24f), panelSize);

        CreateText(panel, "InGameVolumeLabel", "인게임 음량", 21f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(164f, 262f, 180f, 34f), panelSize).fontStyle = FontStyles.Bold;
        _inGameVolumeSlider = CreateVolumeSlider(panel, "InGameVolumeSlider", new Rect(360f, 260f, 390f, 38f), panelSize, HandleInGameVolumeChanged);
        _inGameVolumeValueText = CreateText(panel, "InGameVolumeValue", "", 15f, TextAlignmentOptions.Center, mutedColor, new Rect(768f, 266f, 52f, 24f), panelSize);

        CreateText(panel, "SfxVolumeLabel", "효과음", 21f, TextAlignmentOptions.MidlineLeft, textColor, new Rect(164f, 320f, 180f, 34f), panelSize).fontStyle = FontStyles.Bold;
        _sfxVolumeSlider = CreateVolumeSlider(panel, "SfxVolumeSlider", new Rect(360f, 318f, 390f, 38f), panelSize, HandleSfxVolumeChanged);
        _sfxVolumeValueText = CreateText(panel, "SfxVolumeValue", "", 15f, TextAlignmentOptions.Center, mutedColor, new Rect(768f, 324f, 52f, 24f), panelSize);

        CreateSectionHeader(panel, "ScreenHeader", "화면", new Rect(118f, 392f, 158f, 42f), panelSize, lightText);
        CreateSeparator(panel, "ScreenSeparator", new Rect(266f, 399f, 590f, 28f), panelSize);

        CreateScreenModeOption(panel, "FullscreenMode", "전체화면", new Rect(642f, 452f, 178f, 36f), panelSize, true);
        CreateScreenModeOption(panel, "WindowedMode", "창화면", new Rect(642f, 496f, 178f, 36f), panelSize, false);

        CreateSeparator(panel, "BottomSeparator", new Rect(118f, 536f, 704f, 20f), panelSize);
        _optionsStatusText = CreateText(panel, "Status", "", 13f, TextAlignmentOptions.Center, mutedColor, new Rect(286f, 534f, 368f, 20f), panelSize);

        CreateOptionButton(panel, "Button_ResetDefaults", "기본값", new Rect(118f, 562f, 154f, 48f), panelSize, HandleResetDefaultsClicked);
        CreateOptionButton(panel, "Button_ResetGuest", "Guest 초기화", new Rect(302f, 562f, 154f, 48f), panelSize, HandleResetGuestClicked);
        CreateOptionButton(panel, "Button_Apply", "적용", new Rect(486f, 562f, 154f, 48f), panelSize, HandleApplyOptionsClicked);
        CreateOptionButton(panel, "Button_Close", "닫기", new Rect(670f, 562f, 154f, 48f), panelSize, HandleCloseOptionsClicked);

        _optionsRoot.SetActive(false);
        LoadPendingOptionsFromPrefs();
        RefreshOptionsUi();
        ApplyRuntimeOptionPreview();
    }

    private bool TryEnsureSharedOptionsPage()
    {
        var parent = _mapRoot != null && _mapRoot.transform.parent != null
            ? _mapRoot.transform.parent
            : transform;

        if (_sharedOptionsPanel == null && _optionsRoot != null)
            _sharedOptionsPanel = _optionsRoot.GetComponentInChildren<GameOptionsPanel>(true);

        if (_sharedOptionsPanel == null)
            _sharedOptionsPanel = GetComponentInChildren<GameOptionsPanel>(true);

        if (_sharedOptionsPanel == null)
        {
            var prefab = Resources.Load<GameOptionsPanel>(SharedOptionsPanelResourcePath);
            if (prefab == null)
                return false;

            _sharedOptionsPanel = Instantiate(prefab, parent, false);
            _sharedOptionsPanel.gameObject.name = "UI_Home_Options";
        }
        else if (_sharedOptionsPanel.transform.parent != parent)
        {
            _sharedOptionsPanel.transform.SetParent(parent, false);
        }

        _optionsRoot = _sharedOptionsPanel.gameObject;
        _optionsPanel = _sharedOptionsPanel.PanelRect;
        if (_optionsRoot.transform is RectTransform rootRect)
            Stretch(rootRect);

        _sharedOptionsPanel.CloseRequested -= HandleSharedOptionsCloseRequested;
        _sharedOptionsPanel.CloseRequested += HandleSharedOptionsCloseRequested;
        _sharedOptionsPanel.HideImmediate();
        return true;
    }

    private void HandleSharedOptionsCloseRequested()
    {
        ShowHome();
    }

    private bool TryCacheExistingOptionsControls(RectTransform panel)
    {
        if (panel == null)
            return false;

        var masterSlider = FindChildComponent<Slider>(panel, "MasterVolumeSlider");
        var inGameSlider = FindChildComponent<Slider>(panel, "InGameVolumeSlider");
        var sfxSlider = FindChildComponent<Slider>(panel, "SfxVolumeSlider");
        if (masterSlider == null || inGameSlider == null || sfxSlider == null)
            return false;

        _masterVolumeSlider = masterSlider;
        _inGameVolumeSlider = inGameSlider;
        _sfxVolumeSlider = sfxSlider;
        _masterVolumeValueText = FindChildComponent<TextMeshProUGUI>(panel, "MasterVolumeValue");
        _inGameVolumeValueText = FindChildComponent<TextMeshProUGUI>(panel, "InGameVolumeValue");
        _sfxVolumeValueText = FindChildComponent<TextMeshProUGUI>(panel, "SfxVolumeValue");
        _optionsStatusText = FindChildComponent<TextMeshProUGUI>(panel, "Status");

        _fullscreenToggleImage = FindChildComponent<Image>(FindChild(panel, "FullscreenMode"), "Icon");
        _windowedToggleImage = FindChildComponent<Image>(FindChild(panel, "WindowedMode"), "Icon");

        BindSlider(_masterVolumeSlider, HandleMasterVolumeChanged);
        BindSlider(_inGameVolumeSlider, HandleInGameVolumeChanged);
        BindSlider(_sfxVolumeSlider, HandleSfxVolumeChanged);
        Bind(FindChildComponent<Button>(panel, "FullscreenMode"), () => HandleScreenModeClicked(true));
        Bind(FindChildComponent<Button>(panel, "WindowedMode"), () => HandleScreenModeClicked(false));
        Bind(FindChildComponent<Button>(panel, "Button_ResetDefaults"), HandleResetDefaultsClicked);
        Bind(FindChildComponent<Button>(panel, "Button_ResetGuest"), HandleResetGuestClicked);
        Bind(FindChildComponent<Button>(panel, "Button_Apply"), HandleApplyOptionsClicked);
        Bind(FindChildComponent<Button>(panel, "Button_Close"), HandleCloseOptionsClicked);
        return true;
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

    private void HandleResetGuestClicked()
    {
        var newDeviceId = GameOptionsPanel.ResetGuestIdentity();
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

    private static string ShortDeviceId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return "-";

        return deviceId.Length <= 8 ? deviceId : deviceId.Substring(0, 8);
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

    private static void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null || action == null)
            return;

        slider.onValueChanged.RemoveListener(action);
        slider.onValueChanged.AddListener(action);
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

    private void ApplyInventoryLockedVisual(Transform card, bool locked)
    {
        if (_inventoryButton != null)
        {
            var colors = _inventoryButton.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.70f);
            colors.fadeDuration = 0.08f;
            _inventoryButton.colors = colors;
        }

        if (!(card is RectTransform))
            return;

        if (_inventoryLockedWash == null || _inventoryLockedWash.transform.parent != card)
        {
            _inventoryLockedWash = FindChildComponent<Image>(card, "InventoryLockedWash");
            if (_inventoryLockedWash == null)
            {
                var go = new GameObject("InventoryLockedWash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                _inventoryLockedWash = go.GetComponent<Image>();
                Stretch((RectTransform)go.transform);
            }
        }

        _inventoryLockedWash.color = new Color(1f, 1f, 1f, 0.32f);
        _inventoryLockedWash.raycastTarget = false;
        _inventoryLockedWash.gameObject.SetActive(locked);
        if (locked)
            _inventoryLockedWash.transform.SetAsLastSibling();
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

    private static Vector2 ResolveOptionsPanelSize(RectTransform panel)
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

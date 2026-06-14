using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeUiPageNavigator : MonoBehaviour
{
    private const string OptionsPanelBackgroundResource = "UI/UI_Options/OptionsPanel_Background";
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
    private TextMeshProUGUI _soundButtonLabel;
    private TextMeshProUGUI _gameplayButtonLabel;
    private float _exitConfirmUntil;

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
        SetChildText(card, "Subtitle", "소리, 게임 플레이,\n나가기 설정.");
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

        var panel = CreatePanel(rootRect, "OptionsPanel", new Vector2(920f, 520f), new Color(0.08f, 0.13f, 0.14f, 0.94f));
        CreateText(panel, "Title", "OPTIONS", 34f, TextAlignmentOptions.Center, new Color(0.20f, 0.16f, 0.10f, 1f), new Vector2(0f, 194f), new Vector2(520f, 52f));
        CreateText(panel, "Subtitle", "소리, 게임 플레이, 나가기", 18f, TextAlignmentOptions.Center, new Color(0.32f, 0.27f, 0.18f, 1f), new Vector2(0f, 150f), new Vector2(520f, 36f));

        _soundButtonLabel = CreateOptionButton(panel, "Button_Sound", "소리", new Vector2(0f, 76f), HandleSoundClicked);
        _gameplayButtonLabel = CreateOptionButton(panel, "Button_Gameplay", "게임 플레이", new Vector2(0f, 2f), HandleGameplayClicked);
        CreateOptionButton(panel, "Button_Exit", "나가기", new Vector2(0f, -72f), HandleExitClicked);
        CreateOptionButton(panel, "Button_Back", "뒤로", new Vector2(0f, -172f), ShowHome, new Vector2(220f, 50f));

        _optionsStatusText = CreateText(panel, "Status", "", 17f, TextAlignmentOptions.Center, new Color(0.28f, 0.23f, 0.15f, 1f), new Vector2(0f, -126f), new Vector2(600f, 38f));
        _optionsRoot.SetActive(false);
        UpdateOptionLabels();
    }

    private void HandleSoundClicked()
    {
        var muted = PlayerPrefs.GetInt("Options.SoundMuted", 0) == 1;
        muted = !muted;
        PlayerPrefs.SetInt("Options.SoundMuted", muted ? 1 : 0);
        PlayerPrefs.Save();
        AudioListener.volume = muted ? 0f : 1f;
        SetOptionsStatus(muted ? "소리를 껐습니다." : "소리를 켰습니다.");
        UpdateOptionLabels();
    }

    private void HandleGameplayClicked()
    {
        var enabled = PlayerPrefs.GetInt("Options.GameplayHints", 1) == 1;
        enabled = !enabled;
        PlayerPrefs.SetInt("Options.GameplayHints", enabled ? 1 : 0);
        PlayerPrefs.Save();
        SetOptionsStatus(enabled ? "게임 플레이 가이드를 켰습니다." : "게임 플레이 가이드를 껐습니다.");
        UpdateOptionLabels();
    }

    private void HandleExitClicked()
    {
        if (Time.unscaledTime <= _exitConfirmUntil)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
            return;
        }

        _exitConfirmUntil = Time.unscaledTime + 3f;
        SetOptionsStatus("한 번 더 누르면 게임을 종료합니다.");
    }

    private void UpdateOptionLabels()
    {
        if (_soundButtonLabel != null)
        {
            var muted = PlayerPrefs.GetInt("Options.SoundMuted", 0) == 1;
            _soundButtonLabel.text = muted ? "소리: 꺼짐" : "소리: 켜짐";
            AudioListener.volume = muted ? 0f : 1f;
        }

        if (_gameplayButtonLabel != null)
        {
            var enabled = PlayerPrefs.GetInt("Options.GameplayHints", 1) == 1;
            _gameplayButtonLabel.text = enabled ? "게임 플레이: 가이드 켜짐" : "게임 플레이: 가이드 꺼짐";
        }
    }

    private void SetOptionsStatus(string message)
    {
        if (_optionsStatusText != null)
            _optionsStatusText.text = message;
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
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        var texture = Resources.Load<Texture2D>(OptionsPanelBackgroundResource);
        if (texture != null)
        {
            var rawImage = go.AddComponent<RawImage>();
            rawImage.texture = texture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = true;
        }
        else
        {
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
        }

        return rect;
    }

    private static TextMeshProUGUI CreateOptionButton(
        RectTransform parent,
        string name,
        string label,
        Vector2 position,
        UnityEngine.Events.UnityAction action,
        Vector2? sizeOverride = null)
    {
        var size = sizeOverride ?? new Vector2(520f, 58f);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.10f, 0.34f, 0.36f, 0.96f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.18f, 0.44f, 0.45f, 1f);
        colors.pressedColor = new Color(0.06f, 0.24f, 0.26f, 1f);
        colors.selectedColor = new Color(0.16f, 0.40f, 0.42f, 1f);
        colors.disabledColor = new Color(0.42f, 0.36f, 0.26f, 0.88f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Bind(button, action);

        return CreateText(rect, "Label", label, 20f, TextAlignmentOptions.Center, new Color(0.98f, 0.90f, 0.68f, 1f), Vector2.zero, size);
    }

    private static TextMeshProUGUI CreateText(
        RectTransform parent,
        string name,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Color color,
        Vector2 position,
        Vector2 rectSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = rectSize;
        rect.anchoredPosition = position;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        ApplyPreferredFont(text);
        return text;
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

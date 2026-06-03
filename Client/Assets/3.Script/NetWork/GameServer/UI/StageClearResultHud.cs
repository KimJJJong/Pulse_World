using GameServer.InGame.Director.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class StageClearResultHud : MonoBehaviour
{
    private const string ResourceRoot = "UI/UI_StageClear/";
    private const int SortingOrder = 32000;

    private static readonly Color Cyan = new Color(0.28f, 0.96f, 1f, 1f);
    private static readonly Color CyanDim = new Color(0.18f, 0.68f, 0.76f, 0.68f);
    private static readonly Color Gold = new Color(0.95f, 0.69f, 0.29f, 1f);
    private static readonly Color TextMain = new Color(0.90f, 0.98f, 1f, 1f);
    private static readonly Color TextSoft = new Color(0.78f, 0.89f, 0.92f, 1f);
    private static readonly Color PanelTint = new Color(0.025f, 0.075f, 0.095f, 0.86f);

    private static StageClearResultHud _instance;

    private RectTransform _root;
    private RectTransform _panel;
    private Canvas _canvas;
    private CanvasGroup _group;
    private TextMeshProUGUI _clearTimeValue;
    private TextMeshProUGUI _syncValue;
    private TextMeshProUGUI _comboValue;
    private TextMeshProUGUI _missValue;
    private TextMeshProUGUI _nextAreaValue;
    private TextMeshProUGUI _levelValue;
    private TextMeshProUGUI _dangerValue;
    private TextMeshProUGUI _statusText;
    private Button _returnButton;
    private Button _continueButton;
    private bool _submissionInFlight;

    public static bool TryHandleWarn(int code, string payload)
    {
        if (code != StageSignalCodec.StageClearWarnCode
            || !StageSignalCodec.TryDecodeStageClear(payload, out var data))
        {
            return false;
        }

        Show(data);
        return true;
    }

    public static void Show(StageClearResultData data)
    {
        EnsureInstance().ShowInternal(data ?? new StageClearResultData());
    }

    public static void Hide()
    {
        if (_instance == null)
            return;

        _instance.HideInternal();
    }

#if UNITY_EDITOR
    public static void DestroyEditorPreview()
    {
        if (_instance == null)
            return;

        DestroyImmediate(_instance.gameObject);
        _instance = null;
    }
#endif

    private static StageClearResultHud EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var go = new GameObject(nameof(StageClearResultHud));
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        _instance = go.AddComponent<StageClearResultHud>();
        _instance.BuildUi();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        _canvas = gameObject.AddComponent<Canvas>();
        ConfigureTopCanvas(_canvas);

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        _root = CreateRect("Root", transform);
        Stretch(_root);
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        Image dim = CreateImage("Dim", _root, null, new Color(0f, 0.012f, 0.02f, 0.62f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        _panel = CreateRect("StageClearPanel", _root);
        Anchor(_panel, new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(1480f, 820f), new Vector2(0.5f, 0.5f));
        Image panelImage = _panel.gameObject.AddComponent<Image>();
        panelImage.sprite = Load("Panel_Frame");
        panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.color = panelImage.sprite != null ? Color.white : PanelTint;
        panelImage.raycastTarget = false;

        BuildHeader(_panel);
        BuildPerformanceSummary(_panel);
        BuildRewards(_panel);
        BuildPurificationReport(_panel);
        BuildFooter(_panel);
        BuildButtons(_root);

        gameObject.SetActive(false);
    }

    private void BuildHeader(RectTransform parent)
    {
        Image emblem = CreateImage("CrystalEmblem", parent, Load("Icon_Emblem"), Color.white);
        Anchor(emblem.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -94f), new Vector2(155f, 170f), new Vector2(0.5f, 0.5f));

        TextMeshProUGUI title = CreateText("Title", parent, "STAGE CLEAR", 76f, FontStyles.Bold, TextAlignmentOptions.Center, TextMain);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(760f, 96f), new Vector2(0.5f, 0.5f));
        title.enableAutoSizing = true;
        title.fontSizeMax = 76f;
        title.fontSizeMin = 48f;

        TextMeshProUGUI subtitle = CreateText("Subtitle", parent, "Purification Complete - Deepwood Gate Stabilized", 25f, FontStyles.Normal, TextAlignmentOptions.Center, TextSoft);
        Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(760f, 42f), new Vector2(0.5f, 0.5f));
        subtitle.name = "Subtitle";

        Image rank = CreateImage("RankBadge", parent, Load("Rank_Badge"), Color.white);
        Anchor(rank.rectTransform, new Vector2(1f, 1f), new Vector2(-180f, -98f), new Vector2(170f, 170f), new Vector2(0.5f, 0.5f));
        TextMeshProUGUI rankText = CreateText("RankText", rank.rectTransform, "S", 74f, FontStyles.Bold, TextAlignmentOptions.Center, TextMain);
        Stretch(rankText.rectTransform);

        CreateLine(parent, "HeaderLine", new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1200f, 8f));
    }

    private void BuildPerformanceSummary(RectTransform parent)
    {
        CreateSectionTitle(parent, "1. PERFORMANCE SUMMARY", new Vector2(150f, -240f), new Vector2(1220f, 38f));
        float y = -318f;
        BuildStat(parent, "ClearTime", "Icon_Hourglass", "Clear Time", out _clearTimeValue, new Vector2(275f, y));
        BuildStat(parent, "RhythmSync", "Icon_Rhythm", "Rhythm Sync", out _syncValue, new Vector2(540f, y));
        BuildStat(parent, "MaxCombo", "Icon_Combo", "Max Combo", out _comboValue, new Vector2(805f, y));
        BuildStat(parent, "Misses", "Icon_Miss", "Misses", out _missValue, new Vector2(1070f, y));
        CreateLine(parent, "SummaryDivider", new Vector2(0.5f, 1f), new Vector2(0f, -392f), new Vector2(1220f, 5f));
    }

    private void BuildRewards(RectTransform parent)
    {
        CreateSectionTitle(parent, "2. REWARDS", new Vector2(150f, -440f), new Vector2(620f, 38f));
        RectTransform area = CreateRect("RewardsEmptyArea", parent);
        Anchor(area, new Vector2(0f, 1f), new Vector2(455f, -566f), new Vector2(640f, 185f), new Vector2(0.5f, 0.5f));

        for (int i = 0; i < 5; i++)
        {
            Image slot = CreateImage($"RewardSlot_{i}", area, Load("Hex_Slot"), new Color(1f, 1f, 1f, 0.28f));
            Anchor(slot.rectTransform, new Vector2(0f, 0.5f), new Vector2(75f + i * 128f, 0f), new Vector2(92f, 98f), new Vector2(0.5f, 0.5f));
        }
    }

    private void BuildPurificationReport(RectTransform parent)
    {
        CreateSectionTitle(parent, "3. PURIFICATION REPORT", new Vector2(880f, -440f), new Vector2(430f, 38f));
        RectTransform report = CreateRect("Report", parent);
        Anchor(report, new Vector2(1f, 1f), new Vector2(-320f, -565f), new Vector2(520f, 208f), new Vector2(0.5f, 0.5f));

        string[] labels =
        {
            "Echo Nodes Restored:",
            "Corruption Residue:",
            "Gate Stability:",
            "Hidden Echo Found:",
            "First Clear Bonus:",
            "Route Unlocked:"
        };

        string[] values =
        {
            "4 / 4",
            "Low",
            "92%",
            "1",
            "Active",
            "Deepwood Gate"
        };

        for (int i = 0; i < labels.Length; i++)
            BuildReportRow(report, labels[i], values[i], i);
    }

    private void BuildFooter(RectTransform parent)
    {
        CreateLine(parent, "FooterDivider", new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(1220f, 5f));

        _nextAreaValue = BuildFooterItem(parent, "FooterNext", "Icon_NextGate", "Next Area:", "Deepwood Gate", new Vector2(270f, 82f), 310f);
        _levelValue = BuildFooterItem(parent, "FooterLevel", "Icon_Level", "Recommended Level:", "12", new Vector2(700f, 82f), 300f);
        _dangerValue = BuildFooterItem(parent, "FooterDanger", "Icon_Rhythm", "Danger Rhythm:", "Normal", new Vector2(1080f, 82f), 300f);
    }

    private void BuildButtons(RectTransform parent)
    {
        _returnButton = BuildButton(parent, "Button_ReturnToTown", "Icon_Home", "Return to Town", Load("Button_Return"), Gold, new Vector2(-340f, 36f));
        _continueButton = BuildButton(parent, "Button_Continue", "Icon_Arrow", "Continue to Next Map", Load("Button_Continue"), Cyan, new Vector2(360f, 36f));

        _returnButton.onClick.AddListener(OnReturnToTownClicked);
        _continueButton.onClick.AddListener(OnContinueClicked);

        _statusText = CreateText("Status", parent, string.Empty, 18f, FontStyles.Normal, TextAlignmentOptions.Center, TextSoft);
        Anchor(_statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(760f, 26f), new Vector2(0.5f, 0f));
    }

    private void ShowInternal(StageClearResultData data)
    {
        if (_root == null)
            BuildUi();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ConfigureTopCanvas(_canvas);
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = true;
        _submissionInFlight = false;

        _clearTimeValue.text = FormatTime(data.ClearTimeMs);
        _syncValue.text = $"{Mathf.Clamp(data.RhythmSyncPercent, 0, 100)}%";
        _comboValue.text = Mathf.Max(0, data.MaxCombo).ToString();
        _missValue.text = Mathf.Max(0, data.Misses).ToString();
        _nextAreaValue.text = string.IsNullOrWhiteSpace(data.NextArea) ? "Deepwood Gate" : data.NextArea;
        _levelValue.text = Mathf.Max(0, data.RecommendedLevel).ToString();
        _dangerValue.text = string.IsNullOrWhiteSpace(data.DangerRhythm) ? "Normal" : data.DangerRhythm;

        SetButtonsInteractable(true);
        _statusText.text = CanSubmitResult() ? string.Empty : "Waiting for host confirmation";
    }

    private void HideInternal()
    {
        _submissionInFlight = false;

        if (_group != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        gameObject.SetActive(false);
    }

    private static bool CanSubmitResult()
    {
        var bridge = P2PRelayClientBridge.HasInstance ? P2PRelayClientBridge.Instance : null;
        return bridge == null || !bridge.IsRelayMode || bridge.IsHostLocal;
    }

    private void OnReturnToTownClicked()
    {
        if (_submissionInFlight)
            return;

        if (TrySubmitHostResult("StageClearUI:ReturnToTown", "Returning to town..."))
            return;

        _statusText.text = "Returning to town...";
        HideInternal();
        ClientFlow.Instance?.ReturnToTown();
    }

    private void OnContinueClicked()
    {
        if (_submissionInFlight)
            return;

        if (TrySubmitHostResult("StageClearUI:Continue", "Continuing to next map..."))
            return;

        if (IsRelayGuest())
        {
            _statusText.text = "Waiting for host confirmation";
            return;
        }

        _statusText.text = "Continuing to next map...";
        HideInternal();
        ClientFlow.Instance?.ReturnToTown();
    }

    private bool TrySubmitHostResult(string source, string status)
    {
        if (!CanSubmitResult())
        {
            _statusText.text = "Waiting for host confirmation";
            return false;
        }

        if (P2PHostController.HasInstance)
        {
            _submissionInFlight = true;
            SetButtonsInteractable(false);
            _statusText.text = status;
            P2PHostController.Instance.SubmitStageClearResult(source);
            return true;
        }

        return false;
    }

    private static bool IsRelayGuest()
    {
        var bridge = P2PRelayClientBridge.HasInstance ? P2PRelayClientBridge.Instance : null;
        return bridge != null && bridge.IsRelayMode && !bridge.IsHostLocal;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (_returnButton != null)
            _returnButton.interactable = interactable;
        if (_continueButton != null)
            _continueButton.interactable = interactable;
    }

    private static void BuildStat(RectTransform parent, string name, string iconName, string label, out TextMeshProUGUI valueText, Vector2 anchoredPosition)
    {
        RectTransform root = CreateRect(name, parent);
        Anchor(root, new Vector2(0f, 1f), anchoredPosition, new Vector2(236f, 102f), new Vector2(0.5f, 0.5f));

        Image icon = CreateImage("Icon", root, Load(iconName), Color.white);
        Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(78f, 78f), new Vector2(0.5f, 0.5f));

        TextMeshProUGUI labelText = CreateText("Label", root, label, 20f, FontStyles.Normal, TextAlignmentOptions.Left, TextSoft);
        Anchor(labelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(98f, 20f), new Vector2(150f, 28f), new Vector2(0f, 0.5f));

        valueText = CreateText("Value", root, "0", 34f, FontStyles.Normal, TextAlignmentOptions.Left, TextMain);
        Anchor(valueText.rectTransform, new Vector2(0f, 0.5f), new Vector2(98f, -18f), new Vector2(150f, 44f), new Vector2(0f, 0.5f));
    }

    private static void BuildReportRow(RectTransform parent, string label, string value, int index)
    {
        float y = 86f - index * 34f;
        TextMeshProUGUI labelText = CreateText($"Label_{index}", parent, label, 19f, FontStyles.Normal, TextAlignmentOptions.Left, TextMain);
        Anchor(labelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(36f, y), new Vector2(270f, 28f), new Vector2(0f, 0.5f));

        TextMeshProUGUI valueText = CreateText($"Value_{index}", parent, value, 20f, FontStyles.Normal, TextAlignmentOptions.Right, index == 1 || index == 4 ? new Color(0.70f, 0.96f, 0.34f, 1f) : Cyan);
        Anchor(valueText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-28f, y), new Vector2(190f, 28f), new Vector2(1f, 0.5f));

        if (index < 5)
            CreateLine(parent, $"ReportLine_{index}", new Vector2(0.5f, 0.5f), new Vector2(10f, y - 17f), new Vector2(430f, 2f), new Color(0.21f, 0.78f, 0.88f, 0.23f));
    }

    private static TextMeshProUGUI BuildFooterItem(RectTransform parent, string name, string iconName, string label, string value, Vector2 anchoredPosition, float width)
    {
        RectTransform root = CreateRect(name, parent);
        Anchor(root, new Vector2(0f, 0f), anchoredPosition, new Vector2(width, 86f), new Vector2(0.5f, 0.5f));

        Image icon = CreateImage("Icon", root, Load(iconName), Color.white);
        Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(62f, 62f), new Vector2(0.5f, 0.5f));

        TextMeshProUGUI labelText = CreateText("Label", root, label, 17f, FontStyles.Normal, TextAlignmentOptions.Left, TextSoft);
        Anchor(labelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(88f, 18f), new Vector2(width - 92f, 24f), new Vector2(0f, 0.5f));

        TextMeshProUGUI valueText = CreateText("Value", root, value, 24f, FontStyles.Normal, TextAlignmentOptions.Left, Cyan);
        Anchor(valueText.rectTransform, new Vector2(0f, 0.5f), new Vector2(88f, -16f), new Vector2(width - 92f, 34f), new Vector2(0f, 0.5f));
        return valueText;
    }

    private static Button BuildButton(RectTransform parent, string name, string iconName, string label, Sprite sprite, Color color, Vector2 anchoredPosition)
    {
        Image image = CreateImage(name, parent, sprite, Color.white);
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = true;
        Anchor(image.rectTransform, new Vector2(0.5f, 0f), anchoredPosition, new Vector2(560f, 82f), new Vector2(0.5f, 0f));

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        Image icon = CreateImage("Icon", image.rectTransform, Load(iconName), Color.white);
        Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(98f, 0f), new Vector2(52f, 52f), new Vector2(0.5f, 0.5f));

        TextMeshProUGUI text = CreateText("Label", image.rectTransform, label, 25f, FontStyles.Normal, TextAlignmentOptions.Center, color);
        Anchor(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(46f, 0f), new Vector2(390f, 44f), new Vector2(0.5f, 0.5f));
        return button;
    }

    private static void CreateSectionTitle(RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size)
    {
        TextMeshProUGUI title = CreateText(text.Replace(" ", string.Empty), parent, text, 23f, FontStyles.SmallCaps, TextAlignmentOptions.Left, Cyan);
        Anchor(title.rectTransform, new Vector2(0f, 1f), anchoredPosition, size, new Vector2(0f, 0.5f));
    }

    private static void CreateLine(RectTransform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        => CreateLine(parent, name, anchor, anchoredPosition, size, CyanDim);

    private static void CreateLine(RectTransform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        Image line = CreateImage(name, parent, Load("Line_Cyan"), color);
        Anchor(line.rectTransform, anchor, anchoredPosition, size, new Vector2(0.5f, 0.5f));
    }

    private static string FormatTime(int ms)
    {
        if (ms <= 0)
            return "02:34";

        int totalSeconds = Mathf.Max(0, ms / 1000);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private static Sprite Load(string name)
        => Resources.Load<Sprite>(ResourceRoot + name);

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? color : new Color(color.r, color.g, color.b, color.a * 0.7f);
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureTopCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
    }

    private static void EnsureEventSystem()
    {
        var eventSystem = FindFirstObjectByType<EventSystem>();
        GameObject eventSystemGo;
        if (eventSystem == null)
        {
            eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventSystemGo.GetComponent<EventSystem>();
        }
        else
        {
            eventSystemGo = eventSystem.gameObject;
            eventSystemGo.SetActive(true);
        }

        eventSystem.enabled = true;

        var inputSystemModule = eventSystemGo.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
            inputSystemModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
        inputSystemModule.enabled = true;

        var standalone = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (standalone != null)
            standalone.enabled = false;

        if (Application.isPlaying)
            DontDestroyOnLoad(eventSystemGo);
    }
}

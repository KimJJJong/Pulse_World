using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class HomeAppearanceSelectorUI : MonoBehaviour
{
    public static event Action<int, int> AppearanceAppliedChanged;
    public static int LastSavedAppearanceId { get; private set; }
    public static int LastAppliedAppearanceId { get; private set; }

    public static void PublishAppearanceAppliedChanged(int savedAppearanceId, int appliedAppearanceId)
    {
        LastSavedAppearanceId = savedAppearanceId;
        LastAppliedAppearanceId = appliedAppearanceId;
        AppearanceAppliedChanged?.Invoke(savedAppearanceId, appliedAppearanceId);
    }

    private const float PanelWidth = 380f;
    private const float PanelHeight = 300f;

    private RectTransform _root;
    private GameObject _popup;
    private Text _statusText;
    private Text _currentText;
    private Text _appliedText;
    private Button _openButton;
    private bool _isSaving;
    private int _currentAppearanceId;
    private int _savedAppearanceId;
    private bool _panelVisible = false;
    private bool _hasSceneOptionButtons;
    private readonly Dictionary<int, Button> _optionButtons = new();

    private void Awake()
    {
        BuildUi();
    }

    private void OnEnable()
    {
        BuildUi();

        if (!Application.isPlaying && _popup != null)
            _popup.SetActive(true);
    }

    private async void Start()
    {
        if (!Application.isPlaying)
            return;

        await RefreshFromServerAsync();
    }

    private void BuildUi()
    {
        if (_root != null)
            return;

        var parent = transform as RectTransform;
        if (parent == null)
        {
            Debug.LogWarning("[HomeAppearanceSelectorUI] Parent is not RectTransform.");
            return;
        }

        _root = FindRectTransform("AppearanceSelectorPanel") ?? CreatePanel("AppearanceSelectorPanel", parent, new Vector2(PanelWidth, PanelHeight), new Vector2(40f, -40f), new Color(0.08f, 0.08f, 0.12f, 0.86f));

        var titleText = FindText("AppearanceTitle") ?? CreateLabel("AppearanceTitle", _root, "외형 선택", 22, TextAnchor.MiddleLeft, new Vector2(18f, -18f), new Vector2(280f, 28f));
        titleText.text = "외형 선택";
        titleText.fontSize = 22;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.raycastTarget = false;

        _currentText = FindText("AppearanceCurrent") ?? CreateLabel("AppearanceCurrent", _root, "선택 외형: -", 18, TextAnchor.MiddleLeft, new Vector2(18f, -56f), new Vector2(320f, 24f));
        _appliedText = FindText("AppearanceApplied") ?? CreateLabel("AppearanceApplied", _root, "적용 외형: -", 18, TextAnchor.MiddleLeft, new Vector2(18f, -82f), new Vector2(320f, 24f));
        _statusText = FindText("AppearanceStatus") ?? CreateLabel("AppearanceStatus", _root, "준비 중", 16, TextAnchor.MiddleLeft, new Vector2(18f, -108f), new Vector2(320f, 24f));

        _openButton = FindButton("AppearanceOpenButton") ?? CreateButton(parent, "AppearanceOpenButton", "외형 열기", new Vector2(18f, -8f), new Vector2(140f, 34f));
        SetButtonLabel(_openButton, "외형 닫기");
        _openButton.onClick.RemoveAllListeners();
        _openButton.onClick.AddListener(ToggleAppearanceVisibility);

        _hasSceneOptionButtons = BindSceneOptionButtons();
        if (_hasSceneOptionButtons)
        {
            _popup = null;
        }
        else
        {
            _popup = FindGameObject("AppearancePopup") ?? CreatePopup(_root);
            if (_popup != null)
                _popup.SetActive(true);
        }

        SetAppearanceVisible(false);
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;

        var img = go.GetComponent<Image>();
        img.color = color;
        return rect;
    }

    private GameObject CreatePopup(RectTransform parent)
    {
        var popupRoot = CreatePanel("AppearancePopup", parent, new Vector2(PanelWidth, 310f), new Vector2(40f, -300f), new Color(0.05f, 0.05f, 0.08f, 0.96f));

        CreateLabel("PopupTitle", popupRoot, "선택 가능한 외형", 20, TextAnchor.MiddleLeft, new Vector2(18f, -18f), new Vector2(260f, 28f));
        var closeButton = CreateButton(popupRoot, "CloseButton", "닫기", new Vector2(PanelWidth - 86f, -16f), new Vector2(70f, 28f));
        closeButton.onClick.AddListener(() => popupRoot.gameObject.SetActive(false));

        var buttonsRoot = new GameObject("AppearanceButtons", typeof(RectTransform));
        buttonsRoot.transform.SetParent(popupRoot, false);
        var buttonsRect = buttonsRoot.GetComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0f, 0f);
        buttonsRect.anchorMax = new Vector2(1f, 1f);
        buttonsRect.offsetMin = new Vector2(18f, 18f);
        buttonsRect.offsetMax = new Vector2(-18f, -56f);

        float y = -8f;
        foreach (var option in AppearanceCatalog.Options)
        {
            var label = option.Id == 0
                ? option.DisplayName
                : $"{option.DisplayName} ({option.Id})";
            var optionButton = CreateButton(buttonsRect, $"Option_{option.Id}", label, new Vector2(0f, y), new Vector2(PanelWidth - 72f, 32f));
            var capturedId = option.Id;
            optionButton.onClick.AddListener(() => _ = SelectAppearanceAsync(capturedId));
            _optionButtons[capturedId] = optionButton;
            y -= 40f;
        }

        return popupRoot.gameObject;
    }

    private Button CreateButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.24f, 0.96f);

        var btn = go.GetComponent<Button>();

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);

        var text = textGo.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;

        return btn;
    }

    private Text CreateLabel(string name, RectTransform parent, string label, int fontSize, TextAnchor alignment, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var text = go.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private RectTransform FindRectTransform(string name)
    {
        var transforms = transform.GetComponentsInChildren<RectTransform>(true);
        foreach (var rect in transforms)
        {
            if (rect != null && rect.name == name)
                return rect;
        }

        return null;
    }

    private Text FindText(string name)
    {
        var texts = transform.GetComponentsInChildren<Text>(true);
        foreach (var text in texts)
        {
            if (text != null && text.gameObject.name == name)
                return text;
        }

        return null;
    }

    private Button FindButton(string name)
    {
        var buttons = transform.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button != null && button.gameObject.name == name)
                return button;
        }

        return null;
    }

    private GameObject FindGameObject(string name)
    {
        var transforms = transform.GetComponentsInChildren<Transform>(true);
        foreach (var child in transforms)
        {
            if (child != null && child.name == name)
                return child.gameObject;
        }

        return null;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        var labelText = button.GetComponentInChildren<Text>(true);
        if (labelText != null)
        {
            labelText.text = label;
            labelText.raycastTarget = false;
        }
    }

    private void ToggleAppearanceVisibility()
    {
        _panelVisible = !_panelVisible;
        SetAppearanceVisible(_panelVisible);
    }

    private void SetAppearanceVisible(bool visible)
    {
        _panelVisible = visible;

        if (_hasSceneOptionButtons)
        {
            SetGameObjectActive("AppearanceSelectorPanel", visible);
            SetGameObjectActive("AppearanceTitle", visible);
            SetGameObjectActive("AppearanceCurrent", visible);
            SetGameObjectActive("AppearanceApplied", visible);
            SetGameObjectActive("AppearanceStatus", visible);

            foreach (var button in _optionButtons.Values)
            {
                if (button != null)
                    button.gameObject.SetActive(visible);
            }
        }
        else if (_popup != null)
        {
            _popup.SetActive(visible);
        }

        if (_openButton != null)
        {
            _openButton.gameObject.SetActive(true);
            SetButtonLabel(_openButton, visible ? "외형 닫기" : "외형 열기");
        }
    }

    private void SetGameObjectActive(string name, bool active)
    {
        var go = FindGameObject(name);
        if (go != null)
            go.SetActive(active);
    }

    private async Task RefreshFromServerAsync()
    {
        var uid = GetCurrentUid();
        if (string.IsNullOrWhiteSpace(uid))
        {
            SetStatus("UID를 찾지 못했습니다.");
            return;
        }

        var api = AppBootstrap.Instance?.Root?.PlayerStateApi;
        if (api == null)
        {
            SetStatus("PlayerStateApi가 준비되지 않았습니다.");
            return;
        }

        SetStatus("외형 정보를 불러오는 중...");
        var res = await api.GetPlayerStateAsync(uid);
        if (!res.Ok || res.Data == null)
        {
            SetStatus($"불러오기 실패: {res.Error}");
            return;
        }

        _savedAppearanceId = res.Data.SavedAppearanceId;
        _currentAppearanceId = res.Data.AppearanceId;
        PublishAppearanceAppliedChanged(_savedAppearanceId, _currentAppearanceId);
        ClientGameState.Instance?.ApplyLocalPlayerAppearance(_currentAppearanceId);
        UpdateCurrentLabel();
        UpdateOptionHighlights();
        SetStatus("외형 정보를 불러왔습니다.");
    }

    private async Task SelectAppearanceAsync(int appearanceId)
    {
        if (_isSaving)
            return;

        var uid = GetCurrentUid();
        if (string.IsNullOrWhiteSpace(uid))
        {
            SetStatus("UID를 찾지 못했습니다.");
            return;
        }

        var api = AppBootstrap.Instance?.Root?.PlayerStateApi;
        if (api == null)
        {
            SetStatus("PlayerStateApi가 준비되지 않았습니다.");
            return;
        }

        _isSaving = true;
        SetStatus("저장 중...");

        var res = await api.SetAppearanceAsync(uid, appearanceId);
        _isSaving = false;

        if (!res.Ok || res.Data == null)
        {
            SetStatus($"저장 실패: {res.Error}");
            return;
        }

        _savedAppearanceId = res.Data.SavedAppearanceId;
        _currentAppearanceId = res.Data.AppearanceId;
        PublishAppearanceAppliedChanged(_savedAppearanceId, _currentAppearanceId);
        ClientGameState.Instance?.ApplyLocalPlayerAppearance(_currentAppearanceId);
        UpdateCurrentLabel();
        UpdateOptionHighlights();
        SetStatus($"저장 완료: {AppearanceCatalog.GetDisplayName(_savedAppearanceId)}");

        if (_popup != null)
            _popup.SetActive(false);
    }

    private void UpdateCurrentLabel()
    {
        if (_currentText == null)
            return;

        var savedName = AppearanceCatalog.GetDisplayName(_savedAppearanceId);
        _currentText.text = $"선택 외형: {savedName} ({_savedAppearanceId})";

        if (_appliedText == null)
            return;

        var appliedName = AppearanceCatalog.GetDisplayName(_currentAppearanceId);
        if (_savedAppearanceId == 0 && _currentAppearanceId != 0)
            _appliedText.text = $"적용 외형: {appliedName} ({_currentAppearanceId})";
        else
            _appliedText.text = $"적용 외형: {savedName} ({_currentAppearanceId})";
    }

    private void UpdateOptionHighlights()
    {
        foreach (var kv in _optionButtons)
        {
            if (kv.Value == null)
                continue;

            var img = kv.Value.GetComponent<Image>();
            if (img == null)
                continue;

            img.color = kv.Key == _savedAppearanceId
                ? new Color(0.28f, 0.44f, 0.62f, 0.98f)
                : new Color(0.18f, 0.18f, 0.24f, 0.96f);
        }
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;

        Debug.Log($"[HomeAppearanceSelectorUI] {message}");
    }

    private bool BindSceneOptionButtons()
    {
        var foundAny = false;

        foreach (var option in AppearanceCatalog.Options)
        {
            var button = FindButton($"AppearanceOption_{option.Id}");
            if (button == null)
                continue;

            foundAny = true;
            _optionButtons[option.Id] = button;

            SetButtonLabel(button, option.DisplayName);
            var capturedId = option.Id;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _ = SelectAppearanceAsync(capturedId));

            var img = button.GetComponent<Image>();
            if (img != null)
                img.color = option.Id == 0
                    ? new Color(0.22f, 0.28f, 0.22f, 0.96f)
                    : new Color(0.18f, 0.18f, 0.24f, 0.96f);
        }

        return foundAny;
    }

    private static string GetCurrentUid()
    {
        if (SessionContext.Instance != null && !string.IsNullOrWhiteSpace(SessionContext.Instance.Uid))
            return SessionContext.Instance.Uid;

        var root = AppBootstrap.Instance?.Root;
        if (root != null && !string.IsNullOrWhiteSpace(root.Tokens.Uid))
            return root.Tokens.Uid;

        return "";
    }
}

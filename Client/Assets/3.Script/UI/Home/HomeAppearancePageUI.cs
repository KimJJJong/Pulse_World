using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeAppearancePageUI : MonoBehaviour
{
    private static readonly Vector2 CardSize = new(214f, 144f);
    private static readonly Vector2 PortraitBoxSize = new(118f, 86f);
    private const float PortraitBoxTop = 8f;

    [Serializable]
    public sealed class OptionBinding
    {
        public int AppearanceId;
        public Button Button;
        public TextMeshProUGUI Label;
        public RawImage Portrait;
        public Graphic Highlight;
        public GameObject EquippedMark;
        public GameObject LockedMark;
    }

    [SerializeField] private OptionBinding[] _options;
    [SerializeField] private TextMeshProUGUI _currentText;
    [SerializeField] private TextMeshProUGUI _appliedText;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private Button _applyButton;

    private readonly Dictionary<int, OptionBinding> _bindingsById = new();
    private int _selectedAppearanceId;
    private int _savedAppearanceId;
    private int _appliedAppearanceId;
    private bool _busy;

    private void Awake()
    {
        BindOptions();
        BindApplyButton();
    }

    private async void OnEnable()
    {
        BindOptions();
        BindApplyButton();
        await RefreshFromServerAsync();
    }

    private void BindOptions()
    {
        _bindingsById.Clear();

        if (_options == null)
            return;

        var visibleIndex = 0;
        foreach (var option in _options)
        {
            if (option == null)
                continue;

            if (!AppearanceCatalog.IsSelectableAppearanceId(option.AppearanceId))
            {
                if (option.Button != null)
                    option.Button.gameObject.SetActive(false);
                continue;
            }

            if (option.Button != null)
            {
                option.Button.gameObject.SetActive(true);
                SetOptionCardPosition(option.Button, visibleIndex++);
            }

            _bindingsById[option.AppearanceId] = option;
            if (option.Label != null)
                option.Label.text = AppearanceCatalog.GetDisplayName(option.AppearanceId);

            option.Portrait = EnsurePortrait(option);
            ApplyPortrait(option);

            if (option.Button != null)
            {
                var capturedId = option.AppearanceId;
                option.Button.onClick.RemoveAllListeners();
                option.Button.onClick.AddListener(() => Select(capturedId));
            }

            if (option.LockedMark != null)
                option.LockedMark.SetActive(false);
        }
    }

    private void BindApplyButton()
    {
        if (_applyButton == null)
            return;

        _applyButton.onClick.RemoveListener(HandleApplyClicked);
        _applyButton.onClick.AddListener(HandleApplyClicked);
    }

    private void Select(int appearanceId)
    {
        _selectedAppearanceId = appearanceId;
        UpdateLabels();
        UpdateHighlights();
    }

    private void HandleApplyClicked()
    {
        _ = ApplySelectedAsync();
    }

    private async Task RefreshFromServerAsync()
    {
        if (_busy)
            return;

        var uid = GetCurrentUid();
        if (string.IsNullOrWhiteSpace(uid))
        {
            SetStatus("UID를 찾지 못했습니다.");
            SelectDefault();
            return;
        }

        var api = AppBootstrap.Instance?.Root?.PlayerStateApi;
        if (api == null)
        {
            SetStatus("PlayerStateApi가 준비되지 않았습니다.");
            SelectDefault();
            return;
        }

        _busy = true;
        SetStatus("외형 정보를 불러오는 중...");
        var res = await api.GetPlayerStateAsync(uid);
        _busy = false;

        if (!res.Ok || res.Data == null)
        {
            SetStatus($"불러오기 실패: {res.Error}");
            SelectDefault();
            return;
        }

        _savedAppearanceId = res.Data.SavedAppearanceId;
        _appliedAppearanceId = res.Data.AppearanceId;
        _selectedAppearanceId = AppearanceCatalog.NormalizeSelectableAppearanceId(_savedAppearanceId, _appliedAppearanceId);
        HomeAppearanceSelectorUI.PublishAppearanceAppliedChanged(_savedAppearanceId, _appliedAppearanceId);
        UpdateLabels();
        UpdateHighlights();
        SetStatus("외형 정보를 불러왔습니다.");
    }

    private async Task ApplySelectedAsync()
    {
        if (_busy)
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

        _selectedAppearanceId = AppearanceCatalog.NormalizeSelectableAppearanceId(_selectedAppearanceId, _appliedAppearanceId);
        _busy = true;
        SetApplyInteractable(false);
        SetStatus("외형 저장 중...");

        var res = await api.SetAppearanceAsync(uid, _selectedAppearanceId);
        _busy = false;
        SetApplyInteractable(true);

        if (!res.Ok || res.Data == null)
        {
            SetStatus($"저장 실패: {res.Error}");
            return;
        }

        _savedAppearanceId = res.Data.SavedAppearanceId;
        _appliedAppearanceId = res.Data.AppearanceId;
        _selectedAppearanceId = AppearanceCatalog.NormalizeSelectableAppearanceId(_savedAppearanceId, _appliedAppearanceId);
        HomeAppearanceSelectorUI.PublishAppearanceAppliedChanged(_savedAppearanceId, _appliedAppearanceId);
        UpdateLabels();
        UpdateHighlights();
        SetStatus($"저장 완료: {AppearanceCatalog.GetDisplayName(_savedAppearanceId)}");
    }

    private void SelectDefault()
    {
        _selectedAppearanceId = AppearanceCatalog.NormalizeSelectableAppearanceId(_appliedAppearanceId, _savedAppearanceId);

        UpdateLabels();
        UpdateHighlights();
    }

    private void UpdateLabels()
    {
        if (_currentText != null)
            _currentText.text = $"선택 외형: {AppearanceCatalog.GetDisplayName(_selectedAppearanceId)}";

        if (_appliedText != null)
            _appliedText.text = $"적용 외형: {AppearanceCatalog.GetDisplayName(_appliedAppearanceId)}";
    }

    private void UpdateHighlights()
    {
        var savedHighlightId = AppearanceCatalog.NormalizeSelectableAppearanceId(_savedAppearanceId, _appliedAppearanceId);
        var appliedHighlightId = AppearanceCatalog.NormalizeSelectableAppearanceId(_appliedAppearanceId, _savedAppearanceId);

        foreach (var kv in _bindingsById)
        {
            var selected = kv.Key == _selectedAppearanceId;
            var equipped = kv.Key == appliedHighlightId || kv.Key == savedHighlightId;
            var binding = kv.Value;

            if (binding.Highlight != null)
                binding.Highlight.color = selected
                    ? new Color(1f, 0.86f, 0.32f, 0.36f)
                    : new Color(1f, 1f, 1f, 0f);

            if (binding.Portrait != null)
            {
                binding.Portrait.color = selected
                    ? new Color(1f, 0.98f, 0.88f, 1f)
                    : new Color(0.86f, 0.82f, 0.74f, 1f);
            }

            if (binding.EquippedMark != null)
                binding.EquippedMark.SetActive(equipped);
        }
    }

    private static RawImage EnsurePortrait(OptionBinding option)
    {
        if (option?.Button == null)
            return option?.Portrait;

        if (option.Portrait != null)
            return option.Portrait;

        Transform existing = option.Button.transform.Find("Portrait");
        if (existing != null && existing.TryGetComponent(out RawImage existingImage))
            return existingImage;

        var go = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(option.Button.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(17f, 41f);
        rect.offsetMax = new Vector2(-17f, -11f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var portrait = go.GetComponent<RawImage>();
        portrait.raycastTarget = false;
        portrait.color = new Color(0.86f, 0.82f, 0.74f, 1f);
        portrait.transform.SetSiblingIndex(1);
        return portrait;
    }

    private static void ApplyPortrait(OptionBinding option)
    {
        if (option?.Portrait == null)
            return;

        string resourcePath = AppearanceCatalog.GetPortraitResourcePath(option.AppearanceId);
        var texture = string.IsNullOrWhiteSpace(resourcePath)
            ? null
            : Resources.Load<Texture2D>(resourcePath);

        option.Portrait.texture = texture;
        option.Portrait.gameObject.SetActive(texture != null);
        FitPortrait(option.Portrait, texture);
    }

    private static void SetOptionCardPosition(Button button, int visibleIndex)
    {
        if (button == null)
            return;

        var rect = button.transform as RectTransform;
        if (rect == null)
            return;

        var col = visibleIndex % 2;
        var row = visibleIndex / 2;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = CardSize;
        rect.anchoredPosition = new Vector2(4f + col * 220f, -(2f + row * 154f));
    }

    private static void FitPortrait(RawImage portrait, Texture2D texture)
    {
        if (portrait == null)
            return;

        portrait.uvRect = BuildCoverUvRect(texture, PortraitBoxSize);

        var rect = portrait.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = PortraitBoxSize;
        rect.anchoredPosition = new Vector2(0f, -PortraitBoxTop);
    }

    private static Rect BuildCoverUvRect(Texture2D texture, Vector2 boxSize)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0 || boxSize.x <= 0f || boxSize.y <= 0f)
            return new Rect(0f, 0f, 1f, 1f);

        var textureAspect = texture.width / (float)texture.height;
        var boxAspect = boxSize.x / boxSize.y;
        if (textureAspect < boxAspect)
        {
            var uvHeight = Mathf.Clamp01(textureAspect / boxAspect);
            return new Rect(0f, (1f - uvHeight) * 0.5f, 1f, uvHeight);
        }

        if (textureAspect > boxAspect)
        {
            var uvWidth = Mathf.Clamp01(boxAspect / textureAspect);
            return new Rect((1f - uvWidth) * 0.5f, 0f, uvWidth, 1f);
        }

        return new Rect(0f, 0f, 1f, 1f);
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;

        Debug.Log($"[HomeAppearancePageUI] {message}");
    }

    private void SetApplyInteractable(bool interactable)
    {
        if (_applyButton != null)
            _applyButton.interactable = interactable;
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

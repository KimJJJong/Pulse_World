using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeAppearancePageUI : MonoBehaviour
{
    private static readonly Vector2 CardSize = new(214f, 144f);
    private static readonly Vector2 PortraitBoxSize = new(92f, 68f);
    private static readonly Vector2 LabelSize = new(168f, 24f);
    private static readonly Vector2 BadgeSize = new(46f, 22f);
    private const float PortraitBoxTop = 14f;
    private const float LabelTop = 94f;
    private const float BadgeTop = 12f;
    private const float BadgeRight = 14f;
    private static TMP_FontAsset _koreanFont;

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
    [SerializeField] private bool _useManualObjectLayout = true;
    [SerializeField] private bool _autoCreateMissingPortraits;

    private readonly Dictionary<int, OptionBinding> _bindingsById = new();
    private int _selectedAppearanceId;
    private int _savedAppearanceId;
    private int _appliedAppearanceId;
    private bool _busy;

    private void Awake()
    {
        ApplyPageFonts();
        BindOptions();
        BindApplyButton();
    }

    private async void OnEnable()
    {
        ApplyPageFonts();
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
                if (!_useManualObjectLayout)
                    SetOptionCardPosition(option.Button, visibleIndex);
                visibleIndex++;
            }

            option.Portrait = ResolvePortrait(option, _autoCreateMissingPortraits);
            if (!_useManualObjectLayout)
                NormalizeOptionLayout(option);
            else
                ApplyManualObjectBindings(option);

            _bindingsById[option.AppearanceId] = option;
            if (option.Label != null)
                option.Label.text = AppearanceCatalog.GetDisplayName(option.AppearanceId);

            ApplyPortrait(option, !_useManualObjectLayout);

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

    private void ApplyPageFonts()
    {
        ApplyPreferredFont(_currentText);
        ApplyPreferredFont(_appliedText);
        ApplyPreferredFont(_statusText);

        if (_applyButton == null)
            return;

        foreach (var text in _applyButton.GetComponentsInChildren<TMP_Text>(true))
            ApplyPreferredFont(text);
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

    private static RawImage ResolvePortrait(OptionBinding option, bool createIfMissing)
    {
        if (option?.Button == null)
            return option?.Portrait;

        if (option.Portrait != null)
            return option.Portrait;

        Transform existing = option.Button.transform.Find("Portrait");
        if (existing != null && existing.TryGetComponent(out RawImage existingImage))
            return existingImage;

        if (!createIfMissing)
            return null;

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

    private static void ApplyManualObjectBindings(OptionBinding option)
    {
        if (option == null)
            return;

        if (option.Highlight != null)
            option.Highlight.raycastTarget = false;

        if (option.Label != null)
        {
            ApplyPreferredFont(option.Label);
            option.Label.raycastTarget = false;
        }

        if (option.Portrait != null)
            option.Portrait.raycastTarget = false;
    }

    private static void NormalizeOptionLayout(OptionBinding option)
    {
        if (option == null)
            return;

        if (option.Highlight != null)
        {
            option.Highlight.raycastTarget = false;
            Stretch(option.Highlight.rectTransform);
            option.Highlight.transform.SetAsFirstSibling();
        }

        if (option.Label != null)
        {
            ApplyPreferredFont(option.Label);
            SetTopCenterRect(option.Label.rectTransform, LabelTop, LabelSize);
            option.Label.fontSize = 14f;
            option.Label.enableAutoSizing = true;
            option.Label.fontSizeMin = 10f;
            option.Label.fontSizeMax = 14f;
            option.Label.alignment = TextAlignmentOptions.Center;
            option.Label.textWrappingMode = TextWrappingModes.NoWrap;
            option.Label.overflowMode = TextOverflowModes.Ellipsis;
            option.Label.raycastTarget = false;
            option.Label.transform.SetAsLastSibling();
        }

        NormalizeBadge(option.Button != null ? option.Button.transform.Find("HoldBadge") : null);
        NormalizeBadge(option.EquippedMark != null ? option.EquippedMark.transform : null);
        NormalizeBadge(option.LockedMark != null ? option.LockedMark.transform : null);
    }

    private static void ApplyPortrait(OptionBinding option, bool fitLayout)
    {
        if (option?.Portrait == null)
            return;

        string resourcePath = AppearanceCatalog.GetPortraitResourcePath(option.AppearanceId);
        var texture = string.IsNullOrWhiteSpace(resourcePath)
            ? null
            : Resources.Load<Texture2D>(resourcePath);

        option.Portrait.texture = texture;
        option.Portrait.gameObject.SetActive(texture != null);

        if (fitLayout && option.Portrait.gameObject.activeSelf)
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

        portrait.uvRect = new Rect(0f, 0f, 1f, 1f);

        var rect = portrait.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        var fittedSize = FitSizeInside(texture, PortraitBoxSize);
        var centeredTop = PortraitBoxTop + (PortraitBoxSize.y - fittedSize.y) * 0.5f;
        rect.sizeDelta = fittedSize;
        rect.anchoredPosition = new Vector2(0f, -centeredTop);
        portrait.raycastTarget = false;
    }

    private static Vector2 FitSizeInside(Texture2D texture, Vector2 boxSize)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0 || boxSize.x <= 0f || boxSize.y <= 0f)
            return boxSize;

        var textureAspect = texture.width / (float)texture.height;
        var boxAspect = boxSize.x / boxSize.y;
        var size = boxSize;
        if (textureAspect > boxAspect)
            size.y = boxSize.x / textureAspect;
        else if (textureAspect < boxAspect)
            size.x = boxSize.y * textureAspect;

        return size;
    }

    private static void NormalizeBadge(Transform badge)
    {
        var rect = badge as RectTransform;
        if (rect == null)
            return;

        SetTopRightRect(rect, BadgeTop, BadgeRight, BadgeSize);
        var graphic = badge.GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = false;
        badge.SetAsLastSibling();
    }

    private static void SetTopCenterRect(RectTransform rect, float top, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, -top);
    }

    private static void SetTopRightRect(RectTransform rect, float top, float right, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(-right, -top);
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

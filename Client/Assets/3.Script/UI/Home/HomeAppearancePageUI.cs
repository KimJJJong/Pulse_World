using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeAppearancePageUI : MonoBehaviour
{
    [Serializable]
    public sealed class OptionBinding
    {
        public int AppearanceId;
        public Button Button;
        public TextMeshProUGUI Label;
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

        foreach (var option in _options)
        {
            if (option == null)
                continue;

            _bindingsById[option.AppearanceId] = option;
            if (option.Label != null)
                option.Label.text = AppearanceCatalog.GetDisplayName(option.AppearanceId);

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
        _selectedAppearanceId = _savedAppearanceId;
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
        _selectedAppearanceId = _savedAppearanceId;
        HomeAppearanceSelectorUI.PublishAppearanceAppliedChanged(_savedAppearanceId, _appliedAppearanceId);
        UpdateLabels();
        UpdateHighlights();
        SetStatus($"저장 완료: {AppearanceCatalog.GetDisplayName(_savedAppearanceId)}");
    }

    private void SelectDefault()
    {
        if (_options != null && _options.Length > 0)
            _selectedAppearanceId = _options[0].AppearanceId;

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
        foreach (var kv in _bindingsById)
        {
            var selected = kv.Key == _selectedAppearanceId;
            var equipped = kv.Key == _appliedAppearanceId || kv.Key == _savedAppearanceId;
            var binding = kv.Value;

            if (binding.Highlight != null)
                binding.Highlight.color = selected
                    ? new Color(1f, 0.86f, 0.32f, 0.36f)
                    : new Color(1f, 1f, 1f, 0f);

            if (binding.EquippedMark != null)
                binding.EquippedMark.SetActive(equipped);
        }
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

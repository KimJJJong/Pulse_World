using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeMapRealmUI : MonoBehaviour
{
    [Serializable]
    public sealed class RealmBinding
    {
        public string RealmId;
        public string DisplayName;
        public string Description;
        public string RequiredTicket;
        public Button Button;
        public Graphic Highlight;
    }

    [SerializeField] private RealmBinding[] _realms;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _ticketInfo;
    [SerializeField] private Button _selectButton;
    [SerializeField] private TextMeshProUGUI _status;

    private int _selectedIndex;
    private bool _busy;

    private void Awake()
    {
        BindButtons();
        Select(0);

        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveListener(HandleSelectClicked);
            _selectButton.onClick.AddListener(HandleSelectClicked);
        }
    }

    private void OnEnable()
    {
        BindButtons();
        Select(Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, (_realms?.Length ?? 1) - 1)));
    }

    private void BindButtons()
    {
        if (_realms == null)
            return;

        for (var i = 0; i < _realms.Length; i++)
        {
            var index = i;
            var binding = _realms[i];
            if (binding?.Button == null)
                continue;

            binding.Button.onClick.RemoveAllListeners();
            binding.Button.onClick.AddListener(() => Select(index));
        }
    }

    private void Select(int index)
    {
        if (_realms == null || _realms.Length == 0)
            return;

        _selectedIndex = Mathf.Clamp(index, 0, _realms.Length - 1);
        var selected = _realms[_selectedIndex];

        if (_title != null)
            _title.text = selected.DisplayName;
        if (_description != null)
            _description.text = selected.Description;
        if (_ticketInfo != null)
            _ticketInfo.text = $"Ticket: {selected.RequiredTicket}";
        if (_status != null && !_busy)
            _status.text = "지역을 선택한 뒤 입장 버튼을 누르세요.";

        for (var i = 0; i < _realms.Length; i++)
        {
            var highlight = _realms[i]?.Highlight;
            if (highlight != null)
                highlight.color = i == _selectedIndex
                    ? new Color(1f, 0.86f, 0.32f, 0.36f)
                    : new Color(1f, 1f, 1f, 0f);
        }
    }

    private void HandleSelectClicked()
    {
        _ = ConnectTownAsync();
    }

    private async Task ConnectTownAsync()
    {
        if (_busy)
            return;

        if (_realms == null || _realms.Length == 0)
            return;

        var root = AppBootstrap.Instance?.Root;
        if (root?.SessionApi == null)
        {
            SetStatus("SessionApi가 준비되지 않았습니다.");
            return;
        }

        _busy = true;
        SetButtonInteractable(false);
        var realm = _realms[_selectedIndex];
        SetStatus($"{realm.DisplayName} 티켓 확인 중...");

        var result = await root.SessionApi.IssueTownTicketAsync("");
        if (!result.Ok || result.Data == null)
        {
            SetStatus($"티켓 발급 실패: {result.Error}");
            _busy = false;
            SetButtonInteractable(true);
            return;
        }

        SetStatus("티켓 확인 완료. 이동 중...");
        var nonce = $"town-{realm.RealmId}-{Guid.NewGuid():N}";
        await ClientFlow.Instance.ConnectTown(result.Data, nonce);
        _busy = false;
        SetButtonInteractable(true);
    }

    private void SetStatus(string message)
    {
        if (_status != null)
            _status.text = message;

        Debug.Log($"[HomeMapRealmUI] {message}");
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (_selectButton != null)
            _selectButton.interactable = interactable;
    }
}

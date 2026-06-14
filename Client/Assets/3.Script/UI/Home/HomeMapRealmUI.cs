using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetClient.Town;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeMapRealmUI : MonoBehaviour
{
    private const string MissingMapMessage = "아직 열리지 않은 지역입니다.";
    private const string ReadyMessage = "지역을 선택한 뒤 입장 버튼을 누르세요.";
    private const int DefaultTownMaxPlayers = 4;
    private static readonly Color CreateClearAccent = new(1f, 1f, 1f, 0f);
    private static readonly Color CreateSelectedText = new(0.98f, 0.91f, 0.70f, 1f);
    private static readonly Color CreateIdleText = new(0.84f, 0.76f, 0.58f, 1f);
    private static TMP_FontAsset _koreanFont;

    private enum EntryMode
    {
        Existing,
        Create,
        Key
    }

    private enum EntryView
    {
        Choice,
        Existing,
        Create,
        Key
    }

    [Serializable]
    public sealed class RealmBinding
    {
        public string RealmId;
        public string DisplayName;
        public string Description;
        public string RequiredTicket;
        public string SceneName;
        public Button Button;
        public Graphic Highlight;
    }

    [Serializable]
    public sealed class TownRoomRowBinding
    {
        public GameObject Root;
        public Button Button;
        public Button JoinButton;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Meta;
        public TextMeshProUGUI SteamBadge;
        public Graphic SelectedFrame;
    }

    [Serializable]
    public sealed class MaxPlayerButtonBinding
    {
        public int Value;
        public Button Button;
        public TextMeshProUGUI Label;
        public Graphic Highlight;
    }

    [SerializeField] private RealmBinding[] _realms;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _ticketInfo;
    [SerializeField] private Button _selectButton;
    [SerializeField] private TextMeshProUGUI _status;

    [Header("Town Entry Choice")]
    [SerializeField] private GameObject _choicePanel;
    [SerializeField] private TextMeshProUGUI _choiceTitle;
    [SerializeField] private TextMeshProUGUI _choiceStatus;
    [SerializeField] private Button _closeChoiceButton;
    [SerializeField] private GameObject _entryChoiceRoot;
    [SerializeField] private Button _choiceExistingButton;
    [SerializeField] private Button _choiceCreateButton;
    [SerializeField] private Button _choiceKeyButton;
    [SerializeField] private Button _choiceOpenSelectedButton;
    [SerializeField] private Button _choiceBackButton;
    [SerializeField] private TMP_InputField _quickKeyInput;
    [SerializeField] private Button _quickKeyJoinButton;

    [Header("Existing Towns")]
    [SerializeField] private GameObject _existingTownsRoot;
    [SerializeField] private TMP_InputField _townSearchInput;
    [SerializeField] private Button _refreshTownRoomsButton;
    [SerializeField] private Button _existingBackButton;
    [SerializeField] private TextMeshProUGUI _existingStatus;
    [SerializeField] private TextMeshProUGUI _emptyRoomText;
    [SerializeField] private TownRoomRowBinding[] _roomRows;
    [SerializeField] private TextMeshProUGUI _selectedTownTitle;
    [SerializeField] private TextMeshProUGUI _selectedTownHost;
    [SerializeField] private TextMeshProUGUI _selectedTownPlayers;
    [SerializeField] private TextMeshProUGUI _selectedTownDescription;
    [SerializeField] private TextMeshProUGUI _selectedTownKey;
    [SerializeField] private Button _joinSelectedTownButton;

    [Header("Create Town")]
    [SerializeField] private GameObject _createTownRoot;
    [SerializeField] private TMP_InputField _townNameInput;
    [SerializeField] private Button _privateVisibilityButton;
    [SerializeField] private Button _publicVisibilityButton;
    [SerializeField] private TextMeshProUGUI _visibilityHint;
    [SerializeField] private MaxPlayerButtonBinding[] _maxPlayerButtons;
    [SerializeField] private Button _createTownButton;
    [SerializeField] private Button _createCancelButton;

    [Header("Join With Key")]
    [SerializeField] private GameObject _keyTownRoot;
    [SerializeField] private TMP_InputField _inviteCodeInput;
    [SerializeField] private Button _joinInviteButton;
    [SerializeField] private Button _joinKeyTownButton;
    [SerializeField] private Button _clearKeyButton;
    [SerializeField] private Button _keyCancelButton;
    [SerializeField] private TextMeshProUGUI _keyStatus;
    [SerializeField] private GameObject _keyResultRoot;
    [SerializeField] private TextMeshProUGUI _keyResultTitle;
    [SerializeField] private TextMeshProUGUI _keyResultHost;
    [SerializeField] private TextMeshProUGUI _keyResultPlayers;
    [SerializeField] private TextMeshProUGUI _keyResultDescription;

    private readonly List<TownRoomApiClient.TownRoomSummaryDto> _rooms = new();

    private int _selectedIndex;
    private bool _busy;
    private bool _createPublic = true;
    private int _createMaxPlayers = DefaultTownMaxPlayers;
    private EntryMode _selectedEntryMode = EntryMode.Existing;
    private TownRoomApiClient.TownRoomSummaryDto _selectedTown;
    private TownRoomApiClient.TownRoomSummaryDto _keyResolvedTown;
    private readonly Dictionary<Button, Dictionary<Graphic, Color>> _realmBaseGraphicColors = new();

    private void Awake()
    {
        BindButtons();
        BindChoiceButtons();
        PrepareRealmHighlights();
        PrepareRealmAvailabilityUi();
        ApplyFontToChildren();
        Select(ResolveInitialRealmIndex());

        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveListener(HandleSelectClicked);
            _selectButton.onClick.AddListener(HandleSelectClicked);
        }
    }

    private void OnEnable()
    {
        BindButtons();
        BindChoiceButtons();
        PrepareRealmHighlights();
        PrepareRealmAvailabilityUi();
        ApplyFontToChildren();
        Select(IsRealmAvailable(GetRealm(_selectedIndex))
            ? Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, (_realms?.Length ?? 1) - 1))
            : ResolveInitialRealmIndex());
        if (_choicePanel != null)
            _choicePanel.SetActive(false);
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

    private void BindChoiceButtons()
    {
        Bind(_closeChoiceButton, CloseChoicePanel);
        Bind(_choiceBackButton, CloseChoicePanel);
        Bind(_choiceOpenSelectedButton, () => OpenEntryMode(_selectedEntryMode));
        Bind(_choiceExistingButton, () => OpenEntryMode(EntryMode.Existing));
        Bind(_choiceCreateButton, () => OpenEntryMode(EntryMode.Create));
        Bind(_choiceKeyButton, () => OpenEntryMode(EntryMode.Key));
        Bind(_quickKeyJoinButton, () => _ = JoinQuickKeyAsync());

        Bind(_refreshTownRoomsButton, () => _ = RefreshTownRoomsAsync());
        Bind(_existingBackButton, () => ShowEntryView(EntryView.Choice));
        Bind(_joinSelectedTownButton, () => _ = JoinSelectedTownAsync());

        if (_townSearchInput != null)
        {
            _townSearchInput.onValueChanged.RemoveListener(HandleTownSearchChanged);
            _townSearchInput.onValueChanged.AddListener(HandleTownSearchChanged);
        }

        Bind(_privateVisibilityButton, () => SetCreateVisibility(false));
        Bind(_publicVisibilityButton, () => SetCreateVisibility(true));
        Bind(_createTownButton, () => _ = CreateTownAsync());
        Bind(_createCancelButton, () => ShowEntryView(EntryView.Choice));

        if (_maxPlayerButtons != null)
        {
            foreach (var binding in _maxPlayerButtons)
            {
                if (binding?.Button == null)
                    continue;

                var value = Mathf.Max(2, binding.Value);
                binding.Button.onClick.RemoveAllListeners();
                binding.Button.onClick.AddListener(() => SetCreateMaxPlayers(value));
            }
        }

        Bind(_joinInviteButton, () => _ = FindKeyTownAsync());
        Bind(_joinKeyTownButton, () => _ = JoinKeyTownAsync());
        Bind(_clearKeyButton, ClearKeyResult);
        Bind(_keyCancelButton, () => ShowEntryView(EntryView.Choice));
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

        var hasScene = IsRealmAvailable(selected) && TryGetSceneName(selected, out _);
        if (_ticketInfo != null)
            _ticketInfo.text = hasScene ? $"Ticket: {selected.RequiredTicket}" : "Ticket: 없음";
        if (_status != null && !_busy)
            _status.text = hasScene ? ReadyMessage : MissingMapMessage;

        RefreshSelectButton();

        for (var i = 0; i < _realms.Length; i++)
        {
            ApplyRealmAvailability(_realms[i]);
            ApplyRealmHighlight(_realms[i], i == _selectedIndex && IsRealmAvailable(_realms[i]));
        }

        BringSelectedRealmToFront();
    }

    private void BringSelectedRealmToFront()
    {
        if (_realms == null || _realms.Length == 0)
            return;

        var selectedButton = _realms[_selectedIndex]?.Button;
        if (selectedButton == null || selectedButton.transform.parent == null)
            return;

        var parent = selectedButton.transform.parent;
        var maxRealmSiblingIndex = selectedButton.transform.GetSiblingIndex();
        for (var i = 0; i < _realms.Length; i++)
        {
            var button = _realms[i]?.Button;
            if (button == null || button.transform.parent != parent)
                continue;

            maxRealmSiblingIndex = Mathf.Max(maxRealmSiblingIndex, button.transform.GetSiblingIndex());
        }

        selectedButton.transform.SetSiblingIndex(maxRealmSiblingIndex);
    }

    private void PrepareRealmHighlights()
    {
        if (_realms == null)
            return;

        foreach (var realm in _realms)
            PrepareRealmHighlight(realm);
    }

    private static void PrepareRealmHighlight(RealmBinding realm)
    {
        if (realm?.Highlight == null)
            return;

        var pieceHighlight = realm.Highlight.GetComponent<HomeMapPieceHighlight>();
        if (pieceHighlight == null)
            pieceHighlight = realm.Highlight.gameObject.AddComponent<HomeMapPieceHighlight>();

        if (realm.Highlight is Image highlightImage && highlightImage.sprite != null)
        {
            pieceHighlight.Configure(highlightImage.sprite);
            return;
        }

        var buttonSprite = ResolveButtonSprite(realm.Button);
        if (buttonSprite != null)
        {
            pieceHighlight.Configure(buttonSprite);
            return;
        }

        var buttonTexture = ResolveButtonTexture(realm.Button);
        if (buttonTexture != null)
            pieceHighlight.Configure(buttonTexture);
    }

    private static void ApplyRealmHighlight(RealmBinding realm, bool selected)
    {
        var highlight = realm?.Highlight;
        if (highlight == null)
            return;

        var pieceHighlight = highlight.GetComponent<HomeMapPieceHighlight>();
        if (pieceHighlight != null)
        {
            pieceHighlight.SetSelected(selected);
            return;
        }

        highlight.enabled = true;
        highlight.color = selected
            ? new Color(1f, 0.86f, 0.32f, 0.36f)
            : new Color(1f, 1f, 1f, 0f);
    }

    private static Sprite ResolveButtonSprite(Button button)
    {
        if (button == null)
            return null;

        if (button.targetGraphic is Image targetImage && targetImage.sprite != null)
            return targetImage.sprite;

        var image = button.GetComponent<Image>();
        return image != null ? image.sprite : null;
    }

    private static Texture2D ResolveButtonTexture(Button button)
    {
        if (button == null)
            return null;

        if (button.targetGraphic is RawImage targetRawImage && targetRawImage.texture is Texture2D targetTexture)
            return targetTexture;

        var rawImage = button.GetComponent<RawImage>();
        return rawImage != null ? rawImage.texture as Texture2D : null;
    }

    private void HandleSelectClicked()
    {
        if (_busy || _realms == null || _realms.Length == 0)
            return;

        var realm = _realms[_selectedIndex];
        if (!IsRealmAvailable(realm) || !TryGetSceneName(realm, out var sceneName) || !TryGetTownMapId(realm, out var mapId))
        {
            SetStatus(MissingMapMessage);
            return;
        }

        ShowChoicePanel(realm, sceneName, mapId);
    }

    private void ShowChoicePanel(RealmBinding realm, string sceneName, string mapId)
    {
        if (!HasChoicePanel())
            return;

        _rooms.Clear();
        _selectedTown = null;
        _keyResolvedTown = null;
        _selectedEntryMode = EntryMode.Existing;
        _createPublic = true;
        _createMaxPlayers = DefaultTownMaxPlayers;

        ResetRoomRows();
        ClearSelectedTownDetail();
        ClearKeyResult();

        if (_choicePanel != null)
            _choicePanel.SetActive(true);
        if (_choiceTitle != null)
            _choiceTitle.text = $"{realm.DisplayName} Town";
        if (_quickKeyInput != null)
            _quickKeyInput.text = "";
        if (_inviteCodeInput != null)
            _inviteCodeInput.text = "";
        if (_townSearchInput != null)
            _townSearchInput.text = "";
        if (_townNameInput != null)
            _townNameInput.text = BuildDefaultTownName(realm);

        ShowEntryView(EntryView.Choice);
        SetChoiceStatus("Choose how you want to enter a town.");
        SetStatus($"{realm.DisplayName} 입장 방식 선택 중...");
        UpdateCreateControls();
    }

    private void OpenEntryMode(EntryMode mode)
    {
        if (_busy)
            return;

        _selectedEntryMode = mode;
        switch (mode)
        {
            case EntryMode.Existing:
                ShowEntryView(EntryView.Existing);
                _ = RefreshTownRoomsAsync();
                break;
            case EntryMode.Create:
                ShowEntryView(EntryView.Create);
                UpdateCreateControls();
                break;
            case EntryMode.Key:
                ShowEntryView(EntryView.Key);
                SetKeyStatus("Enter the town key shared by the host.");
                break;
        }
    }

    private void ShowEntryView(EntryView view)
    {
        SetActive(_entryChoiceRoot, view == EntryView.Choice);
        SetActive(_existingTownsRoot, view == EntryView.Existing);
        SetActive(_createTownRoot, view == EntryView.Create);
        SetActive(_keyTownRoot, view == EntryView.Key);

        if (_choiceTitle != null)
        {
            _choiceTitle.text = view switch
            {
                EntryView.Existing => "Existing Towns",
                EntryView.Create => "Create My Town",
                EntryView.Key => "Join with Key",
                _ => $"{GetSelectedRealmName()} Town"
            };
        }

        switch (view)
        {
            case EntryView.Choice:
                SetChoiceStatus("Choose how you want to enter a town.");
                SetStatus($"{GetSelectedRealmName()} 입장 방식 선택 중...");
                break;
            case EntryView.Existing:
                SetChoiceStatus("Browse available towns and join one.");
                SetExistingStatus("Browse available towns and join one.");
                break;
            case EntryView.Create:
                SetChoiceStatus("Create a new town and choose who can join.");
                break;
            case EntryView.Key:
                SetChoiceStatus("Enter the town key shared by the host.");
                break;
        }
    }

    private async Task CreateTownAsync()
    {
        if (!TryGetSelectedRealmContext(out var realm, out var sceneName, out var mapId))
            return;

        var root = AppBootstrap.Instance?.Root;
        if (root?.TownRoomApi == null || root.SessionApi == null)
        {
            SetChoiceStatus("API가 준비되지 않았습니다.");
            return;
        }

        SetBusy(true);
        SetChoiceStatus("이전 Town 정보 정리 중...");
        await LeaveStaleTownContextAsync("home_create_town");
        SetChoiceStatus(_createPublic ? "Public Town 생성 중..." : "Private Town 생성 중...");

        var steam = root.SteamPlatform;
        var title = ResolveCreateTownTitle(realm);
        var created = await root.TownRoomApi.CreateAsync(
            title,
            mapId,
            _createMaxPlayers,
            steam?.SteamId64 ?? "",
            root.Config?.ClientVersion ?? "",
            _createPublic);

        if (!created.Ok || created.Data == null || string.IsNullOrWhiteSpace(created.Data.roomId))
        {
            SetChoiceStatus($"Town 생성 실패: {created.Error}");
            SetBusy(false);
            return;
        }

        var roomId = created.Data.roomId;
        if (steam != null && steam.Enabled && steam.IsInitialized)
        {
            SetChoiceStatus("Steam Lobby 생성 중...");
            var lobbyId = await steam.CreateLobbyAsync(roomId, title, mapId, _createMaxPlayers, "town");
            if (!string.IsNullOrWhiteSpace(lobbyId))
            {
                steam.UpdateLobbyMetadata(roomId, title, mapId, _createMaxPlayers, SessionContext.Instance?.Uid ?? "", "town");
                await root.TownRoomApi.BindSteamLobbyAsync(roomId, lobbyId);
            }
        }

        SetChoiceStatus(_createPublic
            ? "Public Town 준비 완료. 이동 중..."
            : $"Private Town 준비 완료. Key: {roomId}");
        await EnterTownRoomAsync(roomId, mapId, _createMaxPlayers, sceneName, "Host");
    }

    private async Task RefreshTownRoomsAsync()
    {
        if (!TryGetSelectedRealmContext(out _, out _, out var mapId))
            return;

        var root = AppBootstrap.Instance?.Root;
        if (root?.TownRoomApi == null)
        {
            SetExistingStatus("TownRoomApi가 준비되지 않았습니다.");
            return;
        }

        SetBusy(true);
        ResetRoomRows();
        ClearSelectedTownDetail();
        SetExistingStatus("이전 Town 정보 정리 중...");
        await LeaveStaleTownContextAsync("home_find_town");
        SetExistingStatus("기존 Town 검색 중...");

        var result = await root.TownRoomApi.ListAsync(mapId);
        if (!result.Ok)
        {
            SetExistingStatus($"Town 목록 조회 실패: {result.Error}");
            SetBusy(false);
            return;
        }

        var rooms = result.Data?.rooms ?? new List<TownRoomApiClient.TownRoomSummaryDto>();
        var steamRooms = await FindSteamTownLobbiesAsync(root, mapId);
        MergeSteamLobbyHints(rooms, steamRooms);

        _rooms.Clear();
        _rooms.AddRange(rooms
            .Where(x => x != null && x.IsListablePublicRoom())
            .OrderByDescending(x => x.createdAtMs));

        RenderFilteredRooms();
        SetExistingStatus(_rooms.Count == 0 ? "열려 있는 Town이 없습니다." : $"{_rooms.Count}개의 Town을 찾았습니다.");
        SetBusy(false);
    }

    private async Task EnterTownRoomAsync(string roomId, string mapId, int maxPlayers, string sceneName, string roleLabel)
    {
        var root = AppBootstrap.Instance?.Root;
        if (root?.SessionApi == null)
        {
            SetChoiceStatus("SessionApi가 준비되지 않았습니다.");
            SetBusy(false);
            return;
        }

        SetChoiceStatus($"{roleLabel} 입장 토큰 확인 중...");
        var ticket = await root.SessionApi.IssueTownTicketAsync(
            "",
            roomId,
            mapId,
            maxPlayers,
            root.SteamPlatform?.SteamId64 ?? "",
            root.Config?.ClientVersion ?? "");

        if (!ticket.Ok || ticket.Data == null)
        {
            SetChoiceStatus($"티켓 발급 실패: {ticket.Error}");
            SetBusy(false);
            return;
        }

        if (_choicePanel != null)
            _choicePanel.SetActive(false);
        SetStatus("티켓 확인 완료. Town으로 이동 중...");
        var nonce = $"town-{roomId}-{Guid.NewGuid():N}";
        ClientFlow.Instance.SetTargetTownScene(sceneName);
        await ClientFlow.Instance.ConnectTown(ticket.Data, nonce);
        SetBusy(false);
    }

    private async Task JoinSelectedTownAsync()
    {
        if (_selectedTown == null)
        {
            SetExistingStatus("먼저 Town을 선택하세요.");
            return;
        }

        await JoinTownAsync(_selectedTown);
    }

    private async Task JoinTownAsync(TownRoomApiClient.TownRoomSummaryDto room)
    {
        if (room == null)
            return;

        var root = AppBootstrap.Instance?.Root;
        if (root?.TownRoomApi == null)
        {
            SetChoiceStatus("TownRoomApi가 준비되지 않았습니다.");
            return;
        }

        var fallbackScene = TryGetSelectedRealmContext(out _, out var selectedScene, out var selectedMapId)
            ? selectedScene
            : SceneNames.Town_Forest;
        var mapId = !string.IsNullOrWhiteSpace(room.mapId) ? room.mapId : selectedMapId;
        if (!IsForestTownMapId(mapId))
        {
            SetChoiceStatus("현재는 Forest Town만 이용할 수 있습니다.");
            SetBusy(false);
            return;
        }

        var sceneName = ResolveSceneNameForTownMap(mapId, fallbackScene);

        SetBusy(true);
        SetChoiceStatus("Town 참여 요청 중...");

        var steam = root.SteamPlatform;
        if (steam != null && steam.Enabled && steam.IsInitialized && !string.IsNullOrWhiteSpace(room.steamLobbyId))
        {
            SetChoiceStatus("Steam Lobby 참여 중...");
            await steam.JoinLobbyAsync(room.steamLobbyId, room.roomId);
        }

        var joined = await root.TownRoomApi.JoinAsync(room.roomId, steam?.SteamId64 ?? "", root.Config?.ClientVersion ?? "");
        if (!joined.Ok)
        {
            SetChoiceStatus($"Town 참여 실패: {joined.Error}");
            SetBusy(false);
            return;
        }

        await EnterTownRoomAsync(room.roomId, mapId, Mathf.Max(2, room.maxPlayers), sceneName, IsOwnRoom(room) ? "Host" : "Guest");
    }

    private static bool IsOwnRoom(TownRoomApiClient.TownRoomSummaryDto room)
    {
        if (room == null)
            return false;

        var root = AppBootstrap.Instance?.Root;
        var uid = !string.IsNullOrWhiteSpace(SessionContext.Instance?.Uid)
            ? SessionContext.Instance.Uid
            : (root?.Tokens?.Uid ?? "");

        return !string.IsNullOrWhiteSpace(uid)
               && !string.IsNullOrWhiteSpace(room.ownerUid)
               && string.Equals(room.ownerUid, uid, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task LeaveStaleTownContextAsync(string reason)
    {
        if (ClientFlow.Instance == null)
            return;

        await ClientFlow.Instance.LeaveRememberedTownAsync(reason);
    }

    private async Task JoinQuickKeyAsync()
    {
        var code = _quickKeyInput != null ? _quickKeyInput.text : "";
        var room = await GetTownByKeyAsync(code, SetChoiceStatus);
        if (room != null)
            await JoinTownAsync(room);
    }

    private async Task FindKeyTownAsync()
    {
        var code = _inviteCodeInput != null ? _inviteCodeInput.text : "";
        _keyResolvedTown = await GetTownByKeyAsync(code, SetKeyStatus);
        UpdateKeyResult();
    }

    private async Task JoinKeyTownAsync()
    {
        if (_keyResolvedTown == null)
        {
            await FindKeyTownAsync();
            if (_keyResolvedTown == null)
                return;
        }

        await JoinTownAsync(_keyResolvedTown);
    }

    private async Task<TownRoomApiClient.TownRoomSummaryDto> GetTownByKeyAsync(string rawCode, Action<string> statusSetter)
    {
        var code = NormalizeKey(rawCode);
        if (string.IsNullOrWhiteSpace(code))
        {
            statusSetter?.Invoke("Town key를 입력하세요.");
            return null;
        }

        var root = AppBootstrap.Instance?.Root;
        if (root?.TownRoomApi == null)
        {
            statusSetter?.Invoke("TownRoomApi가 준비되지 않았습니다.");
            return null;
        }

        SetBusy(true);
        statusSetter?.Invoke("이전 Town 정보 정리 중...");
        await LeaveStaleTownContextAsync("home_key_join");
        statusSetter?.Invoke("Town key 확인 중...");

        var roomResult = await root.TownRoomApi.GetAsync(code);
        if ((!roomResult.Ok || roomResult.Data == null) && !string.Equals(code, rawCode?.Trim(), StringComparison.Ordinal))
            roomResult = await root.TownRoomApi.GetAsync(rawCode.Trim());

        if (!roomResult.Ok || roomResult.Data == null)
        {
            statusSetter?.Invoke($"Town key 확인 실패: {roomResult.Error}");
            SetBusy(false);
            return null;
        }

        statusSetter?.Invoke("Town key 확인 완료.");
        SetBusy(false);
        return roomResult.Data;
    }

    private async Task<List<SteamLobbyInfo>> FindSteamTownLobbiesAsync(AppCompositionRoot root, string mapId)
    {
        var steam = root?.SteamPlatform;
        if (steam == null || !steam.Enabled || !steam.IsInitialized)
            return new List<SteamLobbyInfo>();

        try
        {
            return await steam.FindLobbiesAsync("town", mapId, 20);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HomeMapRealmUI] Steam lobby search failed: {ex.Message}");
            return new List<SteamLobbyInfo>();
        }
    }

    private static void MergeSteamLobbyHints(
        List<TownRoomApiClient.TownRoomSummaryDto> rooms,
        List<SteamLobbyInfo> steamRooms)
    {
        if (rooms == null || steamRooms == null)
            return;

        var byRoomId = rooms
            .Where(x => !string.IsNullOrWhiteSpace(x.roomId))
            .ToDictionary(x => x.roomId, StringComparer.OrdinalIgnoreCase);

        foreach (var lobby in steamRooms)
        {
            if (string.IsNullOrWhiteSpace(lobby.RoomId))
                continue;

            if (byRoomId.TryGetValue(lobby.RoomId, out var existing)
                && string.IsNullOrWhiteSpace(existing.steamLobbyId))
            {
                existing.steamLobbyId = lobby.LobbyId;
            }
        }
    }

    private void HandleTownSearchChanged(string _)
    {
        RenderFilteredRooms();
    }

    private void RenderFilteredRooms()
    {
        ResetRoomRows();
        var query = (_townSearchInput != null ? _townSearchInput.text : "").Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _rooms
            : _rooms.Where(room => MatchesTownQuery(room, query)).ToList();

        if (filtered.Count == 0)
        {
            AddInfoRow(string.IsNullOrWhiteSpace(query) ? "열려 있는 Town이 없습니다." : "검색 결과가 없습니다.");
            _selectedTown = null;
            ClearSelectedTownDetail();
            return;
        }

        if (_selectedTown == null || filtered.All(x => !SameRoom(x, _selectedTown)))
            _selectedTown = filtered[0];

        foreach (var room in filtered)
            AddRoomRow(room);

        UpdateSelectedTownDetail();
    }

    private static bool MatchesTownQuery(TownRoomApiClient.TownRoomSummaryDto room, string query)
    {
        if (room == null)
            return false;

        return Contains(room.title, query)
               || Contains(room.roomId, query)
               || Contains(room.ownerUid, query)
               || Contains(room.hostUid, query)
               || (room.participants != null && room.participants.Any(x => Contains(x?.name, query) || Contains(x?.uid, query)));
    }

    private void AddRoomRow(TownRoomApiClient.TownRoomSummaryDto room)
    {
        var row = GetInactiveRoomRow();
        if (row == null)
        {
            SetExistingStatus("표시 가능한 Town 목록 수를 초과했습니다. 검색어를 좁혀주세요.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(room.title) ? "Town" : room.title;
        var isOwnRoom = IsOwnRoom(room);
        if (row.Title != null)
            row.Title.text = isOwnRoom ? $"{title} (내 Town)" : title;
        if (row.Meta != null)
            row.Meta.text = $"{room.memberCount} / {Mathf.Max(2, room.maxPlayers)}";
        if (row.SteamBadge != null)
        {
            var hasSteamLobby = !string.IsNullOrWhiteSpace(room.steamLobbyId);
            row.SteamBadge.text = hasSteamLobby ? "Steam" : "";
            row.SteamBadge.gameObject.SetActive(hasSteamLobby);
        }

        if (row.SelectedFrame != null)
            row.SelectedFrame.color = SameRoom(room, _selectedTown)
                ? new Color(0.08f, 0.31f, 0.33f, 0.26f)
                : new Color(0f, 0f, 0f, 0f);

        if (row.Button != null)
        {
            row.Button.onClick.RemoveAllListeners();
            row.Button.onClick.AddListener(() => SelectTown(room));
            row.Button.interactable = !_busy;
        }

        if (row.JoinButton != null)
        {
            row.JoinButton.onClick.RemoveAllListeners();
            row.JoinButton.onClick.AddListener(() => _ = JoinTownAsync(room));
            row.JoinButton.interactable = !_busy;
        }
        else if (row.Button != null)
        {
            row.Button.onClick.AddListener(() => _ = JoinTownAsync(room));
        }

        row.Root.SetActive(true);
    }

    private void SelectTown(TownRoomApiClient.TownRoomSummaryDto room)
    {
        _selectedTown = room;
        RenderFilteredRooms();
    }

    private void UpdateSelectedTownDetail()
    {
        if (_selectedTown == null)
        {
            ClearSelectedTownDetail();
            return;
        }

        if (_selectedTownTitle != null)
            _selectedTownTitle.text = string.IsNullOrWhiteSpace(_selectedTown.title) ? "Town" : _selectedTown.title;
        if (_selectedTownHost != null)
            _selectedTownHost.text = $"Host: {ResolveHostName(_selectedTown)}";
        if (_selectedTownPlayers != null)
            _selectedTownPlayers.text = $"Players: {_selectedTown.memberCount} / {Mathf.Max(2, _selectedTown.maxPlayers)}";
        if (_selectedTownDescription != null)
            _selectedTownDescription.text = BuildTownDescription(_selectedTown);
        if (_selectedTownKey != null)
            _selectedTownKey.text = string.IsNullOrWhiteSpace(_selectedTown.roomId) ? "" : $"Key: {_selectedTown.roomId}";
        if (_joinSelectedTownButton != null)
            _joinSelectedTownButton.interactable = !_busy;
    }

    private void ClearSelectedTownDetail()
    {
        if (_selectedTownTitle != null)
            _selectedTownTitle.text = "Select a Town";
        if (_selectedTownHost != null)
            _selectedTownHost.text = "Host: -";
        if (_selectedTownPlayers != null)
            _selectedTownPlayers.text = "Players: -";
        if (_selectedTownDescription != null)
            _selectedTownDescription.text = "Browse available towns and choose one to see its host, capacity, and current activity.";
        if (_selectedTownKey != null)
            _selectedTownKey.text = "";
        if (_joinSelectedTownButton != null)
            _joinSelectedTownButton.interactable = false;
    }

    private void SetCreateVisibility(bool isPublic)
    {
        _createPublic = isPublic;
        UpdateCreateControls();
    }

    private void SetCreateMaxPlayers(int value)
    {
        _createMaxPlayers = Mathf.Clamp(value, 2, 64);
        UpdateCreateControls();
    }

    private void UpdateCreateControls()
    {
        SetButtonVisual(_privateVisibilityButton, !_createPublic);
        SetButtonVisual(_publicVisibilityButton, _createPublic);

        if (_visibilityHint != null)
        {
            _visibilityHint.text = _createPublic
                ? "Public towns appear in the existing town list and can be joined by other players."
                : "Private towns stay hidden from the list. Share the Town key for direct access.";
        }

        if (_maxPlayerButtons != null)
        {
            foreach (var binding in _maxPlayerButtons)
            {
                if (binding == null)
                    continue;

                var selected = binding.Value == _createMaxPlayers;
                SetMaxPlayerButtonVisual(binding, selected);
            }
        }
    }

    private static void SetMaxPlayerButtonVisual(MaxPlayerButtonBinding binding, bool selected)
    {
        if (binding == null)
            return;

        ApplyOriginalButtonSprite(binding.Button);
        SetCreateButtonSelectionOutline(binding.Button, selected);
        if (binding.Highlight != null)
        {
            binding.Highlight.enabled = selected;
            binding.Highlight.color = selected ? new Color(1f, 0.90f, 0.42f, 0.34f) : CreateClearAccent;
        }
        if (binding.Label != null)
        {
            ApplyPreferredFont(binding.Label);
            binding.Label.color = selected ? CreateSelectedText : CreateIdleText;
            binding.Label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private void SetButtonVisual(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null)
            return;

        ApplyOriginalButtonSprite(button);
        SetCreateButtonSelectionOutline(button, selected);

        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            ApplyPreferredFont(label);
            label.color = selected ? CreateSelectedText : CreateIdleText;
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private static void ApplyOriginalButtonSprite(Button button)
    {
        if (button == null)
            return;

        if (button.targetGraphic != null)
            button.targetGraphic.color = Color.white;

        button.transition = Selectable.Transition.None;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0f;
        button.colors = colors;

        var feedback = button.GetComponent<HomeUIButtonFeedback>();
        if (feedback != null)
            feedback.SetTintOverride(true, Color.white);
    }

    private static void SetCreateButtonSelectionOutline(Button button, bool selected)
    {
        if (button == null)
            return;

        var outline = button.GetComponent<Outline>();
        if (outline == null && selected)
            outline = button.gameObject.AddComponent<Outline>();
        if (outline == null)
            return;

        outline.effectColor = new Color(1f, 0.80f, 0.24f, 1f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);
        outline.useGraphicAlpha = false;
        outline.enabled = selected;
    }

    private void UpdateKeyResult()
    {
        if (_keyResultRoot != null)
            _keyResultRoot.SetActive(_keyResolvedTown != null);

        if (_keyResolvedTown == null)
        {
            if (_joinKeyTownButton != null)
                _joinKeyTownButton.interactable = false;
            return;
        }

        if (_keyResultTitle != null)
            _keyResultTitle.text = string.IsNullOrWhiteSpace(_keyResolvedTown.title) ? "Town" : _keyResolvedTown.title;
        if (_keyResultHost != null)
            _keyResultHost.text = $"Host: {ResolveHostName(_keyResolvedTown)}";
        if (_keyResultPlayers != null)
            _keyResultPlayers.text = $"{_keyResolvedTown.memberCount} / {Mathf.Max(2, _keyResolvedTown.maxPlayers)}";
        if (_keyResultDescription != null)
            _keyResultDescription.text = BuildTownDescription(_keyResolvedTown);
        if (_joinKeyTownButton != null)
            _joinKeyTownButton.interactable = !_busy;
    }

    private void ClearKeyResult()
    {
        _keyResolvedTown = null;
        if (_keyResultRoot != null)
            _keyResultRoot.SetActive(false);
        if (_keyStatus != null)
            _keyStatus.text = "Private towns require a valid key.";
        if (_joinKeyTownButton != null)
            _joinKeyTownButton.interactable = false;
    }

    private bool HasChoicePanel()
    {
        if (_choicePanel != null)
            return true;

        SetStatus("Town 입장 선택 UI가 씬에 연결되지 않았습니다.");
        Debug.LogError("[HomeMapRealmUI] Town entry choice object references are missing. Rebuild/configure Home 1 map UI.");
        return false;
    }

    private void CloseChoicePanel()
    {
        if (_choicePanel != null)
            _choicePanel.SetActive(false);

        SetBusy(false);
        SetStatus(ReadyMessage);
    }

    private void AddInfoRow(string message)
    {
        if (_emptyRoomText == null)
        {
            SetExistingStatus(message);
            return;
        }

        _emptyRoomText.text = message;
        _emptyRoomText.gameObject.SetActive(true);
    }

    private TownRoomRowBinding GetInactiveRoomRow()
    {
        if (_roomRows == null)
            return null;

        foreach (var row in _roomRows)
        {
            if (row?.Root != null && !row.Root.activeSelf)
                return row;
        }

        return null;
    }

    private void ResetRoomRows()
    {
        if (_roomRows != null)
        {
            foreach (var row in _roomRows)
            {
                if (row == null)
                    continue;

                if (row.Button != null)
                    row.Button.onClick.RemoveAllListeners();
                if (row.JoinButton != null)
                    row.JoinButton.onClick.RemoveAllListeners();
                if (row.Title != null)
                    row.Title.text = "";
                if (row.Meta != null)
                    row.Meta.text = "";
                if (row.SteamBadge != null)
                {
                    row.SteamBadge.text = "";
                    row.SteamBadge.gameObject.SetActive(false);
                }
                if (row.SelectedFrame != null)
                    row.SelectedFrame.color = new Color(0f, 0f, 0f, 0f);
                if (row.Root != null)
                    row.Root.SetActive(false);
            }
        }

        if (_emptyRoomText != null)
        {
            _emptyRoomText.text = "";
            _emptyRoomText.gameObject.SetActive(false);
        }
    }

    private bool TryGetSelectedRealmContext(out RealmBinding realm, out string sceneName, out string mapId)
    {
        realm = null;
        sceneName = string.Empty;
        mapId = string.Empty;

        if (_realms == null || _realms.Length == 0)
            return false;

        realm = _realms[_selectedIndex];
        if (!IsRealmAvailable(realm) || !TryGetSceneName(realm, out sceneName) || !TryGetTownMapId(realm, out mapId))
        {
            SetChoiceStatus(MissingMapMessage);
            return false;
        }

        return true;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        SetButtonInteractable(!busy);
        RefreshSelectButton();
        UpdateSelectedTownDetail();
        UpdateKeyResult();
    }

    private void SetChoiceStatus(string message)
    {
        if (_choiceStatus != null)
            _choiceStatus.text = message;

        Debug.Log($"[HomeMapRealmUI] {message}");
    }

    private void SetExistingStatus(string message)
    {
        if (_existingStatus != null)
            _existingStatus.text = message;

        SetChoiceStatus(message);
    }

    private void SetKeyStatus(string message)
    {
        if (_keyStatus != null)
            _keyStatus.text = message;

        SetChoiceStatus(message);
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
            _selectButton.interactable = interactable && IsRealmAvailable(GetRealm(_selectedIndex));
        if (_closeChoiceButton != null)
            _closeChoiceButton.interactable = interactable;
        if (_choiceBackButton != null)
            _choiceBackButton.interactable = interactable;
        if (_choiceExistingButton != null)
            _choiceExistingButton.interactable = interactable;
        if (_choiceCreateButton != null)
            _choiceCreateButton.interactable = interactable;
        if (_choiceKeyButton != null)
            _choiceKeyButton.interactable = interactable;
        if (_choiceOpenSelectedButton != null)
            _choiceOpenSelectedButton.interactable = interactable;
        if (_quickKeyJoinButton != null)
            _quickKeyJoinButton.interactable = interactable;
        if (_quickKeyInput != null)
            _quickKeyInput.interactable = interactable;
        if (_refreshTownRoomsButton != null)
            _refreshTownRoomsButton.interactable = interactable;
        if (_existingBackButton != null)
            _existingBackButton.interactable = interactable;
        if (_townSearchInput != null)
            _townSearchInput.interactable = interactable;
        if (_joinSelectedTownButton != null)
            _joinSelectedTownButton.interactable = interactable && _selectedTown != null;
        if (_privateVisibilityButton != null)
            _privateVisibilityButton.interactable = interactable;
        if (_publicVisibilityButton != null)
            _publicVisibilityButton.interactable = interactable;
        if (_townNameInput != null)
            _townNameInput.interactable = interactable;
        if (_createTownButton != null)
            _createTownButton.interactable = interactable;
        if (_createCancelButton != null)
            _createCancelButton.interactable = interactable;
        if (_inviteCodeInput != null)
            _inviteCodeInput.interactable = interactable;
        if (_joinInviteButton != null)
            _joinInviteButton.interactable = interactable;
        if (_joinKeyTownButton != null)
            _joinKeyTownButton.interactable = interactable && _keyResolvedTown != null;
        if (_clearKeyButton != null)
            _clearKeyButton.interactable = interactable;
        if (_keyCancelButton != null)
            _keyCancelButton.interactable = interactable;
        if (_maxPlayerButtons != null)
        {
            foreach (var binding in _maxPlayerButtons)
            {
                if (binding?.Button != null)
                    binding.Button.interactable = interactable;
            }
        }
        if (_roomRows != null)
        {
            foreach (var row in _roomRows)
            {
                if (row?.Button != null)
                    row.Button.interactable = interactable;
                if (row?.JoinButton != null)
                    row.JoinButton.interactable = interactable;
            }
        }
    }

    private void RefreshSelectButton()
    {
        if (_selectButton == null)
            return;

        _selectButton.interactable = !_busy
            && _realms != null
            && _realms.Length > 0
            && _selectedIndex >= 0
            && _selectedIndex < _realms.Length
            && IsRealmAvailable(_realms[_selectedIndex])
            && TryGetSceneName(_realms[_selectedIndex], out _);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static bool SameRoom(TownRoomApiClient.TownRoomSummaryDto left, TownRoomApiClient.TownRoomSummaryDto right)
    {
        return left != null
               && right != null
               && string.Equals(left.roomId, right.roomId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeKey(string rawCode)
    {
        var code = (rawCode ?? "").Trim();
        if (code.StartsWith("townp2p:", StringComparison.OrdinalIgnoreCase))
            code = code.Substring("townp2p:".Length);

        return code.Replace("-", "").Replace(" ", "").ToLowerInvariant();
    }

    private string GetSelectedRealmName()
    {
        if (_realms == null || _realms.Length == 0 || _selectedIndex < 0 || _selectedIndex >= _realms.Length)
            return "Town";

        return string.IsNullOrWhiteSpace(_realms[_selectedIndex].DisplayName)
            ? "Town"
            : _realms[_selectedIndex].DisplayName;
    }

    private int ResolveInitialRealmIndex()
    {
        if (_realms == null || _realms.Length == 0)
            return 0;

        for (var i = 0; i < _realms.Length; i++)
        {
            if (IsRealmAvailable(_realms[i]))
                return i;
        }

        return 0;
    }

    private RealmBinding GetRealm(int index)
    {
        if (_realms == null || _realms.Length == 0 || index < 0 || index >= _realms.Length)
            return null;

        return _realms[index];
    }

    private void PrepareRealmAvailabilityUi()
    {
        if (_realms == null)
            return;

        foreach (var realm in _realms)
        {
            EnsureUnavailableBadge(realm);
            ApplyRealmAvailability(realm);
        }
    }

    private void ApplyRealmAvailability(RealmBinding realm)
    {
        if (realm?.Button == null)
            return;

        var available = IsRealmAvailable(realm);
        ApplyRealmGraphicState(realm.Button, available);
        ApplyRealmButtonFeedbackState(realm.Button, available);

        var badge = realm.Button.transform.Find("UnavailableBadge");
        if (badge != null)
            badge.gameObject.SetActive(!available);
    }

    private void EnsureUnavailableBadge(RealmBinding realm)
    {
        if (realm?.Button == null)
            return;

        var badgeTransform = realm.Button.transform.Find("UnavailableBadge");
        if (badgeTransform == null)
        {
            var badge = new GameObject("UnavailableBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badge.transform.SetParent(realm.Button.transform, false);
            badgeTransform = badge.transform;
        }

        var rect = (RectTransform)badgeTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(96f, 34f);
        rect.anchoredPosition = Vector2.zero;

        var image = badgeTransform.GetComponent<Image>();
        if (image == null)
            image = badgeTransform.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.13f, 0.09f, 0.92f);
        image.raycastTarget = false;

        var labelTransform = badgeTransform.Find("Label");
        if (labelTransform == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            labelGo.transform.SetParent(badgeTransform, false);
            labelTransform = labelGo.transform;
        }

        var labelRect = (RectTransform)labelTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelTransform.GetComponent<TextMeshProUGUI>();
        if (label == null)
            label = labelTransform.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = "준비 중";
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.82f, 0.52f, 1f);
        label.raycastTarget = false;
        ApplyPreferredFont(label);
    }

    private void ApplyFontToChildren()
    {
        var labels = GetComponentsInChildren<TMP_Text>(true);
        foreach (var label in labels)
            ApplyPreferredFont(label);
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

    private static Color BuildLockedRealmColor(Color source)
    {
        var luminance = source.r * 0.30f + source.g * 0.59f + source.b * 0.11f;
        var value = Mathf.Lerp(luminance, 0.24f, 0.78f);
        return new Color(value * 0.74f, value * 0.72f, value * 0.64f, Mathf.Max(source.a, 0.90f));
    }

    private void ApplyRealmGraphicState(Button button, bool available)
    {
        if (button == null)
            return;

        if (!_realmBaseGraphicColors.TryGetValue(button, out var baseColors))
        {
            baseColors = new Dictionary<Graphic, Color>();
            _realmBaseGraphicColors[button] = baseColors;
        }

        var graphics = button.GetComponentsInChildren<Graphic>(true);
        foreach (var graphic in graphics)
        {
            if (graphic == null || IsUnavailableBadgeGraphic(graphic))
                continue;

            if (!baseColors.TryGetValue(graphic, out var baseColor))
            {
                baseColor = graphic.color;
                baseColors[graphic] = baseColor;
            }

            graphic.color = available ? baseColor : BuildLockedRealmColor(baseColor);
        }
    }

    private void ApplyRealmButtonFeedbackState(Button button, bool available)
    {
        if (button == null)
            return;

        var feedback = button.GetComponent<HomeUIButtonFeedback>();
        if (feedback == null)
            return;

        var baseColor = ResolveRealmTargetBaseColor(button);
        feedback.SetTintOverride(!available, BuildLockedRealmColor(baseColor));
    }

    private Color ResolveRealmTargetBaseColor(Button button)
    {
        if (button == null)
            return Color.white;

        if (_realmBaseGraphicColors.TryGetValue(button, out var baseColors)
            && button.targetGraphic != null
            && baseColors.TryGetValue(button.targetGraphic, out var cachedColor))
        {
            return cachedColor;
        }

        return button.targetGraphic != null ? button.targetGraphic.color : Color.white;
    }

    private static bool IsUnavailableBadgeGraphic(Graphic graphic)
    {
        var current = graphic != null ? graphic.transform : null;
        while (current != null)
        {
            if (string.Equals(current.name, "UnavailableBadge", StringComparison.Ordinal))
                return true;
            current = current.parent;
        }

        return false;
    }

    private static string BuildDefaultTownName(RealmBinding realm)
    {
        var prefix = realm != null && !string.IsNullOrWhiteSpace(realm.DisplayName)
            ? realm.DisplayName
            : "Rhythm";

        if (prefix.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Whispering Nest";
        if (prefix.IndexOf("Plain", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Golden Hearth";

        return $"{prefix} Haven";
    }

    private string ResolveCreateTownTitle(RealmBinding realm)
    {
        var title = (_townNameInput != null ? _townNameInput.text : "").Trim();
        return string.IsNullOrWhiteSpace(title) ? BuildDefaultTownName(realm) : title;
    }

    private static string ResolveHostName(TownRoomApiClient.TownRoomSummaryDto room)
    {
        if (room?.participants != null)
        {
            var host = room.participants.FirstOrDefault(x => string.Equals(x.uid, room.ownerUid, StringComparison.OrdinalIgnoreCase))
                       ?? room.participants.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(host?.name))
                return host.name;
        }

        if (!string.IsNullOrWhiteSpace(room?.hostUid))
            return room.hostUid;
        return !string.IsNullOrWhiteSpace(room?.ownerUid) ? room.ownerUid : "-";
    }

    private static string BuildTownDescription(TownRoomApiClient.TownRoomSummaryDto room)
    {
        if (room == null)
            return "";

        var mapLabel = string.Equals(room.mapId, "Town_Forest", StringComparison.OrdinalIgnoreCase)
            ? "A peaceful woodland village surrounded by mossy trees and soft lantern light."
            : "A traveler-friendly town ready for party setup and rhythm battle preparation.";

        if (!string.IsNullOrWhiteSpace(room.activeGameRoomId))
            return $"{mapLabel}\nActive expedition: {Fallback(room.activeGameTitle, room.activeGameMapId)}";

        return $"{mapLabel}\nFriendly folk welcome travelers and builders alike. A great place to grow together.";
    }

    private static string Fallback(string primary, string fallback)
    {
        return !string.IsNullOrWhiteSpace(primary) ? primary : (!string.IsNullOrWhiteSpace(fallback) ? fallback : "-");
    }

    private static bool TryGetSceneName(RealmBinding realm, out string sceneName)
    {
        sceneName = string.Empty;
        if (realm == null)
            return false;

        if (IsRealmAvailable(realm))
        {
            sceneName = SceneNames.Town_Forest;
            return true;
        }

        return false;
    }

    private static bool TryGetTownMapId(RealmBinding realm, out string mapId)
    {
        mapId = string.Empty;
        if (realm == null)
            return false;

        if (IsRealmAvailable(realm))
        {
            mapId = "Town_Forest";
            return true;
        }

        return false;
    }

    private static string ResolveSceneNameForTownMap(string mapId, string fallbackScene)
    {
        if (IsForestTownMapId(mapId))
            return SceneNames.Town_Forest;

        return string.Equals(fallbackScene, SceneNames.Town_Forest, StringComparison.OrdinalIgnoreCase)
            ? fallbackScene
            : SceneNames.Town_Forest;
    }

    private static bool IsRealmAvailable(RealmBinding realm)
    {
        return IsForestRealm(realm);
    }

    private static bool IsForestRealm(RealmBinding realm)
    {
        return realm != null
               && (string.Equals(realm.RealmId, "forest", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(realm.SceneName, SceneNames.Town_Forest, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsForestTownMapId(string mapId)
    {
        return string.Equals(mapId, "Town_Forest", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mapId, SceneNames.Town_Forest, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class TownRoomSummaryDtoExtensions
{
    public static bool IsListablePublicRoom(this TownRoomApiClient.TownRoomSummaryDto room)
    {
        if (room == null)
            return false;

        return room.isPublic
               && !string.IsNullOrWhiteSpace(room.roomId)
               && !string.Equals(room.status, "Closed", StringComparison.OrdinalIgnoreCase);
    }
}

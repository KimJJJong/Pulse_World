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
    private const string MissingMapMessage = "없는 맵입니다.";
    private const string ReadyMessage = "지역을 선택한 뒤 입장 버튼을 누르세요.";
    private const int DefaultTownMaxPlayers = 4;

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
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Meta;
        public TextMeshProUGUI SteamBadge;
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
    [SerializeField] private Button _createTownButton;
    [SerializeField] private Button _refreshTownRoomsButton;
    [SerializeField] private Button _closeChoiceButton;
    [SerializeField] private TMP_InputField _inviteCodeInput;
    [SerializeField] private Button _joinInviteButton;
    [SerializeField] private TextMeshProUGUI _emptyRoomText;
    [SerializeField] private TownRoomRowBinding[] _roomRows;

    private int _selectedIndex;
    private bool _busy;

    private void Awake()
    {
        BindButtons();
        BindChoiceButtons();
        PrepareRealmHighlights();
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
        BindChoiceButtons();
        PrepareRealmHighlights();
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

        var hasScene = TryGetSceneName(selected, out _);
        if (_ticketInfo != null)
            _ticketInfo.text = hasScene ? $"Ticket: {selected.RequiredTicket}" : "Ticket: 없음";
        if (_status != null && !_busy)
            _status.text = hasScene ? ReadyMessage : MissingMapMessage;

        RefreshSelectButton();

        for (var i = 0; i < _realms.Length; i++)
            ApplyRealmHighlight(_realms[i], i == _selectedIndex);

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
        if (!TryGetSceneName(realm, out var sceneName) || !TryGetTownMapId(realm, out var mapId))
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

        ResetRoomRows();
        _choicePanel.SetActive(true);
        if (_choiceTitle != null)
            _choiceTitle.text = $"{realm.DisplayName} Town";
        if (_inviteCodeInput != null)
            _inviteCodeInput.text = "";
        SetChoiceStatus("새 Town을 만들거나 기존 Town을 찾아 참여하세요.");
        SetStatus($"{realm.DisplayName} 입장 방식 선택 중...");
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
        SetChoiceStatus("Town 방 생성 중...");

        var steam = root.SteamPlatform;
        var title = $"{realm.DisplayName} Town";
        var created = await root.TownRoomApi.CreateAsync(
            title,
            mapId,
            DefaultTownMaxPlayers,
            steam?.SteamId64 ?? "",
            root.Config?.ClientVersion ?? "");

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
            var lobbyId = await steam.CreateLobbyAsync(roomId, title, mapId, DefaultTownMaxPlayers, "town");
            if (!string.IsNullOrWhiteSpace(lobbyId))
            {
                steam.UpdateLobbyMetadata(roomId, title, mapId, DefaultTownMaxPlayers, SessionContext.Instance?.Uid ?? "", "town");
                await root.TownRoomApi.BindSteamLobbyAsync(roomId, lobbyId);
            }
        }

        await EnterTownRoomAsync(roomId, mapId, DefaultTownMaxPlayers, sceneName, "Host");
    }

    private async Task RefreshTownRoomsAsync()
    {
        if (!TryGetSelectedRealmContext(out _, out _, out var mapId))
            return;

        var root = AppBootstrap.Instance?.Root;
        if (root?.TownRoomApi == null)
        {
            SetChoiceStatus("TownRoomApi가 준비되지 않았습니다.");
            return;
        }

        SetBusy(true);
        ResetRoomRows();
        SetChoiceStatus("이전 Town 정보 정리 중...");
        await LeaveStaleTownContextAsync("home_find_town");
        SetChoiceStatus("기존 Town 검색 중...");

        var result = await root.TownRoomApi.ListAsync(mapId);
        if (!result.Ok)
        {
            SetChoiceStatus($"Town 목록 조회 실패: {result.Error}");
            SetBusy(false);
            return;
        }

        var rooms = result.Data?.rooms ?? new List<TownRoomApiClient.TownRoomSummaryDto>();
        var steamRooms = await FindSteamTownLobbiesAsync(root, mapId);
        MergeSteamLobbyHints(rooms, steamRooms);

        if (rooms.Count == 0)
        {
            AddInfoRow("열려 있는 Town이 없습니다.");
            SetChoiceStatus("새 Town을 만들 수 있습니다.");
            SetBusy(false);
            return;
        }

        foreach (var room in rooms.OrderByDescending(x => x.createdAtMs))
            AddRoomRow(room);

        SetChoiceStatus($"{rooms.Count}개의 Town을 찾았습니다.");
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

        _choicePanel?.SetActive(false);
        SetStatus("티켓 확인 완료. Town으로 이동 중...");
        var nonce = $"town-{roomId}-{Guid.NewGuid():N}";
        ClientFlow.Instance.SetTargetTownScene(sceneName);
        await ClientFlow.Instance.ConnectTown(ticket.Data, nonce);
        SetBusy(false);
    }

    private async Task JoinTownAsync(TownRoomApiClient.TownRoomSummaryDto room)
    {
        if (room == null || !TryGetSelectedRealmContext(out _, out var sceneName, out var mapId))
            return;

        var root = AppBootstrap.Instance?.Root;
        if (root?.TownRoomApi == null)
            return;

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

    private async Task JoinTownByInviteCodeAsync()
    {
        var code = (_inviteCodeInput != null ? _inviteCodeInput.text : "").Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            SetChoiceStatus("초대 코드를 입력하세요.");
            return;
        }

        var root = AppBootstrap.Instance?.Root;
        if (root?.TownRoomApi == null)
        {
            SetChoiceStatus("TownRoomApi가 준비되지 않았습니다.");
            return;
        }

        SetBusy(true);
        ResetRoomRows();
        SetChoiceStatus("이전 Town 정보 정리 중...");
        await LeaveStaleTownContextAsync("home_invite_join");

        SetChoiceStatus("초대 코드 확인 중...");
        var roomResult = await root.TownRoomApi.GetAsync(code);
        if (!roomResult.Ok || roomResult.Data == null)
        {
            SetChoiceStatus($"초대 코드 확인 실패: {roomResult.Error}");
            SetBusy(false);
            return;
        }

        var room = roomResult.Data;
        var steam = root.SteamPlatform;
        if (steam != null && steam.Enabled && steam.IsInitialized && !string.IsNullOrWhiteSpace(room.steamLobbyId))
        {
            SetChoiceStatus("Steam Lobby 참여 중...");
            await steam.JoinLobbyAsync(room.steamLobbyId, room.roomId);
        }

        SetChoiceStatus("Town 참여 요청 중...");
        var joined = await root.TownRoomApi.JoinAsync(room.roomId, steam?.SteamId64 ?? "", root.Config?.ClientVersion ?? "");
        if (!joined.Ok)
        {
            SetChoiceStatus($"Town 참여 실패: {joined.Error}");
            SetBusy(false);
            return;
        }

        var fallbackScene = TryGetSelectedRealmContext(out _, out var selectedScene, out _) ? selectedScene : SceneNames.TownMap;
        var sceneName = ResolveSceneNameForTownMap(room.mapId, fallbackScene);
        await EnterTownRoomAsync(room.roomId, room.mapId, Mathf.Max(2, room.maxPlayers), sceneName, IsOwnRoom(room) ? "Host" : "Guest");
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

            if (byRoomId.TryGetValue(lobby.RoomId, out var existing))
            {
                if (string.IsNullOrWhiteSpace(existing.steamLobbyId))
                    existing.steamLobbyId = lobby.LobbyId;
                continue;
            }

            rooms.Add(new TownRoomApiClient.TownRoomSummaryDto
            {
                roomId = lobby.RoomId,
                title = string.IsNullOrWhiteSpace(lobby.Title) ? "Steam Town" : lobby.Title,
                mapId = lobby.MapId,
                maxPlayers = lobby.MaxMembers,
                memberCount = lobby.MemberCount,
                status = "Steam",
                ownerUid = lobby.OwnerUid,
                hostUid = lobby.OwnerUid,
                steamLobbyId = lobby.LobbyId,
                createdAtMs = 0
            });
        }
    }

    private void BindChoiceButtons()
    {
        if (_createTownButton != null)
        {
            _createTownButton.onClick.RemoveAllListeners();
            _createTownButton.onClick.AddListener(() => _ = CreateTownAsync());
        }

        if (_refreshTownRoomsButton != null)
        {
            _refreshTownRoomsButton.onClick.RemoveAllListeners();
            _refreshTownRoomsButton.onClick.AddListener(() => _ = RefreshTownRoomsAsync());
        }

        if (_closeChoiceButton != null)
        {
            _closeChoiceButton.onClick.RemoveAllListeners();
            _closeChoiceButton.onClick.AddListener(CloseChoicePanel);
        }

        if (_joinInviteButton != null)
        {
            _joinInviteButton.onClick.RemoveAllListeners();
            _joinInviteButton.onClick.AddListener(() => _ = JoinTownByInviteCodeAsync());
        }
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

    private void AddRoomRow(TownRoomApiClient.TownRoomSummaryDto room)
    {
        var row = GetInactiveRoomRow();
        if (row == null)
        {
            SetChoiceStatus("표시 가능한 Town 목록 수를 초과했습니다. 새로고침 후 다시 시도하세요.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(room.title) ? "Town" : room.title;
        var isOwnRoom = IsOwnRoom(room);
        if (row.Title != null)
            row.Title.text = isOwnRoom ? $"{title} (내 Town)" : title;
        if (row.Meta != null)
            row.Meta.text = isOwnRoom
                ? $"{room.memberCount}/{room.maxPlayers}  {room.status}  Host"
                : $"{room.memberCount}/{room.maxPlayers}  {room.status}";
        if (row.SteamBadge != null)
        {
            var hasSteamLobby = !string.IsNullOrWhiteSpace(room.steamLobbyId);
            row.SteamBadge.text = hasSteamLobby ? "Steam" : "";
            row.SteamBadge.gameObject.SetActive(hasSteamLobby);
        }

        if (row.Button != null)
        {
            row.Button.onClick.RemoveAllListeners();
            row.Button.onClick.AddListener(() => _ = JoinTownAsync(room));
            row.Button.interactable = !_busy;
        }

        row.Root.SetActive(true);
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

    private void AddInfoRow(string message)
    {
        if (_emptyRoomText == null)
        {
            SetChoiceStatus(message);
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
                if (row.Title != null)
                    row.Title.text = "";
                if (row.Meta != null)
                    row.Meta.text = "";
                if (row.SteamBadge != null)
                {
                    row.SteamBadge.text = "";
                    row.SteamBadge.gameObject.SetActive(false);
                }
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
        if (!TryGetSceneName(realm, out sceneName) || !TryGetTownMapId(realm, out mapId))
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
    }

    private void SetChoiceStatus(string message)
    {
        if (_choiceStatus != null)
            _choiceStatus.text = message;

        Debug.Log($"[HomeMapRealmUI] {message}");
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
        if (_createTownButton != null)
            _createTownButton.interactable = interactable;
        if (_refreshTownRoomsButton != null)
            _refreshTownRoomsButton.interactable = interactable;
        if (_joinInviteButton != null)
            _joinInviteButton.interactable = interactable;
        if (_inviteCodeInput != null)
            _inviteCodeInput.interactable = interactable;
        if (_roomRows != null)
        {
            foreach (var row in _roomRows)
            {
                if (row?.Button != null)
                    row.Button.interactable = interactable;
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
            && TryGetSceneName(_realms[_selectedIndex], out _);
    }

    private static bool TryGetSceneName(RealmBinding realm, out string sceneName)
    {
        sceneName = string.Empty;
        if (realm == null)
            return false;

        if (!string.IsNullOrWhiteSpace(realm.SceneName))
        {
            sceneName = realm.SceneName;
            return true;
        }

        if (string.Equals(realm.RealmId, "plains", StringComparison.OrdinalIgnoreCase))
        {
            sceneName = SceneNames.TownMap;
            return true;
        }

        if (string.Equals(realm.RealmId, "forest", StringComparison.OrdinalIgnoreCase))
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

        if (string.Equals(realm.RealmId, "plains", StringComparison.OrdinalIgnoreCase))
        {
            mapId = "Town_01";
            return true;
        }

        if (string.Equals(realm.RealmId, "forest", StringComparison.OrdinalIgnoreCase))
        {
            mapId = "Town_Forest";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(realm.SceneName))
        {
            mapId = realm.SceneName;
            return true;
        }

        return false;
    }

    private static string ResolveSceneNameForTownMap(string mapId, string fallbackScene)
    {
        if (string.Equals(mapId, "Town_Forest", StringComparison.OrdinalIgnoreCase))
            return SceneNames.Town_Forest;

        if (string.Equals(mapId, "Town_01", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapId, "TownMap", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapId, "town", StringComparison.OrdinalIgnoreCase))
        {
            return SceneNames.TownMap;
        }

        return string.IsNullOrWhiteSpace(fallbackScene) ? SceneNames.TownMap : fallbackScene;
    }
}

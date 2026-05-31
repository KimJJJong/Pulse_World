using System;
using System.Threading;
using System.Threading.Tasks;
using NetClient.Room.UI;
using NetClient.Town;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class TownExpeditionPanel : MonoBehaviour
{
    private const string OverlayCanvasName = "Canvas_TownExpeditionOverlay";
    private const int OverlaySortingOrder = 7000;

    [Header("Runtime UI")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _root;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _detailText;
    [SerializeField] private TMP_Text _readySummaryText;
    [SerializeField] private Button _inventoryButton;
    [SerializeField] private Button _gameSelectButton;
    [SerializeField] private Button _readyWindowButton;
    [SerializeField] private Button _hostStartGameButton;
    [SerializeField] private Button _hostCancelGameButton;
    [SerializeField] private RectTransform _gameSelectWindow;
    [SerializeField] private Button _gameSelectCloseButton;
    [SerializeField] private Button[] _gameOptionButtons;

    [Header("Game Options")]
    [SerializeField] private string[] _gameMapIds =
    {
        "Game_Forest_Tutorial",
        "Game_Forest_01",
        "Game_01",
        "Game"
    };

    [SerializeField] private string[] _gameTitles =
    {
        "Forest Tutorial",
        "Whispering Forest",
        "Game 01",
        "Game"
    };

    [Header("Polling")]
    [SerializeField] private int _pollIntervalMs = 2000;

    private CancellationTokenSource _cts;
    private TownRoomApiClient _townApi;
    private TownRoomApiClient.TownRoomSummaryDto _townRoom;
    private string _townRoomId = "";
    private bool _creatingGameRoom;
    private bool _openingReadyWindow;
    private bool _startingGameRoom;
    private bool _cancelingGameRoom;
    private bool _autoJoiningGameRoom;
    private string _lastAutoJoinRoomId = "";
    private TMP_FontAsset _koreanFont;
    private RoomUiController _subscribedRoomUi;
    private bool _warnedMissingEventSystem;
    private bool _warnedMissingUiReferences;

    public static TownExpeditionPanel EnsureInScene()
    {
        var existing = FindSceneObject<TownExpeditionPanel>();
        if (existing != null)
            return existing;

        Debug.LogError("[TownExpeditionPanel] Scene object is missing. Add Canvas_TownExpeditionOverlay to the scene hierarchy.");
        return null;
    }

    private void Awake()
    {
        _cts = new CancellationTokenSource();
        _townApi = AppBootstrap.Instance?.Root?.TownRoomApi;
        _koreanFont = LoadKoreanFont();
        BindSceneReferencesIfNeeded();
        ConfigureExistingCanvas();
        EnsureUiInputReady();
        BindButtons();
        UpdateView("Town 정보를 불러오는 중...");
    }

    private void Start()
    {
        _ = PollTownRoomLoopAsync(_cts.Token);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();

        if (Input.GetKeyDown(KeyCode.G))
            ShowGameSelectWindow(true);
    }

    private void OnDestroy()
    {
        if (_subscribedRoomUi != null)
        {
            _subscribedRoomUi.RoomSessionClosed -= HandleRoomSessionClosed;
            _subscribedRoomUi.WaitingRoomStateChanged -= HandleWaitingRoomStateChanged;
        }

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch
        {
            // ignored
        }
    }

    private async Task PollTownRoomLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await RefreshTownRoomAsync();

            try
            {
                await Task.Delay(Mathf.Max(500, _pollIntervalMs), token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshTownRoomAsync()
    {
        _townApi ??= AppBootstrap.Instance?.Root?.TownRoomApi;
        if (_townApi == null)
        {
            UpdateView("Town API 준비 중...");
            return;
        }

        _townRoomId = ResolveTownRoomId();
        if (string.IsNullOrWhiteSpace(_townRoomId))
        {
            UpdateView("Town Room 정보를 기다리는 중...");
            return;
        }

        var result = await _townApi.GetAsync(_townRoomId);
        if (!this)
            return;

        if (!result.Ok || result.Data == null)
        {
            UpdateView($"Town Room 조회 실패 ({result.StatusCode})");
            return;
        }

        _townRoom = result.Data;
        UpdateView();
    }

    private void BindButtons()
    {
        if (_inventoryButton)
        {
            _inventoryButton.onClick.RemoveAllListeners();
            _inventoryButton.onClick.AddListener(ToggleInventory);
        }

        if (_gameSelectButton)
        {
            _gameSelectButton.onClick.RemoveAllListeners();
            _gameSelectButton.onClick.AddListener(() => ShowGameSelectWindow(true));
        }

        if (_readyWindowButton)
        {
            _readyWindowButton.onClick.RemoveAllListeners();
            _readyWindowButton.onClick.AddListener(() => _ = HandleReadyButtonAsync());
        }

        if (_hostStartGameButton)
        {
            _hostStartGameButton.onClick.RemoveAllListeners();
            _hostStartGameButton.onClick.AddListener(() => _ = StartActiveGameAsHostAsync());
        }

        if (_hostCancelGameButton)
        {
            _hostCancelGameButton.onClick.RemoveAllListeners();
            _hostCancelGameButton.onClick.AddListener(() => _ = CancelActiveGameAsHostAsync());
        }

        if (_gameSelectCloseButton)
        {
            _gameSelectCloseButton.onClick.RemoveAllListeners();
            _gameSelectCloseButton.onClick.AddListener(() => ShowGameSelectWindow(false));
        }

        BindGameOptionButtons();
    }

    private void BindGameOptionButtons()
    {
        if (_gameOptionButtons == null)
            return;

        for (int i = 0; i < _gameOptionButtons.Length; i++)
        {
            var button = _gameOptionButtons[i];
            if (!button)
                continue;

            int index = i;
            var mapId = index < _gameMapIds.Length ? _gameMapIds[index] ?? "" : "";
            var title = index < _gameTitles.Length ? _gameTitles[index] ?? mapId : mapId;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _ = CreateGameRoomForMapAsync(mapId, title));
        }
    }

    private void UpdateView(string overrideStatus = "")
    {
        BindSceneReferencesIfNeeded();

        var hasRoom = _townRoom != null && !string.IsNullOrWhiteSpace(_townRoom.roomId);
        var isHost = IsTownHost(_townRoom);
        var hasActiveGame = hasRoom && !string.IsNullOrWhiteSpace(_townRoom.activeGameRoomId);
        var activeGameRoomId = hasActiveGame ? _townRoom.activeGameRoomId : "";
        if (hasActiveGame)
            RequestAutoJoinActiveGameRoom(activeGameRoomId);

        var activeRoomUi = hasActiveGame ? FindActiveGameRoomUi(_townRoom.activeGameRoomId) : null;
        var hasReadySnapshot = activeRoomUi != null && activeRoomUi.IsConnectedToRoom;

        if (_titleText)
            _titleText.text = isHost ? "Town Host" : "Town Party";

        if (_statusText)
        {
            if (!string.IsNullOrWhiteSpace(overrideStatus))
                _statusText.text = overrideStatus;
            else if (!hasRoom)
                _statusText.text = "Town Room 정보를 기다리는 중...";
            else if (hasActiveGame)
            {
                if (hasReadySnapshot && isHost)
                    _statusText.text = activeRoomUi.CanOwnerStartGame ? "모두 준비됨 - 시작 가능" : "참가자 Ready 대기 중";
                else if (hasReadySnapshot)
                    _statusText.text = activeRoomUi.AmIReady ? "준비 완료 - Host 시작 대기" : "준비가 필요합니다.";
                else
                    _statusText.text = isHost
                        ? $"{SafeTitle(_townRoom.activeGameTitle, _townRoom.activeGameMapId)} 시작 대기 중"
                        : $"{SafeTitle(_townRoom.activeGameTitle, _townRoom.activeGameMapId)} 준비 가능";
            }
            else if (isHost)
                _statusText.text = "Game을 선택해 대기방을 만들 수 있습니다.";
            else
                _statusText.text = "Host의 Game 선택을 기다리는 중입니다.";
        }

        if (_detailText)
        {
            if (!hasRoom)
            {
                _detailText.text = "Town 연결 후 Inventory와 Game 준비 상태가 표시됩니다.";
            }
            else if (hasActiveGame)
            {
                _detailText.text =
                    $"방: {_townRoom.activeGameRoomId}\n" +
                    $"Map: {SafeTitle(_townRoom.activeGameTitle, _townRoom.activeGameMapId)}\n" +
                    $"참여: {_townRoom.memberCount}/{_townRoom.maxPlayers}";
            }
            else
            {
                _detailText.text =
                    $"Town: {_townRoom.title}\n" +
                    $"참여: {_townRoom.memberCount}/{_townRoom.maxPlayers}\n" +
                    $"Host: {TrimUid(_townRoom.ownerUid)}";
            }
        }

        if (_readySummaryText)
        {
            _readySummaryText.gameObject.SetActive(hasActiveGame);
            _readySummaryText.text = BuildReadySummary(activeRoomUi, hasActiveGame, isHost);
        }

        if (_gameSelectButton)
        {
            _gameSelectButton.gameObject.SetActive(isHost);
            _gameSelectButton.interactable = isHost && !hasActiveGame && !_creatingGameRoom;
        }

        if (_readyWindowButton)
        {
            _readyWindowButton.gameObject.SetActive(hasActiveGame);
            _readyWindowButton.interactable = hasActiveGame && !_openingReadyWindow;
            var readyLabel = isHost
                ? "대기방 보기"
                : (hasReadySnapshot && activeRoomUi.AmIReady ? "준비 취소" : "준비하기");
            if (_openingReadyWindow)
                readyLabel = "연결 중";
            SetButtonLabel(_readyWindowButton, readyLabel);
        }

        if (_hostStartGameButton)
        {
            _hostStartGameButton.gameObject.SetActive(isHost && hasActiveGame);
            _hostStartGameButton.interactable = isHost
                                               && hasActiveGame
                                               && hasReadySnapshot
                                               && activeRoomUi.CanOwnerStartGame
                                               && !_startingGameRoom
                                               && !_cancelingGameRoom;
            SetButtonLabel(_hostStartGameButton, hasReadySnapshot && activeRoomUi.CanOwnerStartGame ? "게임 시작" : "Ready 대기");
        }

        if (_hostCancelGameButton)
        {
            _hostCancelGameButton.gameObject.SetActive(isHost && hasActiveGame);
            _hostCancelGameButton.interactable = isHost && hasActiveGame && !_startingGameRoom && !_cancelingGameRoom;
        }
    }

    private async Task CreateGameRoomForMapAsync(string mapId, string title)
    {
        if (_creatingGameRoom)
            return;

        _townRoomId = ResolveTownRoomId();
        if (string.IsNullOrWhiteSpace(_townRoomId) || !IsTownHost(_townRoom))
        {
            ShowGameSelectWindow(false);
            UpdateView("Host만 Game 대기방을 만들 수 있습니다.");
            return;
        }

        var roomUi = EnsureRoomUiController();
        if (roomUi == null)
        {
            UpdateView("Room UI를 찾을 수 없습니다.");
            return;
        }

        _creatingGameRoom = true;
        UpdateView("Game 대기방 생성 중...");

        try
        {
            ShowGameSelectWindow(false);
            roomUi.OpenRoot(showList: false);

            var maxPlayers = Mathf.Clamp(_townRoom != null && _townRoom.maxPlayers > 0 ? _townRoom.maxPlayers : 4, 1, 50);
            var roomId = await roomUi.CreateAndJoinRoomAsync("", title, mapId, maxPlayers, relayMode: true);
            if (!this)
                return;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                UpdateView("Game 대기방 생성 실패");
                return;
            }

            var result = await _townApi.SetActiveGameRoomAsync(_townRoomId, roomId, mapId, title);
            if (!this)
                return;

            if (!result.Ok)
            {
                UpdateView($"Town에 Game 방 공유 실패 ({result.StatusCode})");
                return;
            }

            if (result.Data?.room != null)
                _townRoom = result.Data.room;

            UpdateView();
        }
        finally
        {
            if (this)
            {
                _creatingGameRoom = false;
                UpdateView();
            }
        }
    }

    private async Task HandleReadyButtonAsync()
    {
        if (_openingReadyWindow)
            return;

        var activeGameRoomId = _townRoom?.activeGameRoomId ?? "";
        if (string.IsNullOrWhiteSpace(activeGameRoomId))
        {
            UpdateView("아직 열린 Game 대기방이 없습니다.");
            return;
        }

        var roomUi = EnsureRoomUiController();
        if (roomUi == null)
        {
            UpdateView("Room UI를 찾을 수 없습니다.");
            return;
        }

        _openingReadyWindow = true;
        UpdateView(IsTownHost(_townRoom) ? "대기방을 여는 중..." : "준비 상태 적용 중...");

        try
        {
            var isHost = IsTownHost(_townRoom);
            await EnsureJoinedActiveGameRoomAsync(roomUi, activeGameRoomId, showUi: isHost);
            if (!isHost)
            {
                var nextReady = !roomUi.AmIReady;
                await roomUi.SetReadyAsync(nextReady);
                UpdateView(nextReady ? "준비 완료를 전송했습니다." : "준비 취소를 전송했습니다.");
            }
        }
        finally
        {
            if (this)
            {
                _openingReadyWindow = false;
                UpdateView();
            }
        }
    }

    private async Task StartActiveGameAsHostAsync()
    {
        if (_startingGameRoom)
            return;

        var activeGameRoomId = _townRoom?.activeGameRoomId ?? "";
        if (string.IsNullOrWhiteSpace(activeGameRoomId))
        {
            UpdateView("시작할 Game 대기방이 없습니다.");
            return;
        }

        if (!IsTownHost(_townRoom))
        {
            UpdateView("Host만 Game을 시작할 수 있습니다.");
            return;
        }

        var roomUi = EnsureRoomUiController();
        if (roomUi == null)
        {
            UpdateView("Room UI를 찾을 수 없습니다.");
            return;
        }

        _startingGameRoom = true;
        UpdateView("Game 시작 요청 중...");

        try
        {
            roomUi.OpenRoot(showList: false);
            await EnsureJoinedActiveGameRoomAsync(roomUi, activeGameRoomId, showUi: true);

            if (!roomUi.CanOwnerStartGame)
            {
                UpdateView(roomUi.ReadySummaryText);
                return;
            }

            await roomUi.StartGameAsync();
            UpdateView("Game 시작 요청을 보냈습니다.");
        }
        finally
        {
            if (this)
            {
                _startingGameRoom = false;
                UpdateView();
            }
        }
    }

    private async Task CancelActiveGameAsHostAsync()
    {
        if (_cancelingGameRoom)
            return;

        var activeGameRoomId = _townRoom?.activeGameRoomId ?? "";
        if (string.IsNullOrWhiteSpace(activeGameRoomId))
        {
            UpdateView("취소할 Game 대기방이 없습니다.");
            return;
        }

        if (!IsTownHost(_townRoom))
        {
            UpdateView("Host만 Game 대기방을 취소할 수 있습니다.");
            return;
        }

        _cancelingGameRoom = true;
        UpdateView("Game 대기방 취소 중...");

        try
        {
            var roomUi = RoomUiController.ActiveInstance ?? FindSceneObject<RoomUiController>();
            if (roomUi != null && string.Equals(roomUi.CurrentRoomId, activeGameRoomId, StringComparison.OrdinalIgnoreCase))
                await roomUi.LeaveCurrentRoomAsync(showListAfter: false);

            await ClearActiveGameRoomAsync(activeGameRoomId);
            UpdateView("Game 대기방을 취소했습니다.");
        }
        finally
        {
            if (this)
            {
                _cancelingGameRoom = false;
                UpdateView();
            }
        }
    }

    private void ToggleInventory()
    {
        var inventory = FindSceneObject<TownInventoryUI>();
        if (inventory == null)
        {
            UpdateView("Inventory UI를 찾을 수 없습니다.");
            return;
        }

        inventory.ToggleInventory();
    }

    private RoomUiController EnsureRoomUiController()
    {
        var active = RoomUiController.ActiveInstance;
        if (active != null)
        {
            SubscribeRoomUi(active);
            return active;
        }

        var existing = FindSceneObject<RoomUiController>();
        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
                existing.gameObject.SetActive(true);
            SubscribeRoomUi(existing);
            return existing;
        }

        var prefab = Resources.Load<GameObject>("RoomUIRoot");
        if (prefab == null)
            return null;

        var instance = Instantiate(prefab);
        instance.name = "RoomUIRoot";
        if (!instance.activeSelf)
            instance.SetActive(true);

        var controller = instance.GetComponentInChildren<RoomUiController>(true);
        SubscribeRoomUi(controller);
        return controller;
    }

    private void SubscribeRoomUi(RoomUiController roomUi)
    {
        if (roomUi == null || _subscribedRoomUi == roomUi)
            return;

        if (_subscribedRoomUi != null)
        {
            _subscribedRoomUi.RoomSessionClosed -= HandleRoomSessionClosed;
            _subscribedRoomUi.WaitingRoomStateChanged -= HandleWaitingRoomStateChanged;
        }

        _subscribedRoomUi = roomUi;
        _subscribedRoomUi.RoomSessionClosed += HandleRoomSessionClosed;
        _subscribedRoomUi.WaitingRoomStateChanged += HandleWaitingRoomStateChanged;
    }

    private void HandleWaitingRoomStateChanged()
    {
        if (this)
            UpdateView();
    }

    private void HandleRoomSessionClosed(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)
            || _townRoom == null
            || !IsTownHost(_townRoom)
            || !string.Equals(roomId, _townRoom.activeGameRoomId, StringComparison.OrdinalIgnoreCase))
            return;

        _ = ClearActiveGameRoomAsync(roomId);
    }

    private async Task ClearActiveGameRoomAsync(string gameRoomId)
    {
        if (_townApi == null || string.IsNullOrWhiteSpace(_townRoomId))
            return;

        var result = await _townApi.ClearActiveGameRoomAsync(_townRoomId);
        if (!this || !result.Ok)
            return;

        if (result.Data?.room != null)
            _townRoom = result.Data.room;
        else if (_townRoom != null && string.Equals(_townRoom.activeGameRoomId, gameRoomId, StringComparison.OrdinalIgnoreCase))
            _townRoom.activeGameRoomId = "";

        UpdateView();
    }

    private void RequestAutoJoinActiveGameRoom(string activeGameRoomId)
    {
        if (string.IsNullOrWhiteSpace(activeGameRoomId) || _autoJoiningGameRoom)
            return;

        var roomUi = RoomUiController.ActiveInstance ?? FindSceneObject<RoomUiController>();
        if (roomUi != null
            && roomUi.IsConnectedToRoom
            && string.Equals(roomUi.CurrentRoomId, activeGameRoomId, StringComparison.OrdinalIgnoreCase))
        {
            _lastAutoJoinRoomId = activeGameRoomId;
            SubscribeRoomUi(roomUi);
            return;
        }

        if (string.Equals(_lastAutoJoinRoomId, activeGameRoomId, StringComparison.OrdinalIgnoreCase))
            return;

        _autoJoiningGameRoom = true;
        _lastAutoJoinRoomId = activeGameRoomId;
        _ = AutoJoinActiveGameRoomAsync(activeGameRoomId);
    }

    private async Task AutoJoinActiveGameRoomAsync(string activeGameRoomId)
    {
        try
        {
            var roomUi = EnsureRoomUiController();
            if (roomUi == null)
                return;

            await EnsureJoinedActiveGameRoomAsync(roomUi, activeGameRoomId, showUi: false);
        }
        finally
        {
            if (this)
            {
                var roomUi = RoomUiController.ActiveInstance ?? FindSceneObject<RoomUiController>();
                if (roomUi == null
                    || !roomUi.IsConnectedToRoom
                    || !string.Equals(roomUi.CurrentRoomId, activeGameRoomId, StringComparison.OrdinalIgnoreCase))
                {
                    _lastAutoJoinRoomId = "";
                }

                _autoJoiningGameRoom = false;
                UpdateView();
            }
        }
    }

    private async Task EnsureJoinedActiveGameRoomAsync(RoomUiController roomUi, string activeGameRoomId, bool showUi)
    {
        if (roomUi == null || string.IsNullOrWhiteSpace(activeGameRoomId))
            return;

        if (showUi)
            roomUi.OpenRoot(showList: false);

        if (!string.Equals(roomUi.CurrentRoomId, activeGameRoomId, StringComparison.OrdinalIgnoreCase)
            || !roomUi.IsConnectedToRoom)
        {
            await roomUi.JoinRoomByIdAsync(activeGameRoomId, showUi);
        }
        else if (showUi)
        {
            roomUi.OpenRoot(showList: false);
        }

        await WaitForRoomUiSnapshotAsync(roomUi, activeGameRoomId);
    }

    private string ResolveTownRoomId()
    {
        var manifestRoomId = SessionContext.Instance?.LastMatchManifest?.RoomId ?? "";
        if (!string.IsNullOrWhiteSpace(manifestRoomId))
            return manifestRoomId;

        var sessionKey = SessionContext.Instance?.Key ?? "";
        var parsed = ParseTownRelayRoomId(sessionKey);
        if (!string.IsNullOrWhiteSpace(parsed))
            return parsed;

        if (P2PRelayClientBridge.HasInstance)
            return ParseTownRelayRoomId(P2PRelayClientBridge.Instance.RelayKey);

        return "";
    }

    private static string ParseTownRelayRoomId(string key)
    {
        const string prefix = "townp2p:";
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return "";

        return key.Substring(prefix.Length);
    }

    private static bool IsTownHost(TownRoomApiClient.TownRoomSummaryDto room)
    {
        var uid = SessionContext.Instance?.Uid ?? "";
        if (room != null && !string.IsNullOrWhiteSpace(uid)
            && string.Equals(room.ownerUid, uid, StringComparison.OrdinalIgnoreCase))
            return true;

        var manifest = SessionContext.Instance?.LastMatchManifest;
        if (manifest != null
            && !string.IsNullOrWhiteSpace(uid)
            && !string.IsNullOrWhiteSpace(manifest.HostUid)
            && string.Equals(manifest.HostUid, uid, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(manifest.NetworkMode)
            && manifest.NetworkMode.IndexOf("town", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return P2PRelayClientBridge.HasInstance
               && P2PRelayClientBridge.Instance.IsTownRelayMode
               && P2PRelayClientBridge.Instance.IsHostLocal;
    }

    private void BindSceneReferencesIfNeeded()
    {
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>(true);

        if (_root == null)
            _root = FindChild<RectTransform>("TownExpeditionInfo");
        if (_titleText == null)
            _titleText = FindChild<TMP_Text>("Title");
        if (_statusText == null)
            _statusText = FindChild<TMP_Text>("Status");
        if (_detailText == null)
            _detailText = FindChild<TMP_Text>("Detail");
        if (_readySummaryText == null)
            _readySummaryText = FindChild<TMP_Text>("ReadySummary");
        if (_inventoryButton == null)
            _inventoryButton = FindChild<Button>("InventoryButton");
        if (_gameSelectButton == null)
            _gameSelectButton = FindChild<Button>("GameSelectButton");
        if (_readyWindowButton == null)
            _readyWindowButton = FindChild<Button>("ReadyWindowButton");
        if (_hostStartGameButton == null)
            _hostStartGameButton = FindChild<Button>("HostStartGameButton");
        if (_hostCancelGameButton == null)
            _hostCancelGameButton = FindChild<Button>("HostCancelGameButton");
        if (_gameSelectWindow == null)
            _gameSelectWindow = FindChild<RectTransform>("TownGameSelectWindow");
        if (_gameSelectCloseButton == null)
            _gameSelectCloseButton = FindChild<Button>("CloseButton");
        if (_gameOptionButtons == null || _gameOptionButtons.Length == 0)
            _gameOptionButtons = FindGameOptionButtons();

        ApplyFontToChildren();

        if (!_warnedMissingUiReferences && (_root == null || _inventoryButton == null || _gameSelectButton == null || _gameSelectWindow == null))
        {
            _warnedMissingUiReferences = true;
            Debug.LogError("[TownExpeditionPanel] Scene UI references are incomplete. Rebuild Canvas_TownExpeditionOverlay in the hierarchy.");
        }
    }

    private RoomUiController FindActiveGameRoomUi(string gameRoomId)
    {
        if (string.IsNullOrWhiteSpace(gameRoomId))
            return null;

        var roomUi = RoomUiController.ActiveInstance ?? FindSceneObject<RoomUiController>();
        if (roomUi == null)
            return null;

        SubscribeRoomUi(roomUi);
        return string.Equals(roomUi.CurrentRoomId, gameRoomId, StringComparison.OrdinalIgnoreCase) ? roomUi : null;
    }

    private static string BuildReadySummary(RoomUiController roomUi, bool hasActiveGame, bool isHost)
    {
        if (!hasActiveGame)
            return "";

        if (roomUi == null || !roomUi.IsConnectedToRoom)
            return isHost
                ? "참가자 준비: 대기방 연결 필요\n대기방 보기로 상태를 확인하세요."
                : "참가자 준비: 대기방 연결 필요\n준비하기를 눌러 상태를 확인하세요.";

        return roomUi.ReadySummaryText;
    }

    private async Task WaitForRoomUiSnapshotAsync(RoomUiController roomUi, string roomId)
    {
        if (roomUi == null || string.IsNullOrWhiteSpace(roomId))
            return;

        var deadline = Time.realtimeSinceStartup + 1.5f;
        while (this
               && Time.realtimeSinceStartup < deadline
               && (!roomUi.IsConnectedToRoom || !string.Equals(roomUi.CurrentRoomId, roomId, StringComparison.OrdinalIgnoreCase)))
        {
            await Task.Delay(100);
        }
    }

    private void ConfigureExistingCanvas()
    {
        if (_canvas == null)
            return;

        _canvas.overrideSorting = true;
        _canvas.sortingOrder = Mathf.Max(_canvas.sortingOrder, OverlaySortingOrder);
        var raycaster = _canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = true;
    }

    private void EnsureUiInputReady()
    {
        ConfigureExistingCanvas();

        var eventSystem = FindSceneObject<EventSystem>();
        if (eventSystem == null)
        {
            if (!_warnedMissingEventSystem)
            {
                _warnedMissingEventSystem = true;
                Debug.LogError("[TownExpeditionPanel] EventSystem is missing from the scene hierarchy.");
            }

            return;
        }

        eventSystem.enabled = true;
        var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule != null)
            inputSystemModule.enabled = true;

        var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null && inputSystemModule != null)
            standaloneModule.enabled = false;
    }

    private void ApplyFontToChildren()
    {
        if (_koreanFont == null)
            return;

        var labels = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].font = _koreanFont;
            labels[i].fontSharedMaterial = _koreanFont.material;
        }
    }

    private T FindChild<T>(string objectName) where T : Component
    {
        var components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && string.Equals(components[i].gameObject.name, objectName, StringComparison.Ordinal))
                return components[i];
        }

        return null;
    }

    private Button[] FindGameOptionButtons()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        var result = new Button[_gameMapIds.Length];
        for (int i = 0; i < _gameMapIds.Length; i++)
        {
            var expectedName = $"GameOption_{_gameMapIds[i]}";
            for (int j = 0; j < buttons.Length; j++)
            {
                if (buttons[j] != null && string.Equals(buttons[j].gameObject.name, expectedName, StringComparison.Ordinal))
                {
                    result[i] = buttons[j];
                    break;
                }
            }
        }

        return result;
    }

    private void ShowGameSelectWindow(bool show)
    {
        BindSceneReferencesIfNeeded();
        if (_gameSelectWindow)
        {
            if (show)
                _gameSelectWindow.SetAsLastSibling();

            _gameSelectWindow.gameObject.SetActive(show);
        }
    }

    private static TMP_FontAsset LoadKoreanFont()
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NanumGothic SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("NanumGothic SDF");
        return font;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (!button)
            return;

        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text)
            text.text = label;
    }

    private static string SafeTitle(string title, string fallback)
    {
        return !string.IsNullOrWhiteSpace(title) ? title : fallback ?? "";
    }

    private static string TrimUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return "-";
        return uid.Length <= 10 ? uid : uid.Substring(0, 10);
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        var objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null)
                continue;

            if (obj is Component component && component.gameObject.scene.IsValid())
                return obj;

            if (obj is GameObject go && go.scene.IsValid())
                return obj;
        }

        return null;
    }
}

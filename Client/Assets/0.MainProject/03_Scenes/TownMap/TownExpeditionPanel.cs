using System;
using System.Collections.Generic;
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
    private const int OverlaySortingOrder = 7000;

    [Serializable]
    public sealed class PartySlotBinding
    {
        public GameObject Root;
        public TMP_Text IndexText;
        public TMP_Text NameText;
        public TMP_Text HpText;
        public TMP_Text HostBadgeText;
        public TMP_Text ReadyText;
        public Graphic ReadyGraphic;
        public Graphic LocalFrame;
        public Slider HpSlider;
    }

    [Serializable]
    public sealed class MapOptionBinding
    {
        public Button Button;
        public TMP_Text TitleText;
        public TMP_Text DescriptionText;
        public TMP_Text DifficultyText;
        public TMP_Text MetaText;
        public Graphic SelectedFrame;
    }

    private sealed class PartyMemberView
    {
        public string Uid = "";
        public string Name = "";
        public bool IsHost;
        public bool IsLocal;
        public bool Connected = true;
        public bool Ready = true;
    }

    [Header("Runtime UI")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _root;
    [SerializeField] private TMP_Text _topTownTitleText;
    [SerializeField] private TMP_Text _topStatusText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _roleBadgeText;
    [SerializeField] private TMP_Text _partyCountText;
    [SerializeField] private TMP_Text _selectedMapTitleText;
    [SerializeField] private TMP_Text _selectedMapMetaText;
    [SerializeField] private TMP_Text _selectedMapDifficultyText;
    [SerializeField] private TMP_Text _selectedMapGoalText;
    [SerializeField] private TMP_Text _inviteCodeText;
    [SerializeField] private TMP_Text _clientHintText;
    [SerializeField] private TMP_Text _minimapCountText;
    [SerializeField] private TMP_Text _readySummaryText;
    [SerializeField] private RectTransform _partyPanelRoot;
    [SerializeField] private RectTransform _partyPanelBodyRoot;
    [SerializeField] private TMP_Text _partyMinimizeLabelText;
    [SerializeField] private RectTransform _hostControlsRoot;
    [SerializeField] private RectTransform _clientControlsRoot;
    [SerializeField] private RectTransform _gameSelectWindow;
    [SerializeField] private RectTransform _mapInfoWindow;
    [SerializeField] private TMP_Text _mapInfoTitleText;
    [SerializeField] private TMP_Text _mapInfoDescriptionText;
    [SerializeField] private TMP_Text _mapInfoStatsText;
    [SerializeField] private TMP_Text[] _mapInfoFeatureTexts;
    [SerializeField] private PartySlotBinding[] _partySlots;
    [SerializeField] private PartySlotBinding[] _sidePartySlots;
    [SerializeField] private MapOptionBinding[] _mapOptions;
    [SerializeField] private Button _inventoryButton;
    [SerializeField] private Button _gameSelectButton;
    [SerializeField] private Button _mapSelectConfirmButton;
    [SerializeField] private Button _mapSelectPartyButton;
    [SerializeField] private Button _gameSelectCloseButton;
    [SerializeField] private Button _mapInfoButton;
    [SerializeField] private Button _mapInfoCloseButton;
    [SerializeField] private Button _copyInviteButton;
    [SerializeField] private Button _partyMinimizeButton;
    [SerializeField] private Button _readyWindowButton;
    [SerializeField] private Button _hostStartGameButton;
    [SerializeField] private Button _hostCancelGameButton;
    [SerializeField] private Button _partyManageButton;
    [SerializeField] private TownHomeUiController _homeUiController;

    [Header("Game Options")]
    [SerializeField] private string[] _gameMapIds =
    {
        "Game_Forest_First_Step",
        "Game_Forest_Tutorial",
        "Game_Forest_01",
        "Game_01"
    };

    [SerializeField] private string[] _gameTitles =
    {
        "포레스트 첫걸음",
        "포레스트 튜토리얼",
        "위스퍼링 포레스트",
        "크리스탈 카번"
    };

    [SerializeField] private string[] _gameDescriptions =
    {
        "첫 전투에 진입하기 전 리듬과 이동 흐름을 짧게 확인하는 시작 맵입니다.",
        "깊고 울창한 포레스트에서 기본 조작을 익힐 수 있는 입문 맵입니다.",
        "몬스터가 숨어 있는 숲길을 따라 전투 흐름을 익히는 맵입니다.",
        "푸른 크리스탈이 빛나는 동굴에서 강한 적을 상대합니다."
    };

    [SerializeField] private string[] _gameDifficultyLabels =
    {
        "입문",
        "쉬움",
        "보통",
        "어려움"
    };

    [SerializeField] private string[] _gamePlayerLabels =
    {
        "1~4명",
        "1~4명",
        "1~4명",
        "2~4명"
    };

    [SerializeField] private string[] _gameTimeLabels =
    {
        "3~5분",
        "5~10분",
        "10~15분",
        "15~20분"
    };

    [SerializeField] private string[] _gameGoalLabels =
    {
        "첫 전투 완료",
        "모든 적 처치",
        "숲의 균열 정리",
        "크리스탈 수호자 처치"
    };

    [Header("Polling")]
    [SerializeField] private int _pollIntervalMs = 500;

    private CancellationTokenSource _cts;
    private TownRoomApiClient _townApi;
    private TownRoomApiClient.TownRoomSummaryDto _townRoom;
    private string _townRoomId = "";
    private int _selectedGameMapIndex;
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
    private float _nextLiveUiRefreshAt;
    private bool _partyPanelCollapsed;

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
        InitializeMapOptionLabels();
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
            ToggleHomeUiOrInventory();

        if (Input.GetKeyDown(KeyCode.G))
            ShowGameSelectWindow(true);

        if (Input.GetKeyDown(KeyCode.M))
            ShowMapInfoWindow(true);

        if (Time.unscaledTime >= _nextLiveUiRefreshAt)
        {
            _nextLiveUiRefreshAt = Time.unscaledTime + 0.25f;
            if (_townRoom != null
                && !_creatingGameRoom
                && !_openingReadyWindow
                && !_startingGameRoom
                && !_cancelingGameRoom)
            {
                var roomUi = RoomUiController.ActiveInstance ?? FindSceneObject<RoomUiController>();
                if (roomUi != null && roomUi.IsConnectedToRoom)
                    UpdateView();
            }
        }
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

    private async Task<bool> RefreshTownRoomAsync()
    {
        _townApi ??= AppBootstrap.Instance?.Root?.TownRoomApi;
        if (_townApi == null)
        {
            UpdateView("Town API 준비 중...");
            return false;
        }

        _townRoomId = ResolveTownRoomId();
        if (string.IsNullOrWhiteSpace(_townRoomId))
        {
            UpdateView("Town Room 정보를 기다리는 중...");
            return false;
        }

        var result = await _townApi.GetAsync(_townRoomId);
        if (!this)
            return false;

        if (!result.Ok || result.Data == null)
        {
            UpdateView($"Town Room 조회 실패 ({result.StatusCode})");
            return false;
        }

        _townRoom = result.Data;
        if (!string.IsNullOrWhiteSpace(_townRoom.activeGameMapId))
            _selectedGameMapIndex = ResolveMapIndex(_townRoom.activeGameMapId);

        UpdateView();
        return true;
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

        if (_mapSelectConfirmButton)
        {
            _mapSelectConfirmButton.onClick.RemoveAllListeners();
            _mapSelectConfirmButton.onClick.AddListener(() => _ = ConfirmSelectedMapSelectionAsync());
        }

        if (_mapSelectPartyButton)
        {
            _mapSelectPartyButton.onClick.RemoveAllListeners();
            _mapSelectPartyButton.onClick.AddListener(() => _ = OpenPartyManageAsync());
        }

        if (_gameSelectCloseButton)
        {
            _gameSelectCloseButton.onClick.RemoveAllListeners();
            _gameSelectCloseButton.onClick.AddListener(() => ShowGameSelectWindow(false));
        }

        if (_mapInfoButton)
        {
            _mapInfoButton.onClick.RemoveAllListeners();
            _mapInfoButton.onClick.AddListener(() => ShowMapInfoWindow(true));
        }

        if (_mapInfoCloseButton)
        {
            _mapInfoCloseButton.onClick.RemoveAllListeners();
            _mapInfoCloseButton.onClick.AddListener(() => ShowMapInfoWindow(false));
        }

        if (_copyInviteButton)
        {
            _copyInviteButton.onClick.RemoveAllListeners();
            _copyInviteButton.onClick.AddListener(CopyInviteCode);
        }

        if (_partyMinimizeButton)
        {
            _partyMinimizeButton.onClick.RemoveAllListeners();
            _partyMinimizeButton.onClick.AddListener(TogglePartyPanelCollapsed);
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

        if (_partyManageButton)
        {
            _partyManageButton.onClick.RemoveAllListeners();
            _partyManageButton.onClick.AddListener(() => _ = OpenPartyManageAsync());
        }

        if (_mapOptions != null)
        {
            for (int i = 0; i < _mapOptions.Length; i++)
            {
                var option = _mapOptions[i];
                if (option?.Button == null)
                    continue;

                int index = i;
                option.Button.onClick.RemoveAllListeners();
                option.Button.onClick.AddListener(() => SelectMapOption(index));
            }
        }
    }

    private void InitializeMapOptionLabels()
    {
        if (_mapOptions == null)
            return;

        for (int i = 0; i < _mapOptions.Length; i++)
        {
            var option = _mapOptions[i];
            if (option == null)
                continue;

            if (option.TitleText)
                option.TitleText.text = GetMapTitle(i);
            if (option.DescriptionText)
                option.DescriptionText.text = GetMapDescription(i);
            if (option.DifficultyText)
                option.DifficultyText.text = GetMapDifficulty(i);
            if (option.MetaText)
                option.MetaText.text = $"{GetMapPlayers(i)}  |  {GetMapTime(i)}";
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
        if (hasReadySnapshot)
            ConfigureTownRequiredStartMembers(activeRoomUi);
        var displayMapIndex = hasActiveGame && !string.IsNullOrWhiteSpace(_townRoom.activeGameMapId)
            ? ResolveMapIndex(_townRoom.activeGameMapId)
            : _selectedGameMapIndex;

        SetActive(_hostControlsRoot, isHost);
        SetActive(_clientControlsRoot, !isHost);

        if (_topTownTitleText)
            _topTownTitleText.text = ResolveTownTitle();

        if (_roleBadgeText)
            _roleBadgeText.text = isHost ? "HOST" : "CLIENT";

        if (_partyCountText)
        {
            var memberCount = hasReadySnapshot
                ? Mathf.Max(0, activeRoomUi.MemberCount)
                : (hasRoom ? Mathf.Max(0, _townRoom.memberCount) : 0);
            var maxPlayers = hasRoom ? Mathf.Max(1, _townRoom.maxPlayers) : 4;
            _partyCountText.text = hasRoom ? $"파티 현황 ({memberCount}/{maxPlayers})" : "파티 현황 (-/-)";
        }

        if (_minimapCountText)
            _minimapCountText.text = Mathf.Clamp(hasRoom ? _townRoom.memberCount : 1, 1, 99).ToString();

        UpdatePartySlots(BuildPartyMembers(activeRoomUi), activeRoomUi, hasActiveGame);
        UpdateSelectedMapCard(displayMapIndex, hasActiveGame);
        UpdateMapSelectOptionState(displayMapIndex);
        UpdateMapInfoWindow(displayMapIndex);

        var inviteCode = ResolveInviteCode();
        if (_inviteCodeText)
            _inviteCodeText.text = string.IsNullOrWhiteSpace(inviteCode) ? "----" : inviteCode;
        if (_copyInviteButton)
            _copyInviteButton.interactable = !string.IsNullOrWhiteSpace(inviteCode);

        var status = ResolveStatusText(overrideStatus, hasRoom, isHost, hasActiveGame, hasReadySnapshot, activeRoomUi, displayMapIndex);
        if (_statusText)
            _statusText.text = status;
        if (_topStatusText)
            _topStatusText.text = hasActiveGame ? "Game 준비 중" : (isHost ? "Host 대기 중" : "Host 선택 대기 중");
        if (_clientHintText)
            _clientHintText.text = hasActiveGame ? "토글하여 준비 / 해제" : "Host가 맵을 선택하면 준비할 수 있습니다.";
        if (_readySummaryText)
            _readySummaryText.text = BuildReadySummary(activeRoomUi, hasActiveGame, isHost);

        if (_gameSelectButton)
        {
            _gameSelectButton.gameObject.SetActive(isHost);
            _gameSelectButton.interactable = isHost && !_creatingGameRoom && !_startingGameRoom && !_cancelingGameRoom;
        }

        if (_readyWindowButton)
        {
            _readyWindowButton.gameObject.SetActive(!isHost);
            _readyWindowButton.interactable = hasActiveGame && !_openingReadyWindow;
            var readyLabel = hasReadySnapshot && activeRoomUi.AmIReady ? "준비 완료" : "준비";
            if (_openingReadyWindow)
                readyLabel = "동기화 중";
            SetButtonLabel(_readyWindowButton, readyLabel);
        }

        if (_hostStartGameButton)
        {
            _hostStartGameButton.gameObject.SetActive(isHost);
            var hasStartBlocker = hasReadySnapshot && HasTownStartBlocker(activeRoomUi);
            _hostStartGameButton.interactable = isHost
                                                && !_creatingGameRoom
                                                && !_startingGameRoom
                                                && !_cancelingGameRoom
                                                && (!hasActiveGame || (hasReadySnapshot && !hasStartBlocker));
            var startLabel = !hasActiveGame ? "대기방 생성" : (hasStartBlocker ? "준비 대기" : "시작");
            if (_creatingGameRoom || _startingGameRoom)
                startLabel = "처리 중";
            SetButtonLabel(_hostStartGameButton, startLabel);
        }

        if (_hostCancelGameButton)
        {
            _hostCancelGameButton.gameObject.SetActive(isHost && hasActiveGame);
            _hostCancelGameButton.interactable = isHost && hasActiveGame && !_startingGameRoom && !_cancelingGameRoom;
        }

        ApplyPartyPanelCollapsedState();
    }

    private void TogglePartyPanelCollapsed()
    {
        _partyPanelCollapsed = !_partyPanelCollapsed;
        ApplyPartyPanelCollapsedState();
    }

    private void ApplyPartyPanelCollapsedState()
    {
        if (_partyPanelBodyRoot)
            _partyPanelBodyRoot.gameObject.SetActive(!_partyPanelCollapsed);

        if (_partyPanelRoot)
        {
            var height = _partyPanelCollapsed ? 56f : 640f;
            _partyPanelRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        if (_partyMinimizeLabelText)
            _partyMinimizeLabelText.text = _partyPanelCollapsed ? "+" : "-";

        if (_partyMinimizeButton)
            SetButtonLabel(_partyMinimizeButton, _partyPanelCollapsed ? "+" : "-");
    }

    private string ResolveStatusText(
        string overrideStatus,
        bool hasRoom,
        bool isHost,
        bool hasActiveGame,
        bool hasReadySnapshot,
        RoomUiController activeRoomUi,
        int mapIndex)
    {
        if (!string.IsNullOrWhiteSpace(overrideStatus))
            return overrideStatus;

        if (!hasRoom)
            return "Town Room 정보를 기다리는 중입니다.";

        if (hasActiveGame)
        {
            if (hasReadySnapshot)
            {
                if (isHost)
                    return activeRoomUi.CanOwnerStartGame ? "모든 준비가 완료되었습니다. 시작할 수 있습니다." : activeRoomUi.ReadySummaryText;

                return activeRoomUi.AmIReady ? "준비 완료. Host의 시작을 기다리는 중입니다." : "준비 버튼으로 참가 준비를 완료하세요.";
            }

            return isHost
                ? $"{GetMapTitle(mapIndex)} 대기방 연결 중입니다."
                : $"{GetMapTitle(mapIndex)} 대기방에 자동 연결 중입니다.";
        }

        return isHost
            ? $"{GetMapTitle(mapIndex)} 선택됨. 시작을 누르면 Game 대기방을 만들고 파티 준비 상태를 확인합니다."
            : "Host의 맵 선택과 시작을 기다리는 중입니다.";
    }

    private void UpdateSelectedMapCard(int mapIndex, bool activeGame)
    {
        if (_selectedMapTitleText)
            _selectedMapTitleText.text = GetMapTitle(mapIndex);
        if (_selectedMapMetaText)
            _selectedMapMetaText.text = $"{GetMapPlayers(mapIndex)}   {GetMapTime(mapIndex)}";
        if (_selectedMapDifficultyText)
            _selectedMapDifficultyText.text = GetMapDifficulty(mapIndex);
        if (_selectedMapGoalText)
            _selectedMapGoalText.text = activeGame ? "선택된 맵" : GetMapGoal(mapIndex);
    }

    private void UpdateMapInfoWindow(int mapIndex)
    {
        if (_mapInfoTitleText)
            _mapInfoTitleText.text = GetMapTitle(mapIndex);
        if (_mapInfoDescriptionText)
            _mapInfoDescriptionText.text = GetMapDescription(mapIndex);
        if (_mapInfoStatsText)
            _mapInfoStatsText.text =
                $"난이도  {GetMapDifficulty(mapIndex)}\n" +
                $"권장 플레이어  {GetMapPlayers(mapIndex)}\n" +
                $"예상 시간  {GetMapTime(mapIndex)}\n" +
                $"목표  {GetMapGoal(mapIndex)}";

        if (_mapInfoFeatureTexts == null)
            return;

        var features = GetMapFeatures(mapIndex);
        for (int i = 0; i < _mapInfoFeatureTexts.Length; i++)
        {
            var text = _mapInfoFeatureTexts[i];
            if (!text)
                continue;

            text.text = i < features.Length ? features[i] : "";
        }
    }

    private void UpdateMapSelectOptionState(int selectedIndex)
    {
        if (_mapOptions == null)
            return;

        for (int i = 0; i < _mapOptions.Length; i++)
        {
            var option = _mapOptions[i];
            if (option?.SelectedFrame != null)
                option.SelectedFrame.gameObject.SetActive(i == selectedIndex);
        }
    }

    private void UpdatePartySlots(List<PartyMemberView> members, RoomUiController roomUi, bool hasActiveGame)
    {
        UpdatePartySlotGroup(_partySlots, members, roomUi, hasActiveGame, false);
        UpdatePartySlotGroup(_sidePartySlots, members, roomUi, hasActiveGame, true);
    }

    private void UpdatePartySlotGroup(
        PartySlotBinding[] slots,
        List<PartyMemberView> members,
        RoomUiController roomUi,
        bool hasActiveGame,
        bool compact)
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.Root == null)
                continue;

            var member = members != null && i < members.Count ? members[i] : null;
            var hasMember = member != null && member.Connected;
            if (compact && !hasMember)
            {
                slot.Root.SetActive(false);
                continue;
            }

            slot.Root.SetActive(true);

            if (slot.IndexText)
                slot.IndexText.text = (i + 1).ToString();
            if (slot.NameText)
                slot.NameText.text = hasMember ? member.Name : "대기 중";
            if (slot.HpText)
                slot.HpText.text = hasMember ? "100/100" : compact ? "-" : "대기 중";
            if (slot.HpSlider)
                slot.HpSlider.value = hasMember ? 1f : 0f;

            if (slot.HostBadgeText)
            {
                slot.HostBadgeText.gameObject.SetActive(hasMember && member.IsHost);
                slot.HostBadgeText.text = "HOST";
            }

            var ready = hasMember && ResolveReadyState(member, roomUi, hasActiveGame);
            if (slot.ReadyText)
            {
                slot.ReadyText.gameObject.SetActive(hasMember);
                slot.ReadyText.text = ready ? "OK" : "...";
            }

            if (slot.ReadyGraphic)
                slot.ReadyGraphic.color = hasMember
                    ? (ready ? new Color(0.48f, 0.92f, 0.20f, 1f) : new Color(0.82f, 0.70f, 0.24f, 1f))
                    : new Color(0.22f, 0.25f, 0.28f, 0.55f);

            if (slot.LocalFrame)
                slot.LocalFrame.gameObject.SetActive(hasMember && member.IsLocal);
        }
    }

    private bool ResolveReadyState(PartyMemberView member, RoomUiController roomUi, bool hasActiveGame)
    {
        if (member == null || !member.Connected)
            return false;

        if (!hasActiveGame || member.IsHost || roomUi == null || !roomUi.IsConnectedToRoom)
            return true;

        if (string.IsNullOrWhiteSpace(member.Uid) || !roomUi.TryGetMemberReady(member.Uid, out var ready))
            return member.Ready;

        return ready;
    }

    private List<PartyMemberView> BuildPartyMembers(RoomUiController roomUi)
    {
        var result = new List<PartyMemberView>(4);
        var uid = SessionContext.Instance?.Uid ?? "";

        if (roomUi != null && roomUi.IsConnectedToRoom)
        {
            var snapshots = roomUi.GetMemberSnapshots();
            if (snapshots != null)
            {
                foreach (var member in snapshots)
                {
                    if (member == null || string.IsNullOrWhiteSpace(member.Uid))
                        continue;

                    result.Add(new PartyMemberView
                    {
                        Uid = member.Uid,
                        Name = !string.IsNullOrWhiteSpace(member.DisplayName) ? member.DisplayName : TrimUid(member.Uid),
                        IsHost = member.IsOwner,
                        IsLocal = member.IsLocal,
                        Connected = true,
                        Ready = member.Ready
                    });

                    if (result.Count >= 4)
                        break;
                }
            }
        }

        if (result.Count == 0 && _townRoom?.participants != null && _townRoom.participants.Count > 0)
        {
            foreach (var participant in _townRoom.participants)
            {
                if (participant == null)
                    continue;

                var name = !string.IsNullOrWhiteSpace(participant.name)
                    ? participant.name
                    : (!string.IsNullOrWhiteSpace(participant.uid) ? TrimUid(participant.uid) : $"Player {result.Count + 1}");
                if (roomUi != null
                    && roomUi.TryGetMemberName(participant.uid, out var roomName)
                    && !string.IsNullOrWhiteSpace(roomName))
                {
                    name = roomName;
                }

                result.Add(new PartyMemberView
                {
                    Uid = participant.uid ?? "",
                    Name = name,
                    IsHost = string.Equals(participant.uid, _townRoom.ownerUid, StringComparison.OrdinalIgnoreCase),
                    IsLocal = !string.IsNullOrWhiteSpace(uid) && string.Equals(participant.uid, uid, StringComparison.OrdinalIgnoreCase),
                    Connected = true,
                    Ready = true
                });

                if (result.Count >= 4)
                    break;
            }
        }

        if (result.Count == 0)
        {
            result.Add(new PartyMemberView
            {
                Uid = uid,
                Name = "Player 1",
                IsHost = true,
                IsLocal = true,
                Connected = true,
                Ready = true
            });
        }

        while (result.Count < 4)
        {
            result.Add(new PartyMemberView
            {
                Name = $"Player {result.Count + 1}",
                Connected = false,
                Ready = false
            });
        }

        return result;
    }

    private async Task<string> CreateGameRoomForSelectedMapAsync(bool showRoomUi)
    {
        if (_creatingGameRoom)
            return "";

        if (!await RefreshTownRoomAsync())
            return "";

        if (string.IsNullOrWhiteSpace(_townRoomId) || !IsTownHost(_townRoom))
        {
            ShowGameSelectWindow(false);
            UpdateView("Host만 Game 대기방을 만들 수 있습니다.");
            return "";
        }

        var roomUi = EnsureRoomUiController();
        if (roomUi == null)
        {
            UpdateView("Room UI를 찾을 수 없습니다.");
            return "";
        }

        var mapId = GetMapId(_selectedGameMapIndex);
        var title = GetMapTitle(_selectedGameMapIndex);
        var existingActiveGameRoomId = _townRoom?.activeGameRoomId ?? "";
        if (!string.IsNullOrWhiteSpace(existingActiveGameRoomId))
        {
            UpdateView("이미 열린 Game 대기방이 있습니다.");
            return existingActiveGameRoomId;
        }

        _creatingGameRoom = true;
        UpdateView("Game 대기방 생성 중...");

        try
        {
            ShowGameSelectWindow(false);
            if (showRoomUi)
                roomUi.OpenRoot(showList: false);

            var requiredMemberUids = GetTownParticipantUids();
            ConfigureTownRequiredStartMembers(roomUi, requiredMemberUids);
            var townMaxPlayers = _townRoom != null && _townRoom.maxPlayers > 0 ? _townRoom.maxPlayers : 4;
            var maxPlayers = Mathf.Clamp(Mathf.Max(townMaxPlayers, requiredMemberUids.Count), 1, 50);
            var roomId = await roomUi.CreateAndJoinRoomAsync(
                "",
                title,
                mapId,
                maxPlayers,
                relayMode: true,
                showUi: showRoomUi,
                requiredMemberUids: requiredMemberUids,
                sourceTownRoomId: _townRoomId);
            if (!this)
                return "";

            if (string.IsNullOrWhiteSpace(roomId))
            {
                UpdateView("Game 대기방 생성 실패");
                return "";
            }

            await WaitForRoomUiSnapshotAsync(roomUi, roomId);
            if (!IsRoomUiConnectedTo(roomUi, roomId))
            {
                UpdateView("Game 대기방 연결 실패");
                return "";
            }

            var result = await _townApi.SetActiveGameRoomAsync(_townRoomId, roomId, mapId, title);
            if (!this)
                return "";

            if (!result.Ok)
            {
                UpdateView($"Town에 Game 방 공유 실패 ({result.StatusCode})");
                return "";
            }

            if (result.Data?.room != null)
                _townRoom = result.Data.room;

            UpdateView("Game 대기방이 생성되었습니다.");
            return roomId;
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
            var joined = await EnsureJoinedActiveGameRoomAsync(roomUi, activeGameRoomId, showUi: isHost);
            if (!joined)
            {
                UpdateView("Game 대기방 연결에 실패했습니다.");
                return;
            }

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
        UpdateView("Game 시작 준비 중...");

        try
        {
            if (!await RefreshTownRoomAsync())
                return;

            var activeGameRoomId = _townRoom?.activeGameRoomId ?? "";
            if (string.IsNullOrWhiteSpace(activeGameRoomId))
            {
                activeGameRoomId = await CreateGameRoomForSelectedMapAsync(showRoomUi: false);
                if (string.IsNullOrWhiteSpace(activeGameRoomId))
                    return;

                UpdateView("Game 대기방을 만들었습니다. 파티원이 준비한 뒤 다시 시작하세요.");
                return;
            }

            var joined = await EnsureJoinedActiveGameRoomAsync(roomUi, activeGameRoomId, showUi: false);
            if (!joined)
            {
                activeGameRoomId = await RecreateActiveGameRoomAfterJoinFailureAsync(activeGameRoomId);
                if (string.IsNullOrWhiteSpace(activeGameRoomId))
                    return;

                joined = await EnsureJoinedActiveGameRoomAsync(roomUi, activeGameRoomId, showUi: false);
                if (!joined)
                {
                    UpdateView("Game 대기방 재연결에 실패했습니다.");
                    return;
                }
            }

            if (TryGetTownStartBlockReason(roomUi, out var blockReason))
            {
                UpdateView(blockReason);
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

    private async Task OpenPartyManageAsync()
    {
        var roomUi = EnsureRoomUiController();
        if (roomUi == null)
        {
            UpdateView("Room UI를 찾을 수 없습니다.");
            return;
        }

        var activeGameRoomId = _townRoom?.activeGameRoomId ?? "";
        if (!string.IsNullOrWhiteSpace(activeGameRoomId))
        {
            var joined = await EnsureJoinedActiveGameRoomAsync(roomUi, activeGameRoomId, showUi: true);
            if (!joined)
                UpdateView("Game 대기방 연결에 실패했습니다.");
            return;
        }

        roomUi.OpenRoot(showList: true);
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

    private void ToggleHomeUiOrInventory()
    {
        BindSceneReferencesIfNeeded();
        if (_homeUiController != null)
        {
            if (_homeUiController.ConsumedToggleThisFrame)
                return;

            _homeUiController.ToggleHomeUi();
            return;
        }

        ToggleInventory();
    }

    private void CopyInviteCode()
    {
        var code = ResolveInviteCode();
        if (string.IsNullOrWhiteSpace(code))
        {
            UpdateView("복사할 초대 코드가 없습니다.");
            return;
        }

        GUIUtility.systemCopyBuffer = code;
        UpdateView("초대 코드를 클립보드에 복사했습니다.");
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

        _lastAutoJoinRoomId = "";
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

    private async Task<string> RecreateActiveGameRoomAfterJoinFailureAsync(string staleGameRoomId)
    {
        if (!IsTownHost(_townRoom))
            return "";

        UpdateView("기존 Game 대기방 연결이 끊겨 새로 만드는 중...");
        _lastAutoJoinRoomId = "";

        if (!string.IsNullOrWhiteSpace(staleGameRoomId))
            await ClearActiveGameRoomAsync(staleGameRoomId);

        if (!this)
            return "";

        await RefreshTownRoomAsync();
        if (!this)
            return "";

        return await CreateGameRoomForSelectedMapAsync(showRoomUi: false);
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

    private async Task<bool> EnsureJoinedActiveGameRoomAsync(RoomUiController roomUi, string activeGameRoomId, bool showUi)
    {
        if (roomUi == null || string.IsNullOrWhiteSpace(activeGameRoomId))
            return false;

        ConfigureTownRequiredStartMembers(roomUi);

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
        ConfigureTownRequiredStartMembers(roomUi);
        return IsRoomUiConnectedTo(roomUi, activeGameRoomId);
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
            _root = FindChild<RectTransform>("TownLobbyRoot");
        if (_topTownTitleText == null)
            _topTownTitleText = FindChild<TMP_Text>("TopTownTitle");
        if (_topStatusText == null)
            _topStatusText = FindChild<TMP_Text>("TopStatus");
        if (_statusText == null)
            _statusText = FindChild<TMP_Text>("Status");
        if (_roleBadgeText == null)
            _roleBadgeText = FindChild<TMP_Text>("RoleBadge");
        if (_partyCountText == null)
            _partyCountText = FindChild<TMP_Text>("PartyCount");
        if (_selectedMapTitleText == null)
            _selectedMapTitleText = FindChild<TMP_Text>("SelectedMapTitle");
        if (_selectedMapMetaText == null)
            _selectedMapMetaText = FindChild<TMP_Text>("SelectedMapMeta");
        if (_selectedMapDifficultyText == null)
            _selectedMapDifficultyText = FindChild<TMP_Text>("SelectedMapDifficulty");
        if (_selectedMapGoalText == null)
            _selectedMapGoalText = FindChild<TMP_Text>("SelectedMapGoal");
        if (_inviteCodeText == null)
            _inviteCodeText = FindChild<TMP_Text>("InviteCode");
        if (_clientHintText == null)
            _clientHintText = FindChild<TMP_Text>("ClientReadyHint");
        if (_minimapCountText == null)
            _minimapCountText = FindChild<TMP_Text>("MinimapCount");
        if (_readySummaryText == null)
            _readySummaryText = FindChild<TMP_Text>("ReadySummary");
        if (_partyPanelRoot == null)
            _partyPanelRoot = FindChild<RectTransform>("TownPartyPanel");
        if (_partyPanelBodyRoot == null)
            _partyPanelBodyRoot = FindChild<RectTransform>("PartyPanelBody");
        if (_partyMinimizeLabelText == null)
            _partyMinimizeLabelText = FindChild<TMP_Text>("PartyPanelMinimizeLabel");
        if (_hostControlsRoot == null)
            _hostControlsRoot = FindChild<RectTransform>("HostControls");
        if (_clientControlsRoot == null)
            _clientControlsRoot = FindChild<RectTransform>("ClientControls");
        if (_gameSelectWindow == null)
            _gameSelectWindow = FindChild<RectTransform>("TownGameSelectWindow");
        if (_mapInfoWindow == null)
            _mapInfoWindow = FindChild<RectTransform>("TownMapInfoWindow");
        if (_mapInfoTitleText == null)
            _mapInfoTitleText = FindChild<TMP_Text>("MapInfoTitle");
        if (_mapInfoDescriptionText == null)
            _mapInfoDescriptionText = FindChild<TMP_Text>("MapInfoDescription");
        if (_mapInfoStatsText == null)
            _mapInfoStatsText = FindChild<TMP_Text>("MapInfoStats");
        if (_inventoryButton == null)
            _inventoryButton = FindChild<Button>("InventoryButton");
        if (_gameSelectButton == null)
            _gameSelectButton = FindChild<Button>("GameSelectButton");
        if (_mapSelectConfirmButton == null)
            _mapSelectConfirmButton = FindChild<Button>("MapSelectConfirmButton");
        if (_mapSelectPartyButton == null)
            _mapSelectPartyButton = FindChild<Button>("MapSelectPartyButton");
        if (_gameSelectCloseButton == null)
            _gameSelectCloseButton = FindChild<Button>("CloseButton");
        if (_mapInfoButton == null)
            _mapInfoButton = FindChild<Button>("MapInfoButton");
        if (_mapInfoCloseButton == null)
            _mapInfoCloseButton = FindChild<Button>("MapInfoCloseButton");
        if (_copyInviteButton == null)
            _copyInviteButton = FindChild<Button>("CopyInviteButton");
        if (_partyMinimizeButton == null)
            _partyMinimizeButton = FindChild<Button>("PartyPanelMinimizeButton");
        if (_readyWindowButton == null)
            _readyWindowButton = FindChild<Button>("ReadyWindowButton");
        if (_hostStartGameButton == null)
            _hostStartGameButton = FindChild<Button>("HostStartGameButton");
        if (_hostCancelGameButton == null)
            _hostCancelGameButton = FindChild<Button>("HostCancelGameButton");
        if (_partyManageButton == null)
            _partyManageButton = FindChild<Button>("PartyManageButton");
        if (_homeUiController == null)
            _homeUiController = FindSceneObject<TownHomeUiController>();

        ApplyFontToChildren();

        if (!_warnedMissingUiReferences && (_root == null || _sidePartySlots == null || _sidePartySlots.Length == 0 || _gameSelectWindow == null))
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
            return isHost ? "Game 대기방 없음" : "Host 대기 중";

        if (roomUi == null || !roomUi.IsConnectedToRoom)
            return isHost
                ? "참가자 준비: 대기방 연결 중"
                : "참가자 준비: 자동 연결 중";

        return roomUi.ReadySummaryText;
    }

    private void ConfigureTownRequiredStartMembers(RoomUiController roomUi)
    {
        ConfigureTownRequiredStartMembers(roomUi, GetTownParticipantUids());
    }

    private static void ConfigureTownRequiredStartMembers(RoomUiController roomUi, IReadOnlyList<string> requiredUids)
    {
        if (roomUi == null)
            return;

        roomUi.SetRequiredStartMembers(requiredUids);
    }

    private bool HasTownStartBlocker(RoomUiController roomUi)
    {
        return TryGetTownStartBlockReason(roomUi, out _);
    }

    private bool TryGetTownStartBlockReason(RoomUiController roomUi, out string reason)
    {
        reason = "";
        if (roomUi == null || !roomUi.IsConnectedToRoom)
        {
            reason = "Game 대기방 연결을 기다리는 중입니다.";
            return true;
        }

        ConfigureTownRequiredStartMembers(roomUi);

        var requiredUids = GetTownParticipantUids();
        if (requiredUids.Count > 0)
        {
            var joined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var snapshots = roomUi.GetMemberSnapshots();
            if (snapshots != null)
            {
                foreach (var snapshot in snapshots)
                {
                    if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.Uid))
                        joined.Add(snapshot.Uid);
                }
            }

            var missingCount = 0;
            foreach (var uid in requiredUids)
            {
                if (!joined.Contains(uid))
                    missingCount++;
            }

            if (missingCount > 0)
            {
                reason = $"파티원 Game 대기방 입장 대기 중 ({joined.Count}/{requiredUids.Count})";
                return true;
            }
        }

        if (!roomUi.CanOwnerStartGame)
        {
            reason = roomUi.ReadySummaryText;
            return true;
        }

        return false;
    }

    private List<string> GetTownParticipantUids()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_townRoom?.participants != null)
        {
            foreach (var participant in _townRoom.participants)
            {
                var uid = participant?.uid ?? "";
                if (!string.IsNullOrWhiteSpace(uid) && seen.Add(uid))
                    result.Add(uid);
            }
        }

        var localUid = SessionContext.Instance?.Uid ?? "";
        if (!string.IsNullOrWhiteSpace(localUid) && seen.Add(localUid))
            result.Add(localUid);

        return result;
    }

    private static bool IsRoomUiConnectedTo(RoomUiController roomUi, string roomId)
    {
        return roomUi != null
               && roomUi.IsConnectedToRoom
               && !string.IsNullOrWhiteSpace(roomId)
               && string.Equals(roomUi.CurrentRoomId, roomId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task WaitForRoomUiSnapshotAsync(RoomUiController roomUi, string roomId)
    {
        if (roomUi == null || string.IsNullOrWhiteSpace(roomId))
            return;

        var deadline = Time.realtimeSinceStartup + 3f;
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

    private void SelectMapOption(int index)
    {
        if (!IsTownHost(_townRoom))
        {
            UpdateView("Host만 맵을 변경할 수 있습니다.");
            return;
        }

        _selectedGameMapIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _gameMapIds.Length - 1));
        UpdateView();
    }

    private async Task ConfirmSelectedMapSelectionAsync()
    {
        if (!await RefreshTownRoomAsync())
            return;

        if (!IsTownHost(_townRoom))
        {
            UpdateView("Host만 맵을 변경할 수 있습니다.");
            return;
        }

        ShowGameSelectWindow(false);
        await EnsureSelectedMapSharedAsync();
    }

    private async Task<string> EnsureSelectedMapSharedAsync()
    {
        var selectedMapId = GetMapId(_selectedGameMapIndex);
        var activeGameRoomId = _townRoom?.activeGameRoomId ?? "";
        var activeMapId = _townRoom?.activeGameMapId ?? "";

        if (!string.IsNullOrWhiteSpace(activeGameRoomId)
            && string.Equals(activeMapId, selectedMapId, StringComparison.OrdinalIgnoreCase))
        {
            UpdateView($"{GetMapTitle(_selectedGameMapIndex)} 선택이 이미 공유되어 있습니다.");
            return activeGameRoomId;
        }

        if (!string.IsNullOrWhiteSpace(activeGameRoomId))
        {
            var roomUi = RoomUiController.ActiveInstance ?? FindSceneObject<RoomUiController>();
            if (roomUi != null && string.Equals(roomUi.CurrentRoomId, activeGameRoomId, StringComparison.OrdinalIgnoreCase))
                await roomUi.LeaveCurrentRoomAsync(showListAfter: false);

            await ClearActiveGameRoomAsync(activeGameRoomId);
            _lastAutoJoinRoomId = "";
        }

        var createdRoomId = await CreateGameRoomForSelectedMapAsync(showRoomUi: false);
        if (!string.IsNullOrWhiteSpace(createdRoomId))
            UpdateView($"{GetMapTitle(_selectedGameMapIndex)} 선택을 파티에 공유했습니다.");

        return createdRoomId;
    }

    private void ShowGameSelectWindow(bool show)
    {
        BindSceneReferencesIfNeeded();
        if (show && !IsTownHost(_townRoom))
        {
            UpdateView("Host만 맵 선택 창을 열 수 있습니다.");
            return;
        }

        if (_gameSelectWindow)
        {
            if (show)
                _gameSelectWindow.SetAsLastSibling();

            _gameSelectWindow.gameObject.SetActive(show);
        }
    }

    private void ShowMapInfoWindow(bool show)
    {
        BindSceneReferencesIfNeeded();
        if (_mapInfoWindow)
        {
            if (show)
            {
                UpdateMapInfoWindow(!string.IsNullOrWhiteSpace(_townRoom?.activeGameMapId)
                    ? ResolveMapIndex(_townRoom.activeGameMapId)
                    : _selectedGameMapIndex);
                _mapInfoWindow.SetAsLastSibling();
            }

            _mapInfoWindow.gameObject.SetActive(show);
        }
    }

    private string ResolveInviteCode()
    {
        if (_townRoom != null && !string.IsNullOrWhiteSpace(_townRoom.roomId))
            return _townRoom.roomId;
        return _townRoomId;
    }

    private string ResolveTownTitle()
    {
        if (_townRoom != null && !string.IsNullOrWhiteSpace(_townRoom.title))
            return _townRoom.title;

        var mapId = SessionContext.Instance?.MapId ?? "";
        if (string.Equals(mapId, "Town_Forest", StringComparison.OrdinalIgnoreCase))
            return "타운 포레스트";

        return "타운";
    }

    private int ResolveMapIndex(string mapId)
    {
        if (_gameMapIds == null || _gameMapIds.Length == 0)
            return 0;

        for (int i = 0; i < _gameMapIds.Length; i++)
        {
            if (string.Equals(_gameMapIds[i], mapId, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return Mathf.Clamp(_selectedGameMapIndex, 0, _gameMapIds.Length - 1);
    }

    private string GetMapId(int index) => GetArrayValue(_gameMapIds, index, "Game_Forest_First_Step");
    private string GetMapTitle(int index) => GetArrayValue(_gameTitles, index, GetMapId(index));
    private string GetMapDescription(int index) => GetArrayValue(_gameDescriptions, index, "");
    private string GetMapDifficulty(int index) => GetArrayValue(_gameDifficultyLabels, index, "쉬움");
    private string GetMapPlayers(int index) => GetArrayValue(_gamePlayerLabels, index, "1~4명");
    private string GetMapTime(int index) => GetArrayValue(_gameTimeLabels, index, "5~10분");
    private string GetMapGoal(int index) => GetArrayValue(_gameGoalLabels, index, "모든 적 처치");

    private string[] GetMapFeatures(int index)
    {
        var title = GetMapTitle(index);
        return new[]
        {
            $"{title} 입구",
            "파티 합류 후 즉시 전투 준비 가능",
            GetMapGoal(index)
        };
    }

    private static string GetArrayValue(string[] values, int index, string fallback)
    {
        if (values == null || values.Length == 0)
            return fallback;

        index = Mathf.Clamp(index, 0, values.Length - 1);
        return string.IsNullOrWhiteSpace(values[index]) ? fallback : values[index];
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

    private static void SetActive(Component component, bool active)
    {
        if (component != null)
            component.gameObject.SetActive(active);
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

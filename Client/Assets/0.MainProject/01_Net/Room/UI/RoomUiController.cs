using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NetClient.Room;
using NetClient.Room.UI;

namespace NetClient.Room.UI
{
    public sealed class RoomUiController : MonoBehaviour
    {
        public static RoomUiController ActiveInstance { get; private set; }

        [Header("Deps")]
        [SerializeField] ApiClientProvider apiProvider;
        [SerializeField] string clientVersion = "1.0.0";

        [Header("Panels")]
        [SerializeField] GameObject panelRoomList;
        [SerializeField] GameObject panelCreateModal;
        [SerializeField] GameObject panelWaitingRoom;

        [Header("RoomList TopBar")]
        [SerializeField] Button btnRefresh;
        [SerializeField] Button btnCreate;
        [SerializeField] Button btnClose;
        [SerializeField] TMP_Text txtStatus;

        [Header("RoomList Scroll")]
        [SerializeField] Transform roomListContent;
        [SerializeField] RoomListItemView roomItemPrefab;

        [Header("Create Modal")]
        [SerializeField] TMP_InputField inputRoomId;
        //[SerializeField] TMP_InputField inputMapId;
        [SerializeField] TMP_Dropdown inputMapIds;
        [SerializeField] TMP_InputField inputMaxPlayers;
        [SerializeField] bool useP2PRelay = true;
        [SerializeField] Button btnCreateConfirm;
        [SerializeField] Button btnCreateCancel;

        [Header("WaitingRoom UI")]
        [SerializeField] TMP_Text txtRoomTitle;
        [SerializeField] Button btnLeave;
        [SerializeField] Button btnReady;
        [SerializeField] Button btnStart;
        [SerializeField] TMP_Text txtWarn;
        [SerializeField] Transform memberListContent;
        [SerializeField] MemberItemView memberItemPrefab;

        RoomListApiClient _roomListApi;
        RoomCreateApiClient _roomCreateApi;
        RoomWsClient _roomWs;

        CancellationTokenSource _cts;
        string _cursor = "";
        bool _isLoadMore = false; // 필요하면 더보기 붙일 때 사용
        bool _amIReady = false;
        string _currentRoomId = "";
        bool _currentRoomUseP2PRelay = false;
        string _currentSteamLobbyId = "";
        string _currentOwnerUid = "";
        string _preferredHostUid = "";
        int _preferredHostEpoch;
        string _hostSelectionMode = "";
        string _hostSelectionMetricVersion = "";
        float _hostSelectionScore = -1f;
        long _hostSelectionUpdatedAtMs;
        readonly List<string> _hostCandidateOrder = new();
        readonly List<HostSelectionCandidateState> _hostSelectionCandidates = new();
        readonly List<MemberTransportState> _memberTransportSnapshot = new();
        readonly List<float> _frameTimeSamplesMs = new();
        float _hostSelectionAvgFrameMs = 16.7f;
        float _hostSelectionP95FrameMs = 16.7f;
        float _nextFrameMetricRefreshAt;
        int _lastWaitingProbeRttMs = -1;
        string _lastWaitingProbeStatus = "Idle";
        string _steamLobbyStatus = "Idle";
        string _lastWarn = "";
        string _lastStatus = "";
        bool _openedExplicitly = false;
        bool _transitioningToGame = false;
        string _requestedRoomId = "";

        const string RelayKeyPrefix = "p2p:";

        // 멤버 UI 부분갱신용
        readonly Dictionary<string, MemberItemView> _memberViews = new();
        readonly Dictionary<string, string> _uidToName = new();
        readonly Dictionary<string, bool> _memberReady = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _requiredStartMemberUids = new(StringComparer.OrdinalIgnoreCase);

        public sealed class MemberSnapshot
        {
            public string Uid;
            public string DisplayName;
            public bool Ready;
            public bool IsOwner;
            public bool IsLocal;
        }

        public string CurrentRoomId => _currentRoomId ?? "";
        public bool CurrentRoomUseP2PRelay => _currentRoomUseP2PRelay;
        public string CurrentSteamLobbyId => _currentSteamLobbyId ?? "";
        public string CurrentOwnerUid => _currentOwnerUid ?? "";
        public string PreferredHostUid => _preferredHostUid ?? "";
        public int PreferredHostEpoch => _preferredHostEpoch;
        public string HostSelectionMode => _hostSelectionMode ?? "";
        public string HostSelectionMetricVersion => _hostSelectionMetricVersion ?? "";
        public float HostSelectionScore => _hostSelectionScore;
        public long HostSelectionUpdatedAtMs => _hostSelectionUpdatedAtMs;
        public IReadOnlyList<HostSelectionCandidateState> HostSelectionCandidates => _hostSelectionCandidates;
        public IReadOnlyList<MemberTransportState> MemberTransportSnapshot => _memberTransportSnapshot;
        public bool HasHostSelectionSnapshot => _hostSelectionCandidates.Count > 0 || _memberTransportSnapshot.Count > 0;
        public float HostSelectionAvgFrameMs => _hostSelectionAvgFrameMs;
        public float HostSelectionP95FrameMs => _hostSelectionP95FrameMs;
        public string HostCandidateOrderSummary => _hostCandidateOrder.Count > 0 ? string.Join(" > ", _hostCandidateOrder) : "-";
        public int LastWaitingProbeRttMs => _lastWaitingProbeRttMs;
        public string LastWaitingProbeStatus => _lastWaitingProbeStatus ?? "Idle";
        public string SteamLobbyStatus => _steamLobbyStatus ?? "Idle";
        public string LastWarningText => _lastWarn ?? "";
        public string LastStatusText => _lastStatus ?? "";
        public int MemberCount => _memberReady.Count > 0 ? _memberReady.Count : _memberViews.Count;
        public bool IsUiOpen => gameObject.activeInHierarchy;
        public bool IsConnectedToRoom => _roomWs != null && !string.IsNullOrWhiteSpace(_currentRoomId);
        public bool AmIReady => _amIReady;
        public bool IsLocalRoomOwner => string.Equals(_currentOwnerUid, apiProvider != null ? apiProvider.Uid : "", StringComparison.OrdinalIgnoreCase);
        public int ReadyTargetCount => CountReadyTargets();
        public int ReadyTargetDoneCount => CountReadyTargets(doneOnly: true);
        public bool AllRequiredStartMembersPresent => AreRequiredStartMembersPresent();
        public bool AllReadyForStart => IsConnectedToRoom
                                       && !string.IsNullOrWhiteSpace(_currentOwnerUid)
                                       && AllRequiredStartMembersPresent
                                       && ReadyTargetDoneCount >= ReadyTargetCount;
        public bool CanOwnerStartGame => IsLocalRoomOwner && AllReadyForStart;
        public string ReadySummaryText => BuildReadySummaryText();
        public event Action<string> RoomSessionClosed;
        public event Action WaitingRoomStateChanged;

        public bool TryGetMemberReady(string uid, out bool ready)
        {
            ready = false;
            return !string.IsNullOrWhiteSpace(uid) && _memberReady.TryGetValue(uid, out ready);
        }

        public bool TryGetMemberName(string uid, out string displayName)
        {
            displayName = "";
            return !string.IsNullOrWhiteSpace(uid) && _uidToName.TryGetValue(uid, out displayName);
        }

        public void SetRequiredStartMembers(IEnumerable<string> uids)
        {
            var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (uids != null)
            {
                foreach (var uid in uids)
                {
                    if (!string.IsNullOrWhiteSpace(uid))
                        next.Add(uid);
                }
            }

            if (_requiredStartMemberUids.SetEquals(next))
                return;

            _requiredStartMemberUids.Clear();
            foreach (var uid in next)
                _requiredStartMemberUids.Add(uid);

            RefreshStartButton();
            NotifyWaitingRoomStateChanged();
        }

        public IReadOnlyList<MemberSnapshot> GetMemberSnapshots()
        {
            var snapshots = new List<MemberSnapshot>(_memberReady.Count);
            foreach (var pair in _memberReady)
            {
                var uid = pair.Key;
                if (string.IsNullOrWhiteSpace(uid))
                    continue;

                var displayName = _uidToName.TryGetValue(uid, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : uid;

                snapshots.Add(new MemberSnapshot
                {
                    Uid = uid,
                    DisplayName = displayName,
                    Ready = pair.Value,
                    IsOwner = string.Equals(uid, _currentOwnerUid, StringComparison.OrdinalIgnoreCase),
                    IsLocal = apiProvider != null && string.Equals(uid, apiProvider.Uid, StringComparison.OrdinalIgnoreCase)
                });
            }

            return snapshots;
        }


        void Awake()
        {
            ActiveInstance = this;
            _cts = new CancellationTokenSource();

            var apiReady = EnsureApiClients();
            ApplyKoreanFontToChildren();

            // 버튼 바인딩
            btnRefresh?.onClick.AddListener(() => UI_Refresh());
            btnCreate?.onClick.AddListener(() => UI_OpenCreate());
            btnClose?.onClick.AddListener(() => UI_Close());

            btnCreateConfirm?.onClick.AddListener(() => UI_CreateConfirm());
            btnCreateCancel?.onClick.AddListener(() => UI_CloseCreate());

            btnLeave?.onClick.AddListener(() => UI_LeaveRoom());
            btnReady?.onClick.AddListener(() => UI_ToggleReady());
            btnStart?.onClick.AddListener(() => UI_StartGame());

            ShowRoomList();
            if (apiReady)
                UI_Refresh();
            else
                SetStatus("API Provider 준비 중...");

            // [Request] MapId Dropdown 초기화
            if (inputMapIds != null)
            {
                inputMapIds.ClearOptions();
                inputMapIds.AddOptions(new List<string> { "Game", "Game_01", "Game_Forest_01", "Game_Forest_Tutorial" });
            }
        }

        private void Start()
        {
            if (!_openedExplicitly)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            SampleHostSelectionFrameMetrics();
            _roomWs?.Tick();
        }

        async void OnDestroy()
        {
            if (ActiveInstance == this)
                ActiveInstance = null;

            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch { /* ignore */ }

            AppBootstrap.Instance?.Root?.SteamPlatform?.LeaveLobby();
            await DisposeWsIfAny();
        }

        // -----------------------
        // Panel control
        // -----------------------
        void ShowRoomList()
        {
            if (panelRoomList) panelRoomList.SetActive(true);
            if (panelCreateModal) panelCreateModal.SetActive(false);
            if (panelWaitingRoom) panelWaitingRoom.SetActive(false);
            SetWarn("");
        }

        void ShowCreateModal(bool on)
        {
            if (panelCreateModal) panelCreateModal.SetActive(on);
        }

        void ShowWaitingRoom()
        {
            if (panelRoomList) panelRoomList.SetActive(false);
            if (panelCreateModal) panelCreateModal.SetActive(false);
            if (panelWaitingRoom) panelWaitingRoom.SetActive(true);
            SetWarn("");
        }

        public void OpenRoot(bool showList = true)
        {
            _openedExplicitly = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            ApplyKoreanFontToChildren();
            if (showList)
            {
                ShowRoomList();
                UI_Refresh();
            }
        }

        public void UI_Close()
        {
            // UI 창 끄기 = 루트 비활성
            // (원하면 dispose하고 상태 초기화)
            _transitioningToGame = false;
            NotifyRoomSessionClosed();
            AppBootstrap.Instance.Root.SteamPlatform.LeaveLobby();
            ResetDebugState();
            _ = DisposeWsIfAny();
            gameObject.SetActive(false);
        }

        // -----------------------
        // RoomList
        // -----------------------
        public async void UI_Refresh()
        {
            if (!EnsureApiClients())
                return;

            _cursor = "";
            _isLoadMore = false;
            SetStatus("Loading rooms...");
            await SafeCall(async () => await _roomListApi.RefreshAsync(cursor: _cursor));
        }

        void OnRoomsUpdated(List<RoomSummaryDto> rooms, string nextCursor)
        {
            _cursor = nextCursor ?? "";

            // 요구사항: Refresh하면 “현 만들어져 있는 RoomList를 받아와 출력”
            RebuildRoomList(rooms);

            SetStatus($"Rooms: {rooms.Count}");
        }

        void RebuildRoomList(List<RoomSummaryDto> rooms)
        {
            ClearChildren(roomListContent);

            if (rooms == null) rooms = new List<RoomSummaryDto>();
            for (int i = 0; i < rooms.Count; i++)
            {
                var dto = rooms[i];
                var item = Instantiate(roomItemPrefab, roomListContent);
                item.Bind(dto, roomId => _ = JoinRoomAsync(roomId));
            }
        }

        public Task JoinRoomByIdAsync(string roomId, bool showUi = true)
        {
            _openedExplicitly = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            return JoinRoomAsync(roomId, showUi);
        }

        async Task JoinRoomAsync(string roomId, bool showUi = true)
        {
            if (!EnsureApiClients())
                return;

            _transitioningToGame = false;

            if (_roomWs != null
                && string.Equals(_currentRoomId, roomId, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(_currentRoomId))
            {
                if (showUi)
                    ShowWaitingRoom();
                else
                    HideAllPanels();
                return;
            }

            await DisposeWsIfAny();

            _memberViews.Clear();
            _uidToName.Clear();
            _memberReady.Clear();
            _requiredStartMemberUids.Clear();
            _requestedRoomId = roomId ?? "";
            _currentRoomId = "";
            _currentRoomUseP2PRelay = false;
            _currentSteamLobbyId = "";
            _amIReady = false;
            _currentOwnerUid = "";
            _preferredHostUid = "";
            _preferredHostEpoch = 0;
            _hostSelectionMode = "";
            _hostSelectionMetricVersion = "";
            _hostSelectionScore = -1f;
            _hostSelectionUpdatedAtMs = 0;
            _hostCandidateOrder.Clear();
            _hostSelectionCandidates.Clear();
            _memberTransportSnapshot.Clear();
            _frameTimeSamplesMs.Clear();
            _hostSelectionAvgFrameMs = 16.7f;
            _hostSelectionP95FrameMs = 16.7f;
            _nextFrameMetricRefreshAt = 0f;
            _lastWaitingProbeRttMs = -1;
            _lastWaitingProbeStatus = "Connecting";
            _steamLobbyStatus = "Waiting";

            var effectiveClientVersion = string.IsNullOrWhiteSpace(clientVersion)
                ? AppBootstrap.Instance.Root.Config.ClientVersion
                : clientVersion;

            _roomWs = new RoomWsClient(new StdWebSocketClient(), effectiveClientVersion);
            BindWsEvents(_roomWs);

            var wsUrl = apiProvider.BuildRoomWsUrl(roomId);
            SetWarn("Connecting...");
            
            if (showUi)
                ShowWaitingRoom();
            else
                HideAllPanels();

            await SafeCall(async () => await _roomWs.ConnectAsync(wsUrl, _cts.Token));
        }

        // -----------------------
        // Create Room
        // -----------------------
        public void UI_OpenCreate()
        {
            // 입력 초기화
            if (inputRoomId) inputRoomId.text = "";
            //if (inputMapId) inputMapId.text = "";
            if (inputMaxPlayers) inputMaxPlayers.text = "4";

            ShowCreateModal(true);
        }

        public void UI_CloseCreate()
        {
            ShowCreateModal(false);
        }

        public async void UI_CreateConfirm()
        {
            var roomId = inputRoomId ? inputRoomId.text.Trim() : "";
            // var mapId = inputMapId ? inputMapId.text.Trim() : "";
            
            // Dropdown에서 선택된 텍스트 가져오기
            var mapId = "1";
            if (inputMapIds != null && inputMapIds.options.Count > 0)
            {
                mapId = inputMapIds.options[inputMapIds.value].text;
            }

            var maxPlayersStr = inputMaxPlayers ? inputMaxPlayers.text.Trim() : "4";

            if (string.IsNullOrEmpty(mapId))
            {
                SetStatus("MapId is required.");
                return;
            }

            if (!int.TryParse(maxPlayersStr, out var maxPlayers) || maxPlayers <= 0 || maxPlayers > 50)
            {
                SetStatus("MaxPlayers invalid (1~50).");
                return;
            }

            var createdId = await CreateAndJoinRoomAsync(
                roomId,
                string.IsNullOrEmpty(roomId) ? "New Room" : roomId,
                mapId,
                maxPlayers,
                useP2PRelay);

            if (!string.IsNullOrWhiteSpace(createdId))
                ShowCreateModal(false);
        }

        public async Task<string> CreateAndJoinRoomAsync(
            string roomId,
            string title,
            string mapId,
            int maxPlayers,
            bool relayMode,
            bool showUi = true,
            IReadOnlyList<string> requiredMemberUids = null,
            string sourceTownRoomId = "")
        {
            if (!EnsureApiClients())
                return "";

            if (string.IsNullOrEmpty(mapId))
            {
                SetStatus("MapId is required.");
                return "";
            }

            if (maxPlayers <= 0 || maxPlayers > 50)
            {
                SetStatus("MaxPlayers invalid (1~50).");
                return "";
            }

            var req = new RoomCreateApiClient.CreateRoomRequest
            {
                roomId = roomId ?? "",
                title = string.IsNullOrWhiteSpace(title) ? "New Room" : title,
                mapId = mapId,
                maxPlayers = maxPlayers,
                useP2PRelay = relayMode,
                requiredMemberUids = requiredMemberUids != null
                    ? requiredMemberUids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    : null,
                sourceTownRoomId = sourceTownRoomId ?? ""
            };

            string createdId = "";
            SetStatus("Creating room...");
            await SafeCall(async () =>
            {
                var r = await _roomCreateApi.CreateAsync(req, _cts.Token);
                if (!r.Ok)
                {
                    SetStatus($"Create failed: {r.StatusCode} {r.Error}");
                    return;
                }

                createdId = r.Data?.roomId ?? "";
                if (string.IsNullOrEmpty(createdId))
                {
                    SetStatus("Create ok but roomId missing in response.");
                    return;
                }

                SetStatus($"Created: {createdId}");
                UI_Refresh();
                await JoinRoomAsync(createdId, showUi);
                SetRequiredStartMembers(req.requiredMemberUids);
            });

            return createdId;
        }

        // -----------------------
        // WaitingRoom
        // -----------------------
        public async void UI_ToggleReady()
        {
            await SetReadyAsync(!_amIReady);
        }

        public async Task SetReadyAsync(bool ready)
        {
            if (_roomWs == null) return;
            await SafeCall(async () => await _roomWs.ToggleReadyAsync(ready, _cts.Token));
        }

        public async void UI_StartGame()
        {
            await StartGameAsync();
        }

        public async Task StartGameAsync()
        {
            if (_roomWs == null)
            {
                SetWarn("Room WebSocket is not connected.");
                return;
            }

            await SafeCall(async () => await _roomWs.StartGameAsync(_cts.Token));
        }

        public async void UI_LeaveRoom()
        {
            _transitioningToGame = false;
            await LeaveCurrentRoomAsync(showListAfter: true);
        }

        public async Task LeaveCurrentRoomAsync(bool showListAfter = false, bool closeRootAfter = false)
        {
            _transitioningToGame = false;
            if (_roomWs != null)
                await SafeCall(async () => await _roomWs.LeaveAsync(_cts.Token));

            NotifyRoomSessionClosed();
            AppBootstrap.Instance?.Root?.SteamPlatform?.LeaveLobby();
            ResetDebugState();
            await DisposeWsIfAny();

            if (closeRootAfter)
            {
                gameObject.SetActive(false);
                return;
            }

            if (showListAfter)
            {
                ShowRoomList();
                UI_Refresh();
            }
        }

        void BindWsEvents(RoomWsClient ws)
        {
            ws.OnHostProbeMeasured += rttMs =>
            {
                _lastWaitingProbeRttMs = rttMs;
                _lastWaitingProbeStatus = $"Measured {rttMs} ms";
            };

            ws.OnInit += room =>
            {
                Debug.Log($"[RoomUiController] OnInit Received. RoomId={room?.roomId}, Members={room?.memberUids?.Count}");
                _currentRoomId = room?.roomId;
                _requestedRoomId = _currentRoomId ?? _requestedRoomId;
                _currentRoomUseP2PRelay = room != null && room.useP2PRelay;
                _currentSteamLobbyId = room?.steamLobbyId ?? "";
                _currentOwnerUid = room?.ownerUid ?? "";
                _steamLobbyStatus = string.IsNullOrWhiteSpace(_currentSteamLobbyId)
                    ? "Waiting Lobby Bind"
                    : $"Bound {_currentSteamLobbyId}";
                ApplyHostSelectionState(
                    room?.preferredHostUid,
                    room != null ? room.hostSelectionEpoch : 0,
                    room?.hostSelectionMode,
                    room?.hostSelectionMetricVersion,
                    room != null ? room.hostSelectionScore : -1f,
                    room != null ? room.hostSelectionUpdatedAtMs : 0L,
                    room?.hostCandidateOrder,
                    room?.hostSelectionCandidates,
                    room?.memberTransport);
                SetWarn("");
                if (txtRoomTitle) txtRoomTitle.text = string.IsNullOrEmpty(room.title) ? room.roomId : room.title;

                // 멤버 리스트 스냅샷 리빌드
                if (!memberListContent) Debug.LogError("[RoomUiController] memberListContent is NULL");
                if (!memberItemPrefab) Debug.LogError("[RoomUiController] memberItemPrefab is NULL");

                ClearChildren(memberListContent);
                _memberViews.Clear();
                _uidToName.Clear();
                _memberReady.Clear();
                SetRequiredStartMembers(room.requiredMemberUids);

                if (room.memberTransport != null)
                {
                    foreach (var member in room.memberTransport)
                    {
                        if (member != null && !string.IsNullOrEmpty(member.uid))
                            _uidToName[member.uid] = string.IsNullOrEmpty(member.name) ? member.uid : member.name;
                    }
                }

                if (room.memberUids != null)
                {
                    foreach (var uid in room.memberUids)
                    {
                        var display = _uidToName.TryGetValue(uid, out var n) ? n : uid;
                        var ready = FindReady(room, uid);
                        _memberReady[uid] = ready;
                        if (uid == apiProvider.Uid) _amIReady = ready;

                        if (memberListContent && memberItemPrefab)
                        {
                            var view = Instantiate(memberItemPrefab, memberListContent);
                            
                            // FORCE UI SCALE/POS FIX
                            view.transform.localScale = Vector3.one;
                            var p = view.transform.localPosition;
                            view.transform.localPosition = new Vector3(p.x, p.y, 0f);
                            view.Bind(uid, display, ready);
                            _memberViews[uid] = view;
                        }
                    }
                }

                RefreshReadyButton();
                RefreshStartButton();
                NotifyWaitingRoomStateChanged();
                _ = SyncSteamLobbyAsync(room);
            };

            ws.OnMemberJoin += (uid, name) =>
            {
                _uidToName[uid] = name;
                _memberReady[uid] = false;

                if (_memberViews.TryGetValue(uid, out var existing) && existing)
                {
                    existing.SetName(name);
                    existing.SetReady(false);
                    RefreshStartButton();
                    NotifyWaitingRoomStateChanged();
                    return;
                }

                var view = Instantiate(memberItemPrefab, memberListContent);
                view.Bind(uid, name, false);
                _memberViews[uid] = view;
                RefreshStartButton();
                NotifyWaitingRoomStateChanged();
            };

            ws.OnMemberLeave += uid =>
            {
                if (_memberViews.TryGetValue(uid, out var view) && view)
                {
                    _memberViews.Remove(uid);
                    Destroy(view.gameObject);
                }

                RemoveSelectionSnapshot(uid);
                _memberReady.Remove(uid);
                if (uid == apiProvider.Uid)
                    _amIReady = false;

                RefreshReadyButton();
                RefreshStartButton();
                NotifyWaitingRoomStateChanged();
            };

            ws.OnMemberUpdate += (uid, ready) =>
            {
                _memberReady[uid] = ready;
                if (_memberViews.TryGetValue(uid, out var view) && view)
                    view.SetReady(ready);

                if (uid == apiProvider.Uid)
                    _amIReady = ready;

                RefreshReadyButton();
                RefreshStartButton();
                NotifyWaitingRoomStateChanged();
            };

            ws.OnHostCandidateUpdate += (preferredHostUid, hostEpoch) =>
            {
                _preferredHostUid = preferredHostUid ?? "";
                _preferredHostEpoch = hostEpoch;
                Debug.Log($"[RoomUiController] Host candidate updated. uid={preferredHostUid}, epoch={hostEpoch}");
                NotifyWaitingRoomStateChanged();
            };

            ws.OnHostSelectionUpdated += update =>
            {
                ApplyHostSelectionState(
                    update?.preferredHostUid,
                    update != null ? update.hostSelectionEpoch : 0,
                    update?.hostSelectionMode,
                    update?.hostSelectionMetricVersion,
                    update != null ? update.hostSelectionScore : -1f,
                    update != null ? update.hostSelectionUpdatedAtMs : 0L,
                    update?.hostCandidateOrder,
                    update?.hostSelectionCandidates,
                    update?.memberTransport);
                NotifyWaitingRoomStateChanged();
            };

            ws.OnSteamLobbyBound += steamLobbyId =>
            {
                _currentSteamLobbyId = steamLobbyId ?? "";
                _steamLobbyStatus = string.IsNullOrWhiteSpace(_currentSteamLobbyId)
                    ? "Lobby Bind Missing"
                    : $"Bound {_currentSteamLobbyId}";
                Debug.Log($"[RoomUiController] Steam lobby bound: {steamLobbyId}");
                _ = JoinBoundSteamLobbyAsync(_currentSteamLobbyId);
            };

            ws.OnGameStart += (endpoint, ticket, mapId, maxPlayers, relayMode, matchManifest) =>
            {
                _transitioningToGame = true;
                _currentRoomUseP2PRelay = relayMode;
                var clientManifest = ConvertMatchManifest(matchManifest);
                var hostUid = clientManifest != null ? clientManifest.HostUid : "";
                SetWarn($"GameStart: {endpoint.host}:{endpoint.port} Map:{mapId} Max:{maxPlayers} Relay:{relayMode} Host:{hostUid}");
                _ = DisposeWsIfAny();

                // DTO 변환 (Global EndpointDto -> SessionDtos.EndpointDto)
                var ticketResp = new SessionDtos.IssueGameTicketResponse
                {
                   Endpoint = new SessionDtos.EndpointDto { Host = endpoint.host, Port = endpoint.port },
                   TicketId = ticket,
                   Key = _currentRoomUseP2PRelay ? $"{RelayKeyPrefix}{_currentRoomId}" : _currentRoomId,
                   MapId = mapId,
                    MaxPlayers = maxPlayers,
                   MatchManifest = clientManifest
                };

                // ClientFlow를 통해 게임 서버 접속 및 씬 전환
                if (ClientFlow.Instance != null)
                {
                   var nonce = System.Guid.NewGuid().ToString("N");
                   ClientFlow.Instance.ConnectGame(ticketResp, nonce);
                }
                else
                {
                   SetWarn("ClientFlow instance is null. Cannot start game.");
                }
            };

            ws.OnErrorMsg += msg => SetWarn($"Error: {msg}");
            ws.OnWarn += msg => SetWarn(msg);
            ws.OnClosed += reason =>
            {
                if (_transitioningToGame)
                    return;

                SetWarn($"Closed: {reason}");
                NotifyRoomSessionClosed();
                AppBootstrap.Instance.Root.SteamPlatform.LeaveLobby();
                _ = DisposeWsIfAny();
                ShowRoomList();
                UI_Refresh();
            };
        }

        void RefreshReadyButton()
        {
            if (!btnReady) return;
            var label = btnReady.GetComponentInChildren<TMP_Text>();
            if (label)
                label.text = _amIReady ? "준비 완료" : "준비하기";
        }

        void RefreshStartButton()
        {
            if (!btnStart) return;
            btnStart.interactable = CanOwnerStartGame;

            var label = btnStart.GetComponentInChildren<TMP_Text>();
            if (label)
                label.text = CanOwnerStartGame ? "게임 시작" : "Ready 대기";
        }

        int CountReadyTargets(bool doneOnly = false)
        {
            if (_memberReady.Count <= 0 || string.IsNullOrWhiteSpace(_currentOwnerUid))
                return 0;

            if (_requiredStartMemberUids.Count > 0)
            {
                int requiredCount = 0;
                foreach (var uid in _requiredStartMemberUids)
                {
                    if (string.Equals(uid, _currentOwnerUid, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!doneOnly)
                    {
                        requiredCount++;
                        continue;
                    }

                    if (_memberReady.TryGetValue(uid, out var ready) && ready)
                        requiredCount++;
                }

                return requiredCount;
            }

            int count = 0;
            foreach (var pair in _memberReady)
            {
                if (string.Equals(pair.Key, _currentOwnerUid, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!doneOnly || pair.Value)
                    count++;
            }

            return count;
        }

        bool AreRequiredStartMembersPresent()
        {
            if (_requiredStartMemberUids.Count <= 0)
                return true;

            foreach (var uid in _requiredStartMemberUids)
            {
                if (!_memberReady.ContainsKey(uid))
                    return false;
            }

            return true;
        }

        int CountMissingRequiredStartMembers()
        {
            if (_requiredStartMemberUids.Count <= 0)
                return 0;

            int missing = 0;
            foreach (var uid in _requiredStartMemberUids)
            {
                if (!_memberReady.ContainsKey(uid))
                    missing++;
            }

            return missing;
        }

        string BuildReadySummaryText()
        {
            if (!IsConnectedToRoom)
                return "준비 상태: 대기방 연결 전";

            var target = ReadyTargetCount;
            var done = ReadyTargetDoneCount;
            var local = IsLocalRoomOwner
                ? "내 역할: Host"
                : (_amIReady ? "내 상태: 준비 완료" : "내 상태: 준비 필요");
            var gate = IsLocalRoomOwner
                ? (AllReadyForStart ? "시작 가능" : "참가자 준비 대기")
                : (AllReadyForStart ? "Host 시작 대기" : "준비 진행 중");
            var missing = CountMissingRequiredStartMembers();
            if (missing > 0)
                gate = $"참가자 입장 대기 {missing}명";

            if (IsLocalRoomOwner && target == 0)
                gate = "Solo 시작 가능";

            return $"참가자 준비 {done}/{target}\n{local} / {gate}";
        }

        void NotifyWaitingRoomStateChanged()
        {
            WaitingRoomStateChanged?.Invoke();
        }

        bool EnsureApiClients()
        {
            if (_roomListApi != null && _roomCreateApi != null && apiProvider != null)
                return true;

            apiProvider = ResolveApiClientProvider();
            if (apiProvider == null)
            {
                Debug.LogError("[RoomUiController] ApiClientProvider is missing. Add it to the scene hierarchy or bind it on RoomUIRoot.");
                return false;
            }

            ApiClient api;
            try
            {
                api = apiProvider.Api;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomUiController] ApiClientProvider failed: {ex.Message}");
                return false;
            }

            if (api == null)
            {
                Debug.LogError("[RoomUiController] ApiClientProvider.Api is null.");
                return false;
            }

            _roomListApi = new RoomListApiClient(api);
            _roomCreateApi = new RoomCreateApiClient(api);
            _roomListApi.OnRoomsUpdated += OnRoomsUpdated;
            _roomListApi.OnWarn += msg => SetStatus($"[RoomList] {msg}");
            return true;
        }

        ApiClientProvider ResolveApiClientProvider()
        {
            if (apiProvider != null)
                return apiProvider;

            var provider = GetComponent<ApiClientProvider>();
            if (provider != null)
                return provider;

            provider = GetComponentInParent<ApiClientProvider>(true);
            if (provider != null)
                return provider;

            provider = FindFirstObjectByType<ApiClientProvider>(FindObjectsInactive.Include);
            if (provider != null)
                return provider;

            return gameObject.AddComponent<ApiClientProvider>();
        }

        static bool FindReady(WaitingRoomDto room, string uid)
        {
            if (room?.memberReady == null) return false;
            for (int i = 0; i < room.memberReady.Count; i++)
            {
                var e = room.memberReady[i];
                if (e != null && e.uid == uid) return e.ready;
            }
            return false;
        }

        async Task SyncSteamLobbyAsync(WaitingRoomDto room)
        {
            if (room == null)
                return;

            var steam = AppBootstrap.Instance.Root.SteamPlatform;
            if (steam == null || !steam.Enabled)
            {
                _steamLobbyStatus = "Steam Disabled";
                return;
            }

            if (!steam.IsInitialized)
            {
                _steamLobbyStatus = "Steam Init Failed";
                if (!string.IsNullOrWhiteSpace(steam.LastError))
                    Debug.LogWarning($"[RoomUiController] Steam unavailable: {steam.LastError}");
                return;
            }

            bool isOwner = string.Equals(room.ownerUid, apiProvider.Uid, StringComparison.Ordinal);
            if (isOwner && string.IsNullOrWhiteSpace(room.steamLobbyId))
            {
                _steamLobbyStatus = "Creating Lobby";
                var lobbyId = await steam.CreateLobbyAsync(room.roomId, room.title, room.mapId, room.maxPlayers);
                if (!string.IsNullOrWhiteSpace(lobbyId))
                {
                    _currentSteamLobbyId = lobbyId;
                    _steamLobbyStatus = $"Created {lobbyId}";
                    if (_roomWs != null)
                        await _roomWs.BindSteamLobbyAsync(lobbyId, _cts.Token);
                }
                else if (!string.IsNullOrWhiteSpace(steam.LastError))
                {
                    _steamLobbyStatus = $"Create Failed: {steam.LastError}";
                    SetWarn($"Steam lobby create failed: {steam.LastError}");
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(room.steamLobbyId))
                await JoinBoundSteamLobbyAsync(room.steamLobbyId);
        }

        async Task JoinBoundSteamLobbyAsync(string steamLobbyId)
        {
            if (string.IsNullOrWhiteSpace(steamLobbyId))
                return;

            var steam = AppBootstrap.Instance.Root.SteamPlatform;
            if (steam == null || !steam.Enabled || !steam.IsInitialized)
            {
                _steamLobbyStatus = "Join Skipped";
                return;
            }

            _steamLobbyStatus = $"Joining {steamLobbyId}";
            bool ok = await steam.JoinLobbyAsync(steamLobbyId, _currentRoomId);
            _steamLobbyStatus = ok ? $"Joined {steamLobbyId}" : $"Join Failed {steamLobbyId}";
            if (!ok && !string.IsNullOrWhiteSpace(steam.LastError))
                SetWarn($"Steam lobby join failed: {steam.LastError}");
        }

        static SessionDtos.MatchManifestDto ConvertMatchManifest(WsMatchManifestDto src)
        {
            if (src == null)
                return null;

            var participants = new List<SessionDtos.MatchParticipantDto>();
            if (src.participants != null)
            {
                for (int i = 0; i < src.participants.Count; i++)
                {
                    var p = src.participants[i];
                    if (p == null) continue;

                    participants.Add(new SessionDtos.MatchParticipantDto
                    {
                        Uid = p.uid,
                        SteamId64 = p.steamId64,
                        ActorId = p.actorId,
                        LoadoutHash = p.loadoutHash
                    });
                }
            }

            return new SessionDtos.MatchManifestDto
            {
                MatchId = src.matchId,
                RoomId = src.roomId,
                NetworkMode = src.networkMode,
                ProtocolVersion = src.protocolVersion,
                MapId = src.mapId,
                StageSeed = src.stageSeed,
                SongStartDelayMs = src.songStartDelayMs,
                HostUid = src.hostUid,
                HostSteamId64 = src.hostSteamId64,
                HostEpoch = src.hostEpoch,
                PreferredHostRttMs = src.preferredHostRttMs,
                HostSelectionMode = src.hostSelectionMode,
                HostSelectionMetricVersion = src.hostSelectionMetricVersion,
                HostSelectionEpoch = src.hostSelectionEpoch,
                HostSelectionScore = src.hostSelectionScore,
                HostSelectionUpdatedAtMs = src.hostSelectionUpdatedAtMs,
                HostCandidateOrder = src.hostCandidateOrder,
                CreatedAtMs = src.createdAtMs,
                Participants = participants
            };
        }

        void ApplyKoreanFontToChildren()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NanumGothic SDF");
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("NanumGothic SDF");
            if (font == null)
                return;

            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (!text) continue;
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
        }

        // -----------------------
        // Helpers
        // -----------------------
        async Task DisposeWsIfAny()
        {
            if (_roomWs == null) return;
            try { await _roomWs.DisposeAsync(); } catch { }
            _roomWs = null;
        }

        async Task SafeCall(Func<Task> f)
        {
            try { await f(); }
            catch (Exception ex) { SetWarn(ex.Message); }
        }

        void ClearChildren(Transform t)
        {
            if (!t) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        void HideAllPanels()
        {
            if (panelRoomList) panelRoomList.SetActive(false);
            if (panelCreateModal) panelCreateModal.SetActive(false);
            if (panelWaitingRoom) panelWaitingRoom.SetActive(false);
        }

        void SetStatus(string s)
        {
            _lastStatus = s ?? "";
            if (txtStatus) txtStatus.text = s;
            Debug.Log(s);
        }

        void SetWarn(string s)
        {
            _lastWarn = s ?? "";
            if (txtWarn) txtWarn.text = s;
            if (!string.IsNullOrEmpty(s)) Debug.LogWarning(s);
        }

        void SampleHostSelectionFrameMetrics()
        {
            if (!gameObject.activeInHierarchy)
                return;

            float frameMs = Mathf.Max(1f, Time.unscaledDeltaTime * 1000f);
            _frameTimeSamplesMs.Add(frameMs);
            if (_frameTimeSamplesMs.Count > 300)
                _frameTimeSamplesMs.RemoveAt(0);

            if (Time.unscaledTime < _nextFrameMetricRefreshAt)
                return;

            _nextFrameMetricRefreshAt = Time.unscaledTime + 0.5f;
            if (_frameTimeSamplesMs.Count <= 0)
                return;

            float sum = 0f;
            for (int i = 0; i < _frameTimeSamplesMs.Count; i++)
                sum += _frameTimeSamplesMs[i];

            _hostSelectionAvgFrameMs = sum / _frameTimeSamplesMs.Count;

            var sorted = new List<float>(_frameTimeSamplesMs);
            sorted.Sort();
            int p95Index = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * 0.95f) - 1, 0, sorted.Count - 1);
            _hostSelectionP95FrameMs = sorted[p95Index];
        }

        void ApplyHostSelectionState(
            string preferredHostUid,
            int selectionEpoch,
            string selectionMode,
            string metricVersion,
            float selectionScore,
            long selectionUpdatedAtMs,
            List<string> candidateOrder,
            List<HostSelectionCandidateState> candidateStates = null,
            List<MemberTransportState> memberTransport = null)
        {
            _preferredHostUid = preferredHostUid ?? "";
            _preferredHostEpoch = selectionEpoch;
            _hostSelectionMode = selectionMode ?? "";
            _hostSelectionMetricVersion = metricVersion ?? "";
            _hostSelectionScore = selectionScore;
            _hostSelectionUpdatedAtMs = selectionUpdatedAtMs;
            _hostCandidateOrder.Clear();
            ApplySelectionSnapshot(candidateStates, memberTransport);

            if (candidateOrder == null)
                return;

            for (int i = 0; i < candidateOrder.Count; i++)
            {
                var uid = candidateOrder[i];
                if (string.IsNullOrWhiteSpace(uid) || _hostCandidateOrder.Contains(uid))
                    continue;

                _hostCandidateOrder.Add(uid);
            }
        }

        void ApplySelectionSnapshot(
            List<HostSelectionCandidateState> candidateStates,
            List<MemberTransportState> memberTransport)
        {
            if (candidateStates != null)
            {
                _hostSelectionCandidates.Clear();
                for (int i = 0; i < candidateStates.Count; i++)
                {
                    var src = candidateStates[i];
                    if (src == null || string.IsNullOrWhiteSpace(src.uid))
                        continue;

                    _hostSelectionCandidates.Add(CloneCandidateState(src));
                }
            }

            if (memberTransport != null)
            {
                _memberTransportSnapshot.Clear();
                for (int i = 0; i < memberTransport.Count; i++)
                {
                    var src = memberTransport[i];
                    if (src == null || string.IsNullOrWhiteSpace(src.uid))
                        continue;

                    _memberTransportSnapshot.Add(CloneMemberTransportState(src));
                }
            }
        }

        void RemoveSelectionSnapshot(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            _hostSelectionCandidates.RemoveAll(x => x != null && string.Equals(x.uid, uid, StringComparison.OrdinalIgnoreCase));
            _memberTransportSnapshot.RemoveAll(x => x != null && string.Equals(x.uid, uid, StringComparison.OrdinalIgnoreCase));
            _hostCandidateOrder.RemoveAll(x => string.Equals(x, uid, StringComparison.OrdinalIgnoreCase));
        }

        static HostSelectionCandidateState CloneCandidateState(HostSelectionCandidateState src)
        {
            return new HostSelectionCandidateState
            {
                uid = src.uid ?? "",
                isEligible = src.isEligible,
                candidateCost = src.candidateCost,
                averagePairCost = src.averagePairCost,
                worstPairCost = src.worstPairCost,
                averagePairRttMs = src.averagePairRttMs,
                worstPairRttMs = src.worstPairRttMs,
                steamPairCount = src.steamPairCount,
                measuredSteamPairCount = src.measuredSteamPairCount,
                proxySteamPairCount = src.proxySteamPairCount,
                serverRelayPairCount = src.serverRelayPairCount,
                unavailablePairCount = src.unavailablePairCount,
                hostCapacityPenalty = src.hostCapacityPenalty,
                steamReady = src.steamReady,
                currentServerRttMs = src.currentServerRttMs,
                currentServerLossPct = src.currentServerLossPct,
                currentServerJitterMs = src.currentServerJitterMs,
                avgFrameMs = src.avgFrameMs,
                p95FrameMs = src.p95FrameMs,
                disqualifiedReasons = src.disqualifiedReasons != null
                    ? new List<string>(src.disqualifiedReasons)
                    : new List<string>()
            };
        }

        static MemberTransportState CloneMemberTransportState(MemberTransportState src)
        {
            return new MemberTransportState
            {
                uid = src.uid ?? "",
                name = src.name ?? "",
                steamId64 = src.steamId64 ?? "",
                clientVersion = src.clientVersion ?? "",
                hostProbeRttMs = src.hostProbeRttMs,
                hostProbeReportedAtMs = src.hostProbeReportedAtMs,
                steamEnabled = src.steamEnabled,
                steamInitialized = src.steamInitialized,
                steamLobbyJoined = src.steamLobbyJoined,
                steamReady = src.steamReady,
                currentServerRttMs = src.currentServerRttMs,
                currentServerLossPct = src.currentServerLossPct,
                currentServerJitterMs = src.currentServerJitterMs,
                avgFrameMs = src.avgFrameMs,
                p95FrameMs = src.p95FrameMs,
                sendQueueDepth = src.sendQueueDepth,
                measuredSteamPairs = CloneMeasuredPairs(src.measuredSteamPairs),
                hostSelectionReportedAtMs = src.hostSelectionReportedAtMs
            };
        }

        static List<MeasuredSteamPairState> CloneMeasuredPairs(List<MeasuredSteamPairState> src)
        {
            if (src == null || src.Count <= 0)
                return new List<MeasuredSteamPairState>();

            var clones = new List<MeasuredSteamPairState>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                var pair = src[i];
                if (pair == null)
                    continue;

                clones.Add(new MeasuredSteamPairState
                {
                    peerUid = pair.peerUid ?? "",
                    peerSteamId64 = pair.peerSteamId64 ?? "",
                    rttMs = pair.rttMs,
                    connectionQualityLocal = pair.connectionQualityLocal,
                    connectionQualityRemote = pair.connectionQualityRemote,
                    connected = pair.connected,
                    reportedAtMs = pair.reportedAtMs,
                    source = pair.source ?? ""
                });
            }

            return clones;
        }

        void ResetDebugState()
        {
            _currentRoomId = "";
            _currentRoomUseP2PRelay = false;
            _currentSteamLobbyId = "";
            _currentOwnerUid = "";
            _preferredHostUid = "";
            _preferredHostEpoch = 0;
            _hostSelectionMode = "";
            _hostSelectionMetricVersion = "";
            _hostSelectionScore = -1f;
            _hostSelectionUpdatedAtMs = 0;
            _hostCandidateOrder.Clear();
            _hostSelectionCandidates.Clear();
            _memberTransportSnapshot.Clear();
            _frameTimeSamplesMs.Clear();
            _hostSelectionAvgFrameMs = 16.7f;
            _hostSelectionP95FrameMs = 16.7f;
            _nextFrameMetricRefreshAt = 0f;
            _lastWaitingProbeRttMs = -1;
            _lastWaitingProbeStatus = "Idle";
            _steamLobbyStatus = "Idle";
            _memberReady.Clear();
            _requestedRoomId = "";
            NotifyWaitingRoomStateChanged();
        }

        void NotifyRoomSessionClosed()
        {
            var roomId = !string.IsNullOrWhiteSpace(_currentRoomId) ? _currentRoomId : (_requestedRoomId ?? "");
            if (!string.IsNullOrWhiteSpace(roomId))
                RoomSessionClosed?.Invoke(roomId);
        }
    }

    internal static class RoomNetworkDebugFormatter
    {
        private const long ReportFreshnessWindowMs = 30_000;
        private const int RelayProcessingAllowanceMs = 8;
        private const int DefaultSteamPairRttMs = 60;

        public static bool HasWaitingRoomDetails(RoomUiController room)
        {
            return room != null && room.HasHostSelectionSnapshot;
        }

        public static List<string> BuildDetailedReportLines(RoomUiController room, int maxCandidates = 4, int maxPairs = 6)
        {
            var lines = new List<string>();
            if (room == null)
            {
                lines.Add("Room Selection Detail: no room snapshot");
                return lines;
            }

            var candidates = room.HostSelectionCandidates ?? Array.Empty<HostSelectionCandidateState>();
            var members = room.MemberTransportSnapshot ?? Array.Empty<MemberTransportState>();
            if (candidates.Count <= 0 && members.Count <= 0)
            {
                lines.Add("Room Selection Detail: no host selection snapshot");
                return lines;
            }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var selected = FindCandidate(candidates, room.PreferredHostUid);
            var runnerUp = candidates
                .Where(x => x != null && !string.Equals(x.uid, room.PreferredHostUid, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.isEligible ? 0 : 1)
                .ThenBy(x => x.candidateCost)
                .FirstOrDefault();
            var selectedMember = FindMember(members, room.PreferredHostUid);
            int expectedPeers = Math.Max(0, CountDistinctMembers(members) - 1);

            lines.Add(
                $"Room Selection Detail: host {FormatDebugValue(room.PreferredHostUid)} / mode {FormatDebugValue(room.HostSelectionMode)} / reason {ResolvePrimarySelectionReason(selected, runnerUp)}");

            if (selected != null)
            {
                lines.Add(
                    $"Reachability: measured {selected.measuredSteamPairCount}/{expectedPeers} ({FormatRatio(selected.measuredSteamPairCount, expectedPeers)})"
                    + $" | proxy {selected.proxySteamPairCount}/{expectedPeers} ({FormatRatio(selected.proxySteamPairCount, expectedPeers)})"
                    + $" | relay {selected.serverRelayPairCount}/{expectedPeers} ({FormatRatio(selected.serverRelayPairCount, expectedPeers)})");
                lines.Add(
                    $"Selected Candidate: cost {FormatCost(selected.candidateCost)} | avg {selected.averagePairRttMs} ms | worst {selected.worstPairRttMs} ms"
                    + $" | frame p95 {FormatFrameMs(selected.p95FrameMs)} | fresh {DescribeFreshness(selectedMember, nowMs)}");
            }
            else
            {
                lines.Add("Selected Candidate: not present in candidate snapshot");
            }

            if (candidates.Count > 0)
            {
                lines.Add("Candidates:");
                foreach (var candidate in candidates.Take(Math.Max(1, maxCandidates)))
                {
                    if (candidate == null)
                        continue;

                    string reasons = candidate.disqualifiedReasons != null && candidate.disqualifiedReasons.Count > 0
                        ? string.Join(", ", candidate.disqualifiedReasons)
                        : "-";
                    lines.Add(
                        $"- {FormatDebugValue(candidate.uid)} [{(candidate.isEligible ? "OK" : "DQ")}]"
                        + $" cost {FormatCost(candidate.candidateCost)}"
                        + $" | steam m/p {candidate.measuredSteamPairCount}/{candidate.proxySteamPairCount}"
                        + $" | relay {candidate.serverRelayPairCount}"
                        + $" | unavail {candidate.unavailablePairCount}"
                        + $" | p95 {FormatFrameMs(candidate.p95FrameMs)}"
                        + $" | reasons {reasons}");
                }
            }

            if (!string.IsNullOrWhiteSpace(room.PreferredHostUid))
            {
                var pairInfos = BuildPairInfos(room.PreferredHostUid, members, nowMs);
                if (pairInfos.Count > 0)
                {
                    lines.Add($"Selected Host Pairs ({Math.Min(maxPairs, pairInfos.Count)}/{pairInfos.Count}):");
                    foreach (var pair in pairInfos.Take(Math.Max(1, maxPairs)))
                    {
                        lines.Add(
                            $"- {FormatDebugValue(pair.PeerUid)} -> {FormatDebugValue(pair.CandidateUid)}: {pair.PathType}"
                            + $" {FormatPairRtt(pair.AverageRttMs)}"
                            + $" | qL {FormatQuality(pair.ConnectionQualityLocal)} / qR {FormatQuality(pair.ConnectionQualityRemote)}"
                            + $" | {pair.Reason}"
                            + $" | {pair.FreshnessLabel}");
                    }
                }
            }

            return lines;
        }

        private static List<PairDebugInfo> BuildPairInfos(string candidateUid, IReadOnlyList<MemberTransportState> members, long nowMs)
        {
            var result = new List<PairDebugInfo>();
            var candidate = FindMember(members, candidateUid);
            if (candidate == null)
                return result;

            foreach (var peer in members)
            {
                if (peer == null || string.IsNullOrWhiteSpace(peer.uid))
                    continue;
                if (string.Equals(peer.uid, candidateUid, StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(EvaluatePair(candidate, peer, nowMs));
            }

            return result
                .OrderBy(x => x.PathRank)
                .ThenBy(x => x.AverageRttMs < 0 ? int.MaxValue : x.AverageRttMs)
                .ThenBy(x => x.PeerUid, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static PairDebugInfo EvaluatePair(MemberTransportState candidate, MemberTransportState peer, long nowMs)
        {
            if (candidate == null || peer == null)
                return PairDebugInfo.Unavailable(candidate?.uid, peer?.uid, "MissingPeerMetrics");

            bool candidateSteam = IsSteamUsable(candidate);
            bool peerSteam = IsSteamUsable(peer);
            bool candidateFresh = HasFreshSelectionReport(candidate, nowMs);
            bool peerFresh = HasFreshSelectionReport(peer, nowMs);

            if (candidateSteam && peerSteam)
            {
                bool candidateMeasured = TryFindFreshMeasuredPair(candidate, peer, nowMs, out var candidatePair);
                bool peerMeasured = TryFindFreshMeasuredPair(peer, candidate, nowMs, out var peerPair);
                if (candidateMeasured || peerMeasured)
                {
                    var resolvedPair = ResolveMeasuredPair(candidatePair, peerPair);
                    int resolvedRtt = ResolveMeasuredRtt(candidatePair, peerPair);
                    return new PairDebugInfo(
                        peer.uid,
                        candidate.uid,
                        "SteamMeasuredReachable",
                        pathRank: 0,
                        averageRttMs: resolvedRtt,
                        connectionQualityLocal: resolvedPair?.connectionQualityLocal ?? -1f,
                        connectionQualityRemote: resolvedPair?.connectionQualityRemote ?? -1f,
                        freshnessLabel: BuildPairFreshnessLabel(candidateFresh && peerFresh, resolvedPair?.reportedAtMs ?? 0L, nowMs),
                        reason: BuildMeasuredPairReason(candidateMeasured, peerMeasured, resolvedPair));
                }

                bool hasStaleMeasured = HasAnyMeasuredPair(candidate, peer) || HasAnyMeasuredPair(peer, candidate);
                return new PairDebugInfo(
                    peer.uid,
                    candidate.uid,
                    "SteamProxyEstimated",
                    pathRank: 1,
                    averageRttMs: EstimateSteamPairRttMs(candidate.currentServerRttMs, peer.currentServerRttMs),
                    connectionQualityLocal: -1f,
                    connectionQualityRemote: -1f,
                    freshnessLabel: candidateFresh && peerFresh ? "fresh-proxy" : "stale-proxy",
                    reason: hasStaleMeasured ? "MeasuredPairStaleProxyFallback" : "ProxyEstimatedFromServerRtt");
            }

            if (candidate.currentServerRttMs >= 0 && peer.currentServerRttMs >= 0)
            {
                return new PairDebugInfo(
                    peer.uid,
                    candidate.uid,
                    "ServerRelayComposite",
                    pathRank: 2,
                    averageRttMs: candidate.currentServerRttMs + peer.currentServerRttMs + RelayProcessingAllowanceMs,
                    connectionQualityLocal: -1f,
                    connectionQualityRemote: -1f,
                    freshnessLabel: candidateFresh && peerFresh ? "fresh-relay" : "stale-relay",
                    reason: candidateSteam || peerSteam ? "SteamUnavailableFallbackToRelay" : "RelayOnlyPair");
            }

            return PairDebugInfo.Unavailable(candidate.uid, peer.uid, "InsufficientTransportData");
        }

        private static HostSelectionCandidateState FindCandidate(IReadOnlyList<HostSelectionCandidateState> candidates, string uid)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(uid))
                return null;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate != null && string.Equals(candidate.uid, uid, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return null;
        }

        private static MemberTransportState FindMember(IReadOnlyList<MemberTransportState> members, string uid)
        {
            if (members == null || string.IsNullOrWhiteSpace(uid))
                return null;

            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member != null && string.Equals(member.uid, uid, StringComparison.OrdinalIgnoreCase))
                    return member;
            }

            return null;
        }

        private static bool IsSteamUsable(MemberTransportState state)
        {
            return state != null && state.steamReady && !string.IsNullOrWhiteSpace(state.steamId64);
        }

        private static bool HasFreshSelectionReport(MemberTransportState state, long nowMs)
        {
            return state != null
                   && state.hostSelectionReportedAtMs > 0
                   && Math.Max(0L, nowMs - state.hostSelectionReportedAtMs) <= ReportFreshnessWindowMs;
        }

        private static string DescribeFreshness(MemberTransportState state, long nowMs)
        {
            if (state == null || state.hostSelectionReportedAtMs <= 0)
                return "missing";

            long ageMs = Math.Max(0L, nowMs - state.hostSelectionReportedAtMs);
            return ageMs <= ReportFreshnessWindowMs ? $"{ageMs} ms old" : $"stale ({ageMs} ms)";
        }

        private static bool TryFindFreshMeasuredPair(MemberTransportState source, MemberTransportState peer, long nowMs, out MeasuredSteamPairState match)
        {
            match = null;
            if (source?.measuredSteamPairs == null || source.measuredSteamPairs.Count <= 0 || peer == null)
                return false;

            match = source.measuredSteamPairs
                .Where(x => x != null
                            && x.connected
                            && x.rttMs >= 0
                            && Math.Max(0L, nowMs - x.reportedAtMs) <= ReportFreshnessWindowMs
                            && MatchesPeer(x, peer))
                .OrderByDescending(x => x.reportedAtMs)
                .ThenBy(x => x.rttMs)
                .FirstOrDefault();
            return match != null;
        }

        private static bool HasAnyMeasuredPair(MemberTransportState source, MemberTransportState peer)
        {
            if (source?.measuredSteamPairs == null || source.measuredSteamPairs.Count <= 0 || peer == null)
                return false;

            for (int i = 0; i < source.measuredSteamPairs.Count; i++)
            {
                var pair = source.measuredSteamPairs[i];
                if (pair != null && MatchesPeer(pair, peer))
                    return true;
            }

            return false;
        }

        private static bool MatchesPeer(MeasuredSteamPairState pair, MemberTransportState peer)
        {
            if (pair == null || peer == null)
                return false;

            if (!string.IsNullOrWhiteSpace(pair.peerUid)
                && !string.IsNullOrWhiteSpace(peer.uid)
                && string.Equals(pair.peerUid, peer.uid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(pair.peerSteamId64)
                && !string.IsNullOrWhiteSpace(peer.steamId64)
                && string.Equals(pair.peerSteamId64, peer.steamId64, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static MeasuredSteamPairState ResolveMeasuredPair(MeasuredSteamPairState left, MeasuredSteamPairState right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;

            return left.reportedAtMs >= right.reportedAtMs ? left : right;
        }

        private static int ResolveMeasuredRtt(MeasuredSteamPairState left, MeasuredSteamPairState right)
        {
            if (left != null && right != null)
                return (int)Math.Round((left.rttMs + right.rttMs) / 2f);
            if (left != null)
                return left.rttMs;
            if (right != null)
                return right.rttMs;
            return -1;
        }

        private static int EstimateSteamPairRttMs(int leftServerRttMs, int rightServerRttMs)
        {
            if (leftServerRttMs >= 0 && rightServerRttMs >= 0)
                return Math.Max(leftServerRttMs, rightServerRttMs) + 6;
            if (leftServerRttMs >= 0)
                return leftServerRttMs + 15;
            if (rightServerRttMs >= 0)
                return rightServerRttMs + 15;
            return DefaultSteamPairRttMs;
        }

        private static string ResolvePrimarySelectionReason(HostSelectionCandidateState selected, HostSelectionCandidateState runnerUp)
        {
            if (selected == null)
                return "SnapshotMissing";
            if (!selected.isEligible)
                return "EmergencyFallback";
            if (selected.measuredSteamPairCount > 0 && selected.serverRelayPairCount == 0)
                return "BestMeasuredReachability";
            if (runnerUp != null && selected.serverRelayPairCount < runnerUp.serverRelayPairCount)
                return "LowerRelayFallbackRatio";
            if (runnerUp != null && selected.worstPairCost + 0.0001f < runnerUp.worstPairCost)
                return "LowerWorstPairCost";
            return "LowestCandidateCost";
        }

        private static int CountDistinctMembers(IReadOnlyList<MemberTransportState> members)
        {
            if (members == null || members.Count <= 0)
                return 0;

            return members
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.uid))
                .Select(x => x.uid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static string FormatRatio(int count, int total)
        {
            if (total <= 0)
                return "-";

            return $"{(count / (float)total) * 100f:F0}%";
        }

        private static string FormatPairRtt(int rttMs)
        {
            return rttMs >= 0 ? $"{rttMs} ms" : "-";
        }

        private static string FormatQuality(float value)
        {
            return value < 0f ? "-" : $"{value * 100f:F0}%";
        }

        private static string FormatCost(float value)
        {
            return value >= 0f ? value.ToString("F3") : "-";
        }

        private static string FormatFrameMs(float value)
        {
            return value > 0f ? $"{value:F1} ms" : "-";
        }

        private static string FormatDebugValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string BuildPairFreshnessLabel(bool selectionFresh, long reportedAtMs, long nowMs)
        {
            if (reportedAtMs <= 0)
                return selectionFresh ? "fresh" : "stale";

            long ageMs = Math.Max(0L, nowMs - reportedAtMs);
            return ageMs <= ReportFreshnessWindowMs ? $"fresh ({ageMs} ms)" : $"stale ({ageMs} ms)";
        }

        private static string BuildMeasuredPairReason(bool candidateMeasured, bool peerMeasured, MeasuredSteamPairState resolvedPair)
        {
            string baseReason = candidateMeasured && peerMeasured
                ? "MeasuredSteamPairBidirectional"
                : "MeasuredSteamPairSingleSided";
            if (resolvedPair == null || string.IsNullOrWhiteSpace(resolvedPair.source))
                return baseReason;

            return $"{baseReason}/{resolvedPair.source}";
        }

        private sealed class PairDebugInfo
        {
            public PairDebugInfo(
                string peerUid,
                string candidateUid,
                string pathType,
                int pathRank,
                int averageRttMs,
                float connectionQualityLocal,
                float connectionQualityRemote,
                string freshnessLabel,
                string reason)
            {
                PeerUid = peerUid ?? "";
                CandidateUid = candidateUid ?? "";
                PathType = pathType ?? "Unavailable";
                PathRank = pathRank;
                AverageRttMs = averageRttMs;
                ConnectionQualityLocal = connectionQualityLocal;
                ConnectionQualityRemote = connectionQualityRemote;
                FreshnessLabel = freshnessLabel ?? "-";
                Reason = reason ?? "-";
            }

            public string PeerUid { get; }
            public string CandidateUid { get; }
            public string PathType { get; }
            public int PathRank { get; }
            public int AverageRttMs { get; }
            public float ConnectionQualityLocal { get; }
            public float ConnectionQualityRemote { get; }
            public string FreshnessLabel { get; }
            public string Reason { get; }

            public static PairDebugInfo Unavailable(string candidateUid, string peerUid, string reason)
            {
                return new PairDebugInfo(
                    peerUid ?? "",
                    candidateUid ?? "",
                    "Unavailable",
                    pathRank: 3,
                    averageRttMs: -1,
                    connectionQualityLocal: -1f,
                    connectionQualityRemote: -1f,
                    freshnessLabel: "missing",
                    reason: reason);
            }
        }
    }
}

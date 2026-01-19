using System;
using System.Collections.Generic;
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
        [SerializeField] TMP_InputField inputMapId;
        [SerializeField] TMP_InputField inputMaxPlayers;
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

        // 멤버 UI 부분갱신용
        readonly Dictionary<string, MemberItemView> _memberViews = new();
        readonly Dictionary<string, string> _uidToName = new();

        void Awake()
        {
            _cts = new CancellationTokenSource();

            _roomListApi = new RoomListApiClient(apiProvider.Api);
            _roomCreateApi = new RoomCreateApiClient(apiProvider.Api);

            _roomListApi.OnRoomsUpdated += OnRoomsUpdated;
            _roomListApi.OnWarn += msg => SetStatus($"[RoomList] {msg}");

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
            UI_Refresh();
        }

        async void OnDestroy()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch { /* ignore */ }

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

        public void UI_Close()
        {
            // UI 창 끄기 = 루트 비활성
            // (원하면 dispose하고 상태 초기화)
            _ = DisposeWsIfAny();
            gameObject.SetActive(false);
        }

        // -----------------------
        // RoomList
        // -----------------------
        public async void UI_Refresh()
        {
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

        async Task JoinRoomAsync(string roomId)
        {
            await DisposeWsIfAny();

            _memberViews.Clear();
            _uidToName.Clear();
            _amIReady = false;

            _roomWs = new RoomWsClient(new StdWebSocketClient(), clientVersion);
            BindWsEvents(_roomWs);

            var wsUrl = apiProvider.BuildRoomWsUrl(roomId);
            SetWarn("Connecting...");
            
            ShowWaitingRoom();

            await SafeCall(async () => await _roomWs.ConnectAsync(wsUrl, _cts.Token));
        }

        // -----------------------
        // Create Room
        // -----------------------
        public void UI_OpenCreate()
        {
            // 입력 초기화
            if (inputRoomId) inputRoomId.text = "";
            if (inputMapId) inputMapId.text = "";
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
            var mapId = inputMapId ? inputMapId.text.Trim() : "";
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

            var req = new RoomCreateApiClient.CreateRoomRequest
            {
                roomId = roomId,         // 빈 문자열이면 서버 생성 방식도 가능
                title = string.IsNullOrEmpty(roomId) ? "New Room" : roomId,
                mapId = mapId,
                maxPlayers = maxPlayers
            };

            SetStatus("Creating room...");
            await SafeCall(async () =>
            {
                var r = await _roomCreateApi.CreateAsync(req, _cts.Token);
                if (!r.Ok)
                {
                    SetStatus($"Create failed: {r.StatusCode} {r.Error}");
                    return;
                }

                var createdId = r.Data?.roomId;
                if (string.IsNullOrEmpty(createdId))
                {
                    SetStatus("Create ok but roomId missing in response.");
                    return;
                }

                SetStatus($"Created: {createdId}");
                ShowCreateModal(false);

                // 생성 후: 목록 갱신 + 자동 입장(요구사항에 가장 자연스러움)
                UI_Refresh();
                await JoinRoomAsync(createdId);
            });
        }

        // -----------------------
        // WaitingRoom
        // -----------------------
        public async void UI_ToggleReady()
        {
            if (_roomWs == null) return;
            var next = !_amIReady;
            await SafeCall(async () => await _roomWs.ToggleReadyAsync(next, _cts.Token));
        }

        public async void UI_StartGame()
        {
            if (_roomWs == null) return;
            await SafeCall(async () => await _roomWs.StartGameAsync(_cts.Token));
        }

        public async void UI_LeaveRoom()
        {
            // Option B: Close = Leave
            if (_roomWs != null)
                await SafeCall(async () => await _roomWs.LeaveAsync(_cts.Token));

            await DisposeWsIfAny();
            ShowRoomList();
            UI_Refresh();
        }

        void BindWsEvents(RoomWsClient ws)
        {
            ws.OnInit += room =>
            {
                Debug.Log($"[RoomUiController] OnInit Received. RoomId={room?.roomId}, Members={room?.memberUids?.Count}");
                SetWarn("");
                if (txtRoomTitle) txtRoomTitle.text = string.IsNullOrEmpty(room.title) ? room.roomId : room.title;

                // 멤버 리스트 스냅샷 리빌드
                if (!memberListContent) Debug.LogError("[RoomUiController] memberListContent is NULL");
                if (!memberItemPrefab) Debug.LogError("[RoomUiController] memberItemPrefab is NULL");

                ClearChildren(memberListContent);
                _memberViews.Clear();

                if (room.memberUids != null)
                {
                    foreach (var uid in room.memberUids)
                    {
                        var display = _uidToName.TryGetValue(uid, out var n) ? n : uid;
                        var ready = FindReady(room, uid);
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
                RefreshStartButton(ownerUid: room.ownerUid);
            };

            ws.OnMemberJoin += (uid, name) =>
            {
                _uidToName[uid] = name;

                if (_memberViews.TryGetValue(uid, out var existing) && existing)
                {
                    existing.SetName(name);
                    existing.SetReady(false);
                    return;
                }

                var view = Instantiate(memberItemPrefab, memberListContent);
                view.Bind(uid, name, false);
                _memberViews[uid] = view;
            };

            ws.OnMemberLeave += uid =>
            {
                if (_memberViews.TryGetValue(uid, out var view) && view)
                {
                    _memberViews.Remove(uid);
                    Destroy(view.gameObject);
                }

                if (uid == apiProvider.Uid)
                    _amIReady = false;

                RefreshReadyButton();
            };

            ws.OnMemberUpdate += (uid, ready) =>
            {
                if (_memberViews.TryGetValue(uid, out var view) && view)
                    view.SetReady(ready);

                if (uid == apiProvider.Uid)
                    _amIReady = ready;

                RefreshReadyButton();
            };

            ws.OnGameStart += (endpoint, ticket) =>
            {
                SetWarn($"GameStart: {endpoint.host}:{endpoint.port}");
                _ = DisposeWsIfAny();

                // TODO: 여기서 게임 씬 전환 + TCP 접속 시작
                // apiProvider.OnGameStart(endpoint, ticket) 등으로 위임 추천
            };

            ws.OnErrorMsg += msg => SetWarn($"Error: {msg}");
            ws.OnWarn += msg => SetWarn(msg);
            ws.OnClosed += reason =>
            {
                SetWarn($"Closed: {reason}");
                _ = DisposeWsIfAny();
                ShowRoomList();
                UI_Refresh();
            };
        }

        void RefreshReadyButton()
        {
            if (!btnReady) return;
            var label = btnReady.GetComponentInChildren<TMP_Text>();
            if (label) label.text = _amIReady ? "READY (ON)" : "READY (OFF)";
        }

        void RefreshStartButton(string ownerUid)
        {
            if (!btnStart) return;
            var isOwner = string.Equals(ownerUid, apiProvider.Uid, StringComparison.Ordinal);
            btnStart.interactable = isOwner;
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

        void SetStatus(string s)
        {
            if (txtStatus) txtStatus.text = s;
            Debug.Log(s);
        }

        void SetWarn(string s)
        {
            if (txtWarn) txtWarn.text = s;
            if (!string.IsNullOrEmpty(s)) Debug.LogWarning(s);
        }
    }
}

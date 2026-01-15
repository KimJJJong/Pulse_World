using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using NetClient.Lobby;     // LobbyApiClient
using NetClient.Room;      // RoomWsClient
using Contracts.Packet;    //  공통 패킷 (MemberDto, RoomDto, GetRoomsRes, ...)

public class RoomUIView : MonoBehaviour
{
    [Header("Config")]
    public string BaseUrl = "http://localhost:5290";
    public string ClientVersion = "1.0.0";

    [Header("Lobby UI")]
    public Button BtnRefresh;
    public Button BtnSession;
    public Button BtnCreate;
    public InputField InputTitle;
    public Transform RoomListRoot;
    public RoomItemView RoomItemPrefab;
    public GameObject LobbySection;   // 로비 섹션 루트(선택)

    [Header("Room UI")]
    public Button BtnReady;
    public Button BtnUnready;
    public Button BtnLeave;
    public Text TxtCountdown;
    public PlayerSlotView Slot1;      // 1P 표시
    public PlayerSlotView Slot2;      // 2P 표시
    public GameObject RoomSection;    // 룸 섹션 루트(선택)

    LobbyApiClient _api;
    IWebSocketClient _ws = new StdWebSocketClient();
    RoomWsClient _room;
    CancellationTokenSource _cts;
    long _serverOffsetMs = 0;

    // 현재 멤버 상태 캐시(userId -> (slot,name,ready))
    class LocalMember { public int slot; public string name; public bool ready; }
    Dictionary<string, LocalMember> _members = new();

    void Awake()
    {
        if (FindAnyObjectByType<MainThreadDispatcher>() == null)
            new GameObject("MainThreadDispatcher").AddComponent<MainThreadDispatcher>();

        _api = new LobbyApiClient(BaseUrl, ClientVersion);
        _cts = new CancellationTokenSource();

        if (BtnRefresh) BtnRefresh.onClick.AddListener(() => _ = RefreshRooms());
        if (BtnSession) BtnSession.onClick.AddListener(() => _ = ClickSessionMove());
        if (BtnCreate) BtnCreate.onClick.AddListener(() => _ = CreateAndEnter());
        if (BtnReady) BtnReady.onClick.AddListener(() => _ = _room?.ToggleReadyAsync(true));
        if (BtnUnready) BtnUnready.onClick.AddListener(() => _ = _room?.ToggleReadyAsync(false));
        if (BtnLeave) BtnLeave.onClick.AddListener(async () =>
        {
            if (_room != null) await _room.LeaveAsync();
            ShowLobby();
        });

        ShowLobby(); // 시작은 로비 화면
    }

    void ShowLobby()
    {
        LobbySection?.SetActive(true);
        RoomSection?.SetActive(false);
        TxtCountdown?.SetText("");
        Slot1?.Clear();
        Slot2?.Clear();
        _members.Clear();
    }

    void ShowRoom()
    {
        LobbySection?.SetActive(false);
        RoomSection?.SetActive(true);
        TxtCountdown?.SetText("");
        Slot1?.Clear();
        Slot2?.Clear();
        _members.Clear();
    }

    public async Task RefreshRooms()
    {
        var (ok, data, notModified, err) = await _api.GetRoomsAsync();
        if (!ok) { Debug.LogWarning(err); return; }
        if (notModified) return;

        foreach (Transform c in RoomListRoot) Destroy(c.gameObject);
        if (data?.rooms != null && RoomItemPrefab)
        {
            foreach (var r in data.rooms) // r: Contracts.Packet.RoomDto
            {
                var item = Instantiate(RoomItemPrefab, RoomListRoot);
                item.Setup(r, this); // RoomItemView는 RoomDto를 받도록 구현되어 있어야 함
            }
        }
    }
    public async Task ClickSessionMove()
    {
        var res = await _api.TryGetTownTicketAsync();
        if (res == null)
        {
            Debug.LogWarning("튕~");
            return;
        }
        Debug.Log($"Ticket : {res.ticketId} ||HOST :  {res.host} ||Port:  {res.port} ");

    }

    public async Task CreateAndEnter()
    {
        var title = string.IsNullOrWhiteSpace(InputTitle?.text) ? "1v1" : InputTitle.text.Trim();
        var (ok, res, err) = await _api.CreateRoomAsync(title);
        if (!ok) { Debug.LogWarning(err); return; }
        //await EnterRoom(res.wsUrl, _api.GetAccestToekn()); // TODO : accessToken 관리 철저하게 할 필요 있음
        ClickJoinRoom(res.roomId);
    }

    public async void ClickJoinRoom(string roomId)
    {
        var (ok, res, err) = await _api.JoinRoomAsync(roomId);
        if (!ok) { Debug.LogWarning($"Join fail: {err}"); return; }
        await EnterRoom(res.wsUrl, _api.GetAccestToekn());
    }

    async Task EnterRoom(string wsUrl, string token)
    {

        if (_room != null) await _room.DisposeAsync();
        _room = new RoomWsClient(_ws, ClientVersion);

        // 웰컴(멤버 전체 스냅샷)
        _room.OnWelcomeMembers += members =>
        {
            ShowRoom();
            _members.Clear();
            foreach (var m in members) // m: Contracts.Packet.MemberDto
            {
                if (string.IsNullOrEmpty(m.userId)) { Debug.LogWarning("member without userId"); continue; }
                _members[m.userId] = new LocalMember { slot = m.slot, name = m.name, ready = m.ready };
            }
            RefreshMemberUI();
        };

        _room.OnWelcome += w =>
        {
            _serverOffsetMs = w.serverTimeMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        };

        // 개별 이벤트 바인딩
        _room.OnMemberReady += (id, v) =>
        {
            if (_members.TryGetValue(id, out var lm))
            { lm.ready = v; RefreshMemberUI(); }
        };

        _room.OnMemberJoin += m =>
        {
            if (string.IsNullOrEmpty(m.userId)) return;
            _members[m.userId] = new LocalMember { slot = m.slot, name = m.name, ready = m.ready };
            RefreshMemberUI();
        };

        _room.OnMemberLeave += id =>
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_members.Remove(id)) RefreshMemberUI();
        };

        _room.OnRoomUpdate += (cur, status, _) =>
        {
            // 필요시 상단 라벨 등 갱신
            // RoomStatusText.SetText($"{cur}/2 * {status}");
        };

        _room.OnCountdownStart += (sec, startAtMs) => { _ = RunCountdownUI(sec, startAtMs); };
        _room.OnCountdownCancel += () => TxtCountdown?.SetText("");

        _room.OnGameBegin += begin =>
        {
            Debug.Log($"BEGIN udp:{begin.gsAddress.host}:{begin.gsAddress.port} ticket:{begin.ticket}");
            /*         if (begin.protoVer != 1)
                     {
                         //UI.ShowError($"업데이트 필요 (expected {NetConfig.ProtoVer}, got {begin.protoVer})");
                         Debug.LogError("업뎃 필요");
                         return;
                     }*/
            // NetWorkManager.Instance.MatchId = begin.matchId; // begin에 있으면 셋팅
            // NetWorkManager.Instance.Uid = MyUidProvider.Value;
            MainThreadDispatcher.Post(() =>
            {
                //NetWorkManager.Instance.ConnectAndJoin(begin.gsAddress.host, begin.gsAddress.port, begin.ticket, begin.protoVer);
            });

        };

        _room.OnClosed += reason => { Debug.Log($"ws closed: {reason}"); ShowLobby(); };
        _room.OnWarn += w => Debug.LogWarning(w);

        await _room.ConnectAsync(wsUrl, token, _cts.Token);
    }

    void RefreshMemberUI()
    {
        // 슬롯 1/2 기준으로 표시
        foreach (var kv in _members)
        {
            var m = kv.Value;
            if (m.slot == 1 && Slot1 != null) Slot1.Set(m.name, m.ready);
            if (m.slot == 2 && Slot2 != null) Slot2.Set(m.name, m.ready);
        }
        // 빈 슬롯 초기화(멤버 수 < 2)
        if (!HasSlot(1) && Slot1 != null) Slot1.Clear();
        if (!HasSlot(2) && Slot2 != null) Slot2.Clear();

        bool HasSlot(int s)
        {
            foreach (var lm in _members.Values) if (lm.slot == s) return true;
            return false;
        }
    }

    async Task RunCountdownUI(int sec, long startAtMs)
    {
        while (true)
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _serverOffsetMs; // 
            var remain = Mathf.CeilToInt((startAtMs - nowMs) / 1000f);
            if (remain <= 0) { TxtCountdown?.SetText(""); return; }
            TxtCountdown?.SetText(remain.ToString());
            await Task.Delay(200);
        }
    }

    void OnDestroy() => _cts.Cancel();
}

static class TextExt
{
    public static void SetText(this Text t, string s) { if (t) t.text = s; }
}

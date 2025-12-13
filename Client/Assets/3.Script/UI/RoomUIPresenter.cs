using Contracts.Packet;    // ★ 공통 패킷 (MemberDto, RoomDto, GetRoomsRes, ...)
using NetClient.Room;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class RoomUIPresenter : MonoBehaviour
{

    [SerializeField] private PlayerSlotUIView slot1;
    [SerializeField] private PlayerSlotUIView slot2;
    [SerializeField] private TextMeshProUGUI txtCountdown;
    [SerializeField] private Button btnLeave;
    [SerializeField] private Button btnReady;
    [SerializeField] private Button btnUnready;

    IWebSocketClient ws = new StdWebSocketClient();
    RoomWsClient room;
    CancellationTokenSource cts;
    long serverOffsetMs = 0;
    private string clientVersion;
    private string wsUrl;
    private string token;

    class LocalMember { public int slot; public string name; public bool ready; }
    Dictionary<string, LocalMember> _members = new();

    async private void Awake()
    {
        //무조건 필요한 스크립트
        if (FindAnyObjectByType<MainThreadDispatcher>() == null)
            new GameObject("MainThreadDispatcher").AddComponent<MainThreadDispatcher>();

        //클라 선언
        cts = new CancellationTokenSource();
        var payLoad = SceneTransit.I.ConsumePayload(); //여기서 가져오는 순간 비움
        if (payLoad != null)
        {
            clientVersion = payLoad.ClientVersion;
            wsUrl = payLoad.WsUrl;
            token = payLoad.Token;
        }

        // *

        if (btnLeave) btnLeave.onClick.AddListener(async () =>
        {
            if (room != null) await room.LeaveAsync();
            SceneLoader.LoadWithLoading(SceneLoader.Names.Lobby);
        });

        if (btnReady) btnReady.onClick.AddListener(() => _ = room?.ToggleReadyAsync(true));
        if (btnUnready) btnUnready.onClick.AddListener(() => _ = room?.ToggleReadyAsync(false));

        await EnterRoom();
    }
    bool HasSlot(int s)
    {
        foreach (var lm in _members.Values) if (lm.slot == s) return true;
        return false;
    }

    void RefreshMemberUI()
    {
        // 슬롯 1/2 기준으로 표시
        foreach (var kv in _members)
        {
            var m = kv.Value;
            if (m.slot == 1 && slot1 != null) slot1.Set(m.name, m.ready);
            if (m.slot == 2 && slot2 != null) slot2.Set(m.name, m.ready);
        }
        // 빈 슬롯 초기화(멤버 수 < 2)
        if (!HasSlot(1) && slot1 != null) slot1.Clear();
        if (!HasSlot(2) && slot2 != null) slot2.Clear();
    }
    async Task RunCountdownUI(int sec, long startAtMs)
    {
        while (true)
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + serverOffsetMs; // ★
            var remain = Mathf.CeilToInt((startAtMs - nowMs) / 1000f);
            if (remain <= 0) { txtCountdown?.SetText(""); return; }
            txtCountdown?.SetText(remain.ToString());
            await Task.Delay(200);
        }
    }

    async Task EnterRoom()
    {
        if (room != null) await room.DisposeAsync();
        if (clientVersion == null) Debug.LogError("clientVersion null");
        room = new RoomWsClient(ws, clientVersion);

        // 웰컴(멤버 전체 스냅샷)
        room.OnWelcomeMembers += members =>
        {
            _members.Clear();
            foreach (var m in members) // m: Contracts.Packet.MemberDto
            {
                if (string.IsNullOrEmpty(m.userId)) { Debug.LogWarning("member without userId"); continue; }
                _members[m.userId] = new LocalMember { slot = m.slot, name = m.name, ready = m.ready };
            }

            // 버튼 활성화관리
            if (HasSlot(1) && slot1 != null)
            {
                slot1.SetSlot(true);
                slot2.SetSlot(false);
            }
            if (HasSlot(2) && slot2 != null)
            {
                slot2.SetSlot(true);
                slot1.SetSlot(false);
            }

            RefreshMemberUI();
        };

        room.OnWelcome += w =>
        {
            serverOffsetMs = w.serverTimeMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        };

        // 개별 이벤트 바인딩
        room.OnMemberReady += (id, v) =>
        {
            if (_members.TryGetValue(id, out var lm))
            { lm.ready = v; RefreshMemberUI(); }
        };

        room.OnMemberJoin += m =>
        {
            if (string.IsNullOrEmpty(m.userId)) return;
            _members[m.userId] = new LocalMember { slot = m.slot, name = m.name, ready = m.ready };
            RefreshMemberUI();
        };

        room.OnMemberLeave += id =>
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_members.Remove(id)) RefreshMemberUI();
        };

        room.OnRoomUpdate += (cur, status, _) =>
        {
            // 필요시 상단 라벨 등 갱신
             
        };

        room.OnCountdownStart += (sec, startAtMs) => { _ = RunCountdownUI(sec, startAtMs); };
        room.OnCountdownCancel += () => txtCountdown?.SetText("");

        room.OnGameBegin += begin =>
        {
            //  통일 패킷: udp(host, port) + ticket
            //Debug.Log($"BEGIN udp:{begin.gSAddress.host}:{begin.gSAddress.port} ticket:{begin.ticket}");
            // TODO: 인게임 전환 핸드셰이크
        };

        room.OnClosed += reason => {
            Debug.Log($"ws closed: {reason}");
            SceneLoader.LoadWithLoading(SceneLoader.Names.Lobby);
        };
        room.OnWarn += w => Debug.LogWarning(w);

        await room.ConnectAsync(wsUrl, token, cts.Token);
    }

}

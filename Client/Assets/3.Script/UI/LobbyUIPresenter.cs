using Contracts.Packet;    // ★ 공통 패킷 (MemberDto, RoomDto, GetRoomsRes, ...)
using Cysharp.Threading.Tasks;
using NetClient.Lobby;
using PullToRefresh;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum MapType
{
    Forest,
}

public class LobbyUIPresenter : MonoBehaviour
{
    [Header("Config")]
    public string baseUrl = "http://localhost:5000";
    public string clientVersion = "1.0.0";

    [Header("UI Object")]
    [SerializeField] private ObjectPool objectPoolForUI;
    [SerializeField] private Transform roomListRoot; //반납할 때 쓸 용
    [SerializeField] private Button creatButton;
    [SerializeField] private UIRefreshControl refreshMotion;
    [SerializeField] private TMP_InputField roomName;
    [SerializeField] private GameObject emptRoomUIObj;
    [SerializeField] private MapType mapType;

    LobbyApiClient api;
    CancellationTokenSource cts;

    private void Awake()
    {
        //무조건 필요한 스크립트
        if (FindAnyObjectByType<MainThreadDispatcher>() == null)
            new GameObject("MainThreadDispatcher").AddComponent<MainThreadDispatcher>();

        //클라 선언
        api = new LobbyApiClient(baseUrl, clientVersion);
        cts = new CancellationTokenSource();

        if (creatButton) creatButton.onClick.AddListener(() => _ = CreateAndEnter());
        if (refreshMotion) refreshMotion.OnRefresh.AddListener(() => _ = RefreshRooms());
    }

    private async Task EnterRoom(string wsUrl, string token)
    {
        //  페이로드 생성 → 세션에 저장 → 로딩을 거쳐 룸 씬으로 토큰, wsUrl 전달
        var payload = new RoomLaunchPayload(wsUrl, token, clientVersion);
        var sceneTransit = SceneTransit.I;
        sceneTransit.SetNext(SceneLoader.Names.Room, payload);

        await SceneLoader.LoadWithLoading(SceneLoader.Names.Loading, cts.Token);
    }

    public async Task CreateAndEnter()
    {
        var title = string.IsNullOrWhiteSpace(roomName?.text) ? "1v1" : roomName.text.Trim();
        var (ok, res, err) = await api.CreateRoomAsync(title, mapType.ToString());
        if (!ok) { Debug.LogWarning(err); return; }

        await EnterRoom(res.wsUrl, res.token);
    }

    public async Task RefreshRooms()
    {
        await Task.Delay(1500); //1.5초 뒤
        refreshMotion.EndRefreshing(); //끝내고 업데이트 해야함

        var (ok, data, notModified, err) = await api.GetRoomsAsync();
        if (!ok) { Debug.LogWarning(err); return; }
        if (notModified) return;

        foreach (Transform c in roomListRoot) c.gameObject.SetActive(false);

        if (data?.rooms != null && objectPoolForUI)
        {
            foreach (var roomInfo in data.rooms) // r: Contracts.Packet.RoomDto
            {
                var item = objectPoolForUI.GetGo<RoomItemUIView>(roomInfo.map);
                item.Setup(roomInfo, this); // RoomItemView는 RoomDto를 받도록 구현되어 있어야 함
            }
            emptRoomUIObj.SetActive(false);
        }
        else emptRoomUIObj.SetActive(true);
    }

    public async void ClickJoinRoom(string roomId)
    {
        var (ok, res, err) = await api.JoinRoomAsync(roomId);
        if (!ok) { Debug.LogWarning($"Join fail: {err}"); return; }

        await EnterRoom(res.wsUrl, res.token);
    }
}

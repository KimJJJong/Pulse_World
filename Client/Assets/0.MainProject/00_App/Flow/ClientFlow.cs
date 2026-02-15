// Assets/0_App/Flow/ClientFlow.cs
using System;
using System.Threading.Tasks;
using System.Net;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ClientFlow : MonoBehaviour
{
    public static ClientFlow Instance { get; private set; } = null!;

    enum Target { None, TownMap, Game }
    Target _target = Target.None;
    bool _entering;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        NetworkManager.Instance.Ready += OnNetReady;
        NetworkManager.Instance.Failed += OnNetFailed;
        NetworkManager.Instance.Disconnected += OnNetDisconnected;
    }

    public async Task ConnectTown(SessionDtos.IssueTownTicketResponse ticket, string clientNonce)
    {
        _target = Target.TownMap;

        // HOSTNAME -> IP RESOLUTION
        IPAddress ipAddr;
        if (!IPAddress.TryParse(ticket.Endpoint.Host, out ipAddr))
        {
            try
            {
                var ips = await Dns.GetHostAddressesAsync(ticket.Endpoint.Host);
                if (ips.Length > 0)
                {
                    ipAddr = ips[0];
                    Debug.Log($"[ClientFlow] Resolved town host '{ticket.Endpoint.Host}' to {ipAddr}");
                }
                else
                {
                    Debug.LogError($"[ClientFlow] Failed to resolve host: {ticket.Endpoint.Host}");
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ClientFlow] DNS Error: {ex.Message}");
                return;
            }
        }

        var ep = new IPEndPoint(ipAddr, ticket.Endpoint.Port);
        Debug.Log($"[HandshakeArgs]endpoint :{ep} || Ticket :{ticket.TicketId} || Nonce: {clientNonce}  || target : {_target}");

        NetworkManager.Instance.ConnectAndHandshake(ep, ticket.TicketId, clientNonce);
    }



    string _mapId = "";
    int _maxPlayers = 2; // Default

    public async Task ConnectGame(SessionDtos.IssueGameTicketResponse ticket, string clientNonce)
    {
        _target = Target.Game;
        _mapId = ticket.MapId;
        _maxPlayers = ticket.MaxPlayers;

        IPAddress ipAddr;
        if (!IPAddress.TryParse(ticket.Endpoint.Host, out ipAddr))
        {
            try
            {
                var ips = await Dns.GetHostAddressesAsync(ticket.Endpoint.Host);
                if (ips.Length > 0)
                {
                    ipAddr = ips[0];
                    Debug.Log($"[ClientFlow] Resolved game host '{ticket.Endpoint.Host}' to {ipAddr}");
                }
                else
                {
                    Debug.LogError($"[ClientFlow] Failed to resolve host: {ticket.Endpoint.Host}");
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ClientFlow] DNS Error: {ex.Message}");
                return;
            }
        }

        var ep = new IPEndPoint(ipAddr, ticket.Endpoint.Port);
        Debug.Log($"[HandshakeArgs]endpoint :{ep} || Ticket :{ticket.TicketId} || Nonce: {clientNonce} || key :{ticket.Key} || target : {_target} || Map : {_mapId}");
        NetworkManager.Instance.ConnectAndHandshake(ep, ticket.TicketId, clientNonce, ticket.Key);
    }

    async void OnNetReady()
    {
        Debug.Log("OnNetReady");

        if (_entering) return;
        _entering = true;
        try
        {
            if (_target == Target.TownMap)
            {
                Debug.Log($"[ClientFlow] Scene Load Start: {SceneNames.TownMap}");
                
                // [LoadingScene] 적용
                LoadingSceneController.TargetSceneName = SceneNames.TownMap;
                UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScene");

                // Target Scene이 로드될 때까지 대기 (LoadingSceneController가 Additive로 로드함)
                while (UnityEngine.SceneManagement.SceneManager.GetSceneByName(SceneNames.TownMap).isLoaded == false)
                {
                    await Task.Yield();
                }

                Debug.Log($"[ClientFlow] Scene Load Complete: {SceneNames.TownMap}");

                var ctx = UnityEngine.Object.FindFirstObjectByType<TownSceneContext>();
                if (ctx == null)
                {
                    Debug.LogError("[ClientFlow] TownSceneContext not found in TownMap scene");
                    return;
                }

                await ctx.EnterTownAsync();
            }
            else if (_target == Target.Game)
            {
                string sceneToLoad = !string.IsNullOrEmpty(_mapId) ? _mapId : SceneNames.Game;

                Debug.Log($"[ClientFlow] Scene Load Start: {sceneToLoad} || MapId:{sceneToLoad}");
                
                // [LoadingScene] 적용
                LoadingSceneController.TargetSceneName = sceneToLoad;
                UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScene");

                // Target Scene이 로드될 때까지 대기
                while (UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneToLoad).isLoaded == false)
                {
                    await Task.Yield();
                }

                Debug.Log($"[ClientFlow] Scene Load Complete: {sceneToLoad}");
                
                // GameSceneContext 찾아서 진입 요청
                var ctx = UnityEngine.Object.FindFirstObjectByType<GameSceneContext>();
                if (ctx != null)
                {
                    ctx.SetMapId(_mapId);
                    ctx.SetMaxPlayers(_maxPlayers);
                    await ctx.EnterGameAsync();
                }
                else
                {
                    Debug.LogWarning("[ClientFlow] GameSceneContext not found in Game scene");
                }
            }
            else
            {
                Debug.LogWarning("[ClientFlow] Ready but target is None");
            }
        }
        finally
        {
            _entering = false;
        }
    }

    void OnNetFailed(string reason)
    {
        Debug.LogWarning($"[ClientFlow] Net failed: {reason}");
        SessionContext.Instance.ResetForReconnect();
        Debug.Log($"[ClientFlow] Scene Load Start: {SceneNames.Login}");
        _ = SceneRouter.LoadAsync(SceneNames.Login).ContinueWith(t => Debug.Log($"[ClientFlow] Scene Load Complete: {SceneNames.Login}"));
    }

    void OnNetDisconnected()
    {
        // 여기서 정책 결정: TownMap에서 끊기면 재접속 UI? Login 복귀?
        Debug.LogWarning("[ClientFlow] Disconnected");
    }

    public void ReturnToTown()
    {
        Debug.Log("[ClientFlow] ReturnToTown: Disconnecting and loading Town Scene.");
        NetworkManager.Instance.Disconnect("ReturnToTown");
        _ = ReturnToTownAsync();
    }

    async Task ReturnToTownAsync()
    {
        // 1. Scene Load (UI 등 표시는 ConnectTown 내부가 아니라 여기서 선처리하거나, ConnectTown이 함)
        // 일단 연결을 끊고 API 호출
        // * TownScreen 로직 참고: IssueTownTicketAsync -> ConnectTown

        var root = AppBootstrap.Instance.Root; // AppBootstrap이 접근 가능해야 함
        
        // TODO: Preferred Region 저장 정책? 일단 local or empty
        string region = ""; 

        Debug.Log("[ClientFlow] Issuing Town Ticket...");
        var r = await root.SessionApi.IssueTownTicketAsync(region);

        if (!r.Ok)
        {
            Debug.LogError($"[ClientFlow] Failed to issue town ticket: {r.Error}");
            // 로그인 화면으로? or 재시도?
            OnNetFailed(r.Error);
            return;
        }

        Debug.Log($"[ClientFlow] Town Ticket Issued: {r.Data.TicketId}");
        
        var clientNonce = "town-ret-" + System.Guid.NewGuid().ToString("N");
        
        // 2. Connect
        await ConnectTown(r.Data, clientNonce);
    }
}

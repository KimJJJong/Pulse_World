// Assets/0_App/Flow/ClientFlow.cs
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

    public void ConnectTown(SessionDtos.IssueTownTicketResponse ticket, string clientNonce)
    {
        _target = Target.TownMap;

        var ep = new IPEndPoint(IPAddress.Parse(ticket.Endpoint.Host), ticket.Endpoint.Port);
        Debug.Log($"[HandshakeArgs]endpoint :{ep} || Ticket :{ticket.TicketId} || Nonce: {clientNonce}  || target : {_target}");

        NetworkManager.Instance.ConnectAndHandshake(ep, ticket.TicketId, clientNonce);
    }

    string _mapId = "";

    public void ConnectGame(SessionDtos.IssueGameTicketResponse ticket, string clientNonce)
    {
        _target = Target.Game;
        _mapId = ticket.MapId;

        var ep = new IPEndPoint(IPAddress.Parse(ticket.Endpoint.Host), ticket.Endpoint.Port);
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
                await SceneRouter.LoadAsync(SceneNames.TownMap);
                Debug.Log($"[ClientFlow] Scene Load Complete: {SceneNames.TownMap}");

                var ctx = Object.FindFirstObjectByType<TownSceneContext>();
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
                await SceneRouter.LoadAsync(sceneToLoad);
                Debug.Log($"[ClientFlow] Scene Load Complete: {sceneToLoad}");
                // GameSceneContext 찾아서 진입 요청
                var ctx = Object.FindFirstObjectByType<GameSceneContext>();
                if (ctx != null)
                {
                    ctx.SetMapId(_mapId);
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
}

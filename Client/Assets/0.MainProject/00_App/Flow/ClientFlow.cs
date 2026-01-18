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
        NetworkManager.Instance.ConnectAndHandshake(ep, ticket.TicketId, clientNonce);
    }

    public void ConnectGame(SessionDtos.IssueGameTicketResponse ticket, string clientNonce)
    {
        _target = Target.Game;

        var ep = new IPEndPoint(IPAddress.Parse(ticket.Endpoint.Host), ticket.Endpoint.Port);
        Debug.Log($"[HandshakeArgs]endpoint :{ep} || Ticket :{ticket.TicketId} || Nonce: {clientNonce} || key :{ticket.Key} ");
        NetworkManager.Instance.ConnectAndHandshake(ep, ticket.TicketId, clientNonce, ticket.Key);
    }

    async void OnNetReady()
    {
        if (_entering) return;
        _entering = true;
        try
        {
            if (_target == Target.TownMap)
            {
                await SceneRouter.LoadAsync(SceneNames.TownMap);

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
                await SceneRouter.LoadAsync(SceneNames.Game);
                // TODO: GameSceneContext.EnterGameAsync()
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
        _ = SceneRouter.LoadAsync(SceneNames.Login);
    }

    void OnNetDisconnected()
    {
        // 여기서 정책 결정: TownMap에서 끊기면 재접속 UI? Login 복귀?
        Debug.LogWarning("[ClientFlow] Disconnected");
    }
}

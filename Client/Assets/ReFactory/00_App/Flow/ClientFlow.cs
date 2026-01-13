using System.Net;
using UnityEngine;

public sealed class ClientFlow : MonoBehaviour
{
    public static ClientFlow Instance { get; private set; } = null!;

    // “지금 연결이 어떤 목적이었나”를 기억해야 씬 전환이 깔끔해짐
    enum Target { None, Town, Game, TownMap }
    Target _target = Target.None;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        NetworkManager.Instance.Ready += OnNetReady;
        NetworkManager.Instance.Failed += OnNetFailed;
    }

    // LoginScreen/TownScreen 등 어디서든 호출 가능
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
        NetworkManager.Instance.ConnectAndHandshake(ep, ticket.TicketId, clientNonce, ticket.Key);
    }

    void OnNetReady()
    {
        // 여기서 “씬을 바꾼다” (NetworkManager가 하면 안 됨)
        if (_target == Target.Town)
            SceneRouter.Load(SceneNames.Town);
        else if (_target == Target.Game)
            SceneRouter.Load(SceneNames.Game); // Game 씬이 있다면
        else if (_target == Target.TownMap) 
            SceneRouter.Load(SceneNames.TownMap);
    }

    void OnNetFailed(string reason)
    {
        Debug.LogWarning($"[Flow] Net failed: {reason}");
        // 초기 운영 정석: 실패 시 Login으로 복귀(또는 Town이면 재시도 UI)
         SceneRouter.Load(SceneNames.Login);
    }
}

// Assets/0_App/Flow/ClientFlow.cs
using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ClientFlow : MonoBehaviour
{
    public static ClientFlow Instance { get; private set; } = null!;

    enum Target { None, TownMap, Game }
    Target _target = Target.None;
    bool _entering;

    string _mapId = "";
    int _maxPlayers = 2;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        NetworkManager.Instance.Ready += OnNetReady;
        NetworkManager.Instance.Failed += OnNetFailed;
        NetworkManager.Instance.Disconnected += OnNetDisconnected;
    }

    // ──────────────────────────────────────────
    // Public: 외부(TownScreen, GameLobby 등)에서 호출
    // ──────────────────────────────────────────

    public async Task ConnectTown(SessionDtos.IssueTownTicketResponse ticket, string clientNonce)
    {
        _target = Target.TownMap;
        SessionContext.Instance.ClearInitMap();
        SessionContext.Instance.ApplySessionKey("");
        SessionContext.Instance.ApplyMatchManifest(null);

        var endpoint = ResolveClientEndpoint(ticket.Endpoint);
        if (endpoint == null) { Debug.LogError("[ClientFlow] ConnectTown: endpoint is missing."); return; }
        var ep = await ResolveEndpointAsync(endpoint.Host, endpoint.Port);
        if (ep == null) return;

        Debug.Log($"[ClientFlow] ConnectTown → {ep} Ticket={ticket.TicketId}");
        NetworkManager.Instance.ConnectAndHandshake(ep, ticket.TicketId, clientNonce);
    }

    public async Task ConnectGame(SessionDtos.IssueGameTicketResponse ticket, string clientNonce)
    {
        _target = Target.Game;
        _mapId = ticket.MapId;
        _maxPlayers = ticket.MaxPlayers;

        SessionContext.Instance.ClearInitMap();
        SessionContext.Instance.ApplySessionKey(ticket.Key);
        SessionContext.Instance.ApplyMatchManifest(ticket.MatchManifest);

        if (P2PRelayClientBridge.Instance != null)
            P2PRelayClientBridge.Instance.ConfigureSession(ticket.Key, ticket.MatchManifest);

        if (string.IsNullOrEmpty(ticket.Key))
        {
            Debug.LogError("[ClientFlow] ConnectGame: ticket.Key가 비어 있습니다. 서버에서 Key를 제대로 내려줍니까?");
            return;
        }

        var endpoint = ResolveClientEndpoint(ticket.Endpoint);
        if (endpoint == null) { Debug.LogError("[ClientFlow] ConnectGame: endpoint is missing."); return; }
        var ep = await ResolveEndpointAsync(endpoint.Host, endpoint.Port);
        if (ep == null) return;

        Debug.Log($"[ClientFlow] ConnectGame → {ep} Ticket={ticket.TicketId} Map={_mapId}");
        NetworkManager.Instance.ConnectAndHandshake(ep, ticket.TicketId, clientNonce, ticket.Key);
    }

    public void ReturnToTown()
    {
        Debug.Log("[ClientFlow] ReturnToTown");
        P2PRelayClientBridge.Instance?.Reset();
        NetworkManager.Instance.Disconnect("ReturnToTown");
        _ = ReturnToTownAsync();
    }

    // ──────────────────────────────────────────
    // Network events
    // ──────────────────────────────────────────

    async void OnNetReady()
    {
        Debug.Log("[ClientFlow] OnNetReady");
        if (_entering) return;
        _entering = true;
        try
        {
            switch (_target)
            {
                case Target.TownMap:
                    await EnterSceneAsync(SceneNames.TownMap, async () =>
                    {
                        var ctx = UnityEngine.Object.FindFirstObjectByType<TownSceneContext>();
                        if (ctx == null) { Debug.LogError("[ClientFlow] TownSceneContext not found"); return; }
                        await ctx.EnterTownAsync();
                    });
                    break;

                case Target.Game:
                    string scene = !string.IsNullOrEmpty(_mapId) ? _mapId : SceneNames.Game;
                    await EnterSceneAsync(scene, async () =>
                    {
                        var ctx = UnityEngine.Object.FindFirstObjectByType<GameSceneContext>();
                        if (ctx == null) { Debug.LogWarning("[ClientFlow] GameSceneContext not found"); return; }
                        ctx.SetMapId(_mapId);
                        ctx.SetMaxPlayers(_maxPlayers);
                        await ctx.EnterGameAsync();
                    });
                    break;

                default:
                    Debug.LogWarning("[ClientFlow] OnNetReady: target is None");
                    break;
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
        P2PRelayClientBridge.Instance?.Reset();
        SessionContext.Instance.ResetForReconnect();
        _ = SceneRouter.LoadAsync(SceneNames.Login);
    }

    void OnNetDisconnected()
    {
        // TODO: 정책 결정 — 재접속 UI 표시 or 로그인 화면 복귀
        Debug.LogWarning("[ClientFlow] Disconnected — 재접속 정책 미구현");
        P2PRelayClientBridge.Instance?.Reset();
    }

    // ──────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────

    /// <summary>
    /// hostname을 IPEndPoint로 변환. IP 문자열이면 즉시 반환, 도메인이면 DNS 조회.
    /// 실패 시 null 반환.
    /// </summary>
    static async Task<IPEndPoint?> ResolveEndpointAsync(string host, int port)
    {
        if (IPAddress.TryParse(host, out var directIp))
            return new IPEndPoint(directIp, port);

        try
        {
            var ips = await Dns.GetHostAddressesAsync(host);
            if (ips.Length > 0)
            {
                Debug.Log($"[ClientFlow] DNS resolved '{host}' → {ips[0]}");
                return new IPEndPoint(ips[0], port);
            }
            Debug.LogError($"[ClientFlow] DNS: no addresses for '{host}'");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientFlow] DNS error for '{host}': {ex.Message}");
        }
        return null;
    }

    static SessionDtos.EndpointDto ResolveClientEndpoint(SessionDtos.EndpointDto endpoint)
    {
        if (endpoint == null || !ShouldUseConfiguredHost(endpoint.Host))
            return endpoint;

        var baseUrl = AppBootstrap.Instance?.Root?.Config?.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            ShouldUseConfiguredHost(baseUri.Host))
        {
            return endpoint;
        }

        Debug.Log($"[ClientFlow] Endpoint host from AppConfig: {endpoint.Host}:{endpoint.Port} -> {baseUri.Host}:{endpoint.Port}");
        return new SessionDtos.EndpointDto
        {
            Host = baseUri.Host,
            Port = endpoint.Port
        };
    }

    static bool ShouldUseConfiguredHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return true;

        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// LoadingScene을 통해 targetScene을 로드한 뒤 onLoaded 콜백을 실행.
    /// </summary>
    static async Task EnterSceneAsync(string targetScene, Func<Task> onLoaded)
    {
        Debug.Log($"[ClientFlow] Loading scene: {targetScene}");
        LoadingSceneController.TargetSceneName = targetScene;
        SceneManager.LoadScene("LoadingScene");

        while (!SceneManager.GetSceneByName(targetScene).isLoaded)
            await Task.Yield();

        var loadedScene = SceneManager.GetSceneByName(targetScene);
        if (loadedScene.IsValid())
        {
            SceneManager.SetActiveScene(loadedScene);
            Debug.Log($"[ClientFlow] Active scene set: {loadedScene.name}");
        }

        Debug.Log($"[ClientFlow] Scene loaded: {targetScene}");
        await onLoaded();
    }

    async Task ReturnToTownAsync()
    {
        var root = AppBootstrap.Instance.Root;
        var r = await root.SessionApi.IssueTownTicketAsync("");

        if (!r.Ok)
        {
            Debug.LogError($"[ClientFlow] Failed to issue town ticket: {r.Error}");
            OnNetFailed(r.Error);
            return;
        }

        var nonce = "town-ret-" + Guid.NewGuid().ToString("N");
        await ConnectTown(r.Data, nonce);
    }
}

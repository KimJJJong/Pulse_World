using System.Threading.Tasks;

public sealed class SessionApi
{
    readonly ApiClient _api;

    public SessionApi(ApiClient api) => _api = api;

    public Task<ApiResult<SessionDtos.IssueTownTicketResponse>> IssueTownTicketAsync(string preferredRegion)
        => _api.PostJsonAsync<SessionDtos.IssueTownTicketResponse>(
            "/session/ticket/town",
            new SessionDtos.IssueTownTicketRequest(preferredRegion),
            attachAuth: true);

    public Task<ApiResult<SessionDtos.IssueTownTicketResponse>> IssueTownTicketAsync(
        string preferredRegion,
        string townRoomId,
        string mapId,
        int maxPlayers,
        string steamId64,
        string clientVersion)
        => _api.PostJsonAsync<SessionDtos.IssueTownTicketResponse>(
            "/session/ticket/town",
            new SessionDtos.IssueTownTicketRequest(preferredRegion, townRoomId, mapId, maxPlayers, steamId64, clientVersion),
            attachAuth: true);

    public Task<ApiResult<SessionDtos.IssueGameTicketResponse>> IssueGameTicketAsync(
        string roomId, string map, int maxPlayers, string preferredRegion, bool useP2PRelay = true)
        => _api.PostJsonAsync<SessionDtos.IssueGameTicketResponse>(
            "/session/ticket/game",
            new SessionDtos.IssueGameTicketRequest(roomId, map, maxPlayers, preferredRegion, useP2PRelay),
            attachAuth: true);
}

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

    public Task<ApiResult<SessionDtos.IssueGameTicketResponse>> IssueGameTicketAsync(
        string roomId, string map, int maxPlayers, string preferredRegion)
        => _api.PostJsonAsync<SessionDtos.IssueGameTicketResponse>(
            "/session/ticket/game",
            new SessionDtos.IssueGameTicketRequest(roomId, map, maxPlayers, preferredRegion),
            attachAuth: true);
}

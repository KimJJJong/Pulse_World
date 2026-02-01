using System.Text.Json.Serialization;

namespace ApiServer.Presentation.Http.Contracts;

public static class SessionDtos
{
    public sealed record EndpointDto(string Host, int Port);

    public sealed record IssueTownTicketRequest(
        string? PreferredRegion,
        string? ClientNonce
    );

    public sealed record IssueTownTicketResponse(
        string TicketId,
        long ExpireAtMs,
        EndpointDto Endpoint
    );

    public sealed record IssueGameTicketRequest(
        string RoomId,
        string Map,
        int MaxPlayers,
        string? PreferredRegion
    );

    public sealed record IssueGameTicketResponse(
        string TransitionId,
        string TicketId,
        long ExpireAtMs,
        string ServerId,
        EndpointDto Endpoint,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("mapId")] string MapId,
        [property: JsonPropertyName("maxPlayers")] int MaxPlayers
    );
}

using System.Text.Json.Serialization;

namespace ApiServer.Presentation.Http.Contracts;

public static class SessionDtos
{
    public sealed record EndpointDto(string Host, int Port);

    public sealed record IssueTownTicketRequest(
        string? PreferredRegion,
        string? ClientNonce,
        string? TownRoomId = null,
        string? MapId = null,
        int MaxPlayers = 16,
        string? SteamId64 = null,
        string? ClientVersion = null
    );

    public sealed record IssueTownTicketResponse(
        string TicketId,
        long ExpireAtMs,
        EndpointDto Endpoint,
        [property: JsonPropertyName("key")] string Key = "",
        [property: JsonPropertyName("townRoomId")] string TownRoomId = "",
        [property: JsonPropertyName("mapId")] string MapId = "",
        [property: JsonPropertyName("maxPlayers")] int MaxPlayers = 0,
        [property: JsonPropertyName("matchManifest")] MatchManifestDto? MatchManifest = null
    );

    public sealed record IssueGameTicketRequest(
        string RoomId,
        string Map,
        int MaxPlayers,
        string? PreferredRegion,
        bool UseP2PRelay = false
    );

    public sealed record IssueGameTicketResponse(
        string TransitionId,
        string TicketId,
        long ExpireAtMs,
        string ServerId,
        EndpointDto Endpoint,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("mapId")] string MapId,
        [property: JsonPropertyName("maxPlayers")] int MaxPlayers,
        [property: JsonPropertyName("matchManifest")] MatchManifestDto? MatchManifest = null
    );

    public sealed record MatchManifestDto(
        string MatchId,
        string RoomId,
        string NetworkMode,
        string ProtocolVersion,
        [property: JsonPropertyName("mapId")] string MapId,
        int StageSeed,
        int SongStartDelayMs,
        string HostUid,
        string HostSteamId64,
        int HostEpoch,
        int PreferredHostRttMs,
        string HostSelectionMode,
        string HostSelectionMetricVersion,
        int HostSelectionEpoch,
        float HostSelectionScore,
        long HostSelectionUpdatedAtMs,
        IReadOnlyList<string> HostCandidateOrder,
        long CreatedAtMs,
        IReadOnlyList<MatchParticipantDto> Participants
    );

    public sealed record MatchParticipantDto(
        string Uid,
        string DisplayName,
        string SteamId64,
        int ActorId,
        string LoadoutHash
    );
}

using ApiServer.Application.Ports.Models;

namespace ApiServer.Application.Ports;

public interface IControlPlanePort
{
    Task<(string transitionId, long expireAtMs)> BeginOrReuseTransitionAsync(
        string uid,
        string toState,   // "MOVING_TO_GAME"
        string ctx,       // roomId
        int ttlSeconds,
        long nowMs,
        CancellationToken ct);

    Task<(string serverId, Models.Endpoint endpoint, string reservationId, long expireAtMs)> AllocateGameServerAsync(
        string uid,
        string region, // optional -> "" 허용
        int reserveTtlSeconds,
        long nowMs,
        CancellationToken ct);

    Task CreateRoomAsync(
        string uid,
        string serverId,
        string reservationId,
        string roomId,
        string map,
        int maxPlayers,
        long nowMs,
        CancellationToken ct);

    Task<(string ticketId, long expireAtMs, string serverId, string key, Models.Endpoint endpoint)> IssueTicketAsync(
        string uid,
        string target,             // "TOWN" | "GAME"
        string key,                // roomId or ""
        string preferredServerId,  // pinned server id or ""
        int ttlSeconds,
        CancellationToken ct);


}

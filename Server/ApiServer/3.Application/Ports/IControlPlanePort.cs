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

    // --- Waiting Room ---
    // Returns (roomId, fullDto)
    Task<(string? roomId, WaitingRoomDto? room)> CreateWaitingRoomAsync(
        string title, 
        string mapId, 
        int maxPlayers, 
        string ownerUid, 
        string ownerName,
        CancellationToken ct);

    Task<(bool ok, WaitingRoomDto? room)> JoinWaitingRoomAsync(string roomId, string uid, string name, CancellationToken ct);
    Task<bool> LeaveWaitingRoomAsync(string roomId, string uid, CancellationToken ct);
    Task<bool> SetMemberReadyAsync(string roomId, string uid, bool ready, CancellationToken ct);
    Task<(bool ok, WaitingRoomDto? room)> GetWaitingRoomAsync(string roomId, CancellationToken ct);
    Task<(List<WaitingRoomDto> rooms, string nextCursor)> GetWaitingRoomListAsync(int limit, string cursor, CancellationToken ct);
    
    // Returns (gameServerId, endpoint, userTickets)
    Task<(string gameServerId, Models.Endpoint endpoint, Dictionary<string, string> userTickets)> StartGameSessionAsync(
        string roomId, 
        string uid, 
        CancellationToken ct);
}

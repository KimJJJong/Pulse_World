using ApiServer.Application.Ports;
using ApiServer.Shared.Abstractions;
using ApiServer.Shared.Errors;

namespace ApiServer.Application.Session.IssueGameTicket;

public sealed class IssueGameTicketHandler
{
    private readonly IControlPlanePort _cp;
    private readonly ITimeProvider _time;

    public IssueGameTicketHandler(IControlPlanePort cp, ITimeProvider time)
    {
        _cp = cp;
        _time = time;
    }

    public async Task<IssueGameTicketResult> HandleAsync(IssueGameTicketCommand cmd, CancellationToken ct)
    {

        //Console.WriteLine( $" roomID : {cmd.RoomId} || Map :{cmd.Map} || Player : {cmd.MaxPlayers} || Uid : {cmd.Uid}");
        if (string.IsNullOrWhiteSpace(cmd.RoomId))
            throw new ApiException(400, ErrorCodes.InvalidRequest, "RoomId required.");
        if (string.IsNullOrWhiteSpace(cmd.Map))
            throw new ApiException(400, ErrorCodes.InvalidRequest, "Map required.");
        if (cmd.MaxPlayers <= 0 || cmd.MaxPlayers > 16)
            throw new ApiException(400, ErrorCodes.InvalidRequest, "MaxPlayers out of range.");

        var nowMs = _time.UtcNow.ToUnixTimeMilliseconds();

        // 1) Transition
        var (transitionId, _) = await _cp.BeginOrReuseTransitionAsync(
            uid: cmd.Uid,
            toState: "MOVING_TO_GAME",
            ctx: cmd.RoomId,
            ttlSeconds: 15,
            nowMs: nowMs,
            ct: ct);

        // 2) Allocate
        var (serverId, endpoint, reservationId, _) = await _cp.AllocateGameServerAsync(
            uid: cmd.Uid,
            region: cmd.PreferredRegion ?? "",
            reserveTtlSeconds: 15,
            nowMs: nowMs,
            ct: ct);

        // 3) CreateRoom
        await _cp.CreateRoomAsync(
            uid: cmd.Uid,
            serverId: serverId,
            reservationId: reservationId,
            roomId: cmd.RoomId,
            map: cmd.Map,
            maxPlayers: cmd.MaxPlayers,
            nowMs: nowMs,
            ct: ct);

        // 4) IssueTicket (pinned)      : 여기 EndPoint 는 IssueTicket에서 받아오는거 Allocate와는 다르지만 값은 같아야지
        var (tid, expAt, _, key, _endPoint) = await _cp.IssueTicketAsync(
            uid: cmd.Uid,
            target: "GAME",
            key: cmd.RoomId,
            preferredServerId: serverId,
            ttlSeconds: 30,
            ct: ct);

        return new IssueGameTicketResult(
            TransitionId: transitionId,
            TicketId: tid,
            ExpireAtMs: expAt,
            ServerId: serverId,
            Endpoint: endpoint,
            Key: key,
            MapId: cmd.Map,
            MaxPlayers: cmd.MaxPlayers
        );
    }
}

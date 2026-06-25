using ApiServer.Application.Ports;
using ApiServer.Domain.Town;
using ApiServer.Shared.Abstractions;
using ApiServer.Shared.Errors;

namespace ApiServer.Application.Session.IssueTownTicket;

public sealed class IssueTownTicketHandler
{
    private readonly IControlPlanePort _cp;
    private readonly ITimeProvider _time;
    private readonly TownRoomService _townRooms;

    public IssueTownTicketHandler(
        IControlPlanePort cp,
        ITimeProvider time,
        TownRoomService townRooms)
    {
        _cp = cp;
        _time = time;
        _townRooms = townRooms;
    }

    public async Task<IssueTownTicketResult> HandleAsync(IssueTownTicketCommand cmd, CancellationToken ct)
    {
        _ = _time.UtcNow.ToUnixTimeMilliseconds();

        try
        {
            if (!string.IsNullOrWhiteSpace(cmd.TownRoomId))
                return await HandleP2PTownTicketAsync(cmd, ct);

            var (tid, expAt, _, _, endPoint) = await _cp.IssueTicketAsync(
                uid: cmd.Uid,
                target: "Town",
                key: "",
                preferredServerId: "ts1",
                ttlSeconds: 30,
                ct: ct);

            return new IssueTownTicketResult(tid, expAt, endPoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EX] {ex.GetType().FullName}: {ex.Message}");
            Console.WriteLine(ex.ToString());
            throw;
        }
    }

    private async Task<IssueTownTicketResult> HandleP2PTownTicketAsync(IssueTownTicketCommand cmd, CancellationToken ct)
    {
        var roomId = cmd.TownRoomId?.Trim() ?? "";
        var room = await _townRooms.GetAsync(roomId);
        if (room == null)
            throw new ApiException(404, "town_room_not_found", "Town room not found.");

        var joined = await _townRooms.JoinAsync(
            roomId,
            cmd.Uid,
            cmd.Uid,
            cmd.SteamId64 ?? "",
            cmd.ClientVersion ?? "");
        if (!joined.ok)
            throw new ApiException(400, ErrorCodes.InvalidRequest, joined.error);

        room = await _townRooms.GetAsync(roomId)
               ?? throw new ApiException(404, "town_room_not_found", "Town room not found.");

        var manifest = await _townRooms.CreateOrReplaceManifestAsync(
            roomId,
            string.IsNullOrWhiteSpace(cmd.SteamId64) ? "server_relay_town_p2p" : "steam_town_p2p_host",
            cmd.ClientVersion ?? "",
            ct);
        if (manifest == null)
            throw new ApiException(500, "town_manifest_create_failed", "Town manifest create failed.");

        var ticketKey = $"townp2p:{roomId}";
        var (tid, expAt, _, key, endPoint) = await _cp.IssueTicketAsync(
            uid: cmd.Uid,
            target: "Town",
            key: ticketKey,
            preferredServerId: "ts1",
            ttlSeconds: 30,
            ct: ct);

        return new IssueTownTicketResult(
            TicketId: tid,
            ExpireAtMs: expAt,
            Endpoint: endPoint,
            Key: key,
            TownRoomId: roomId,
            MapId: manifest.MapId,
            MaxPlayers: room.MaxPlayers,
            MatchManifest: manifest);
    }
}

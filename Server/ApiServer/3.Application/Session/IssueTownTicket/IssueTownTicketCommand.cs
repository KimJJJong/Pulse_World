namespace ApiServer.Application.Session.IssueTownTicket;

public sealed record IssueTownTicketCommand(
    string Uid,
    string? PreferredRegion,
    string? TownRoomId,
    string? MapId,
    int MaxPlayers,
    string? SteamId64,
    string? ClientVersion
);

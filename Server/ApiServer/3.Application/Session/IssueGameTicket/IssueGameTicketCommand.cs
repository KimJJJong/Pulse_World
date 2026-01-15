namespace ApiServer.Application.Session.IssueGameTicket;

public sealed record IssueGameTicketCommand(
    string Uid,
    string RoomId,
    string Map,
    int MaxPlayers,
    string? PreferredRegion
);

namespace ApiServer.Application.Session.IssueTownTicket;

public sealed record IssueTownTicketCommand(
    string Uid,
    string? PreferredRegion
);

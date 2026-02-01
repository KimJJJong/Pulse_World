using ApiServer.Application.Ports.Models;

namespace ApiServer.Application.Session.IssueGameTicket;

public sealed record IssueGameTicketResult(
    string TransitionId,
    string TicketId,
    long ExpireAtMs,
    string ServerId,
    Ports.Models.Endpoint Endpoint,
    string Key,
    string MapId,
    int MaxPlayers
);

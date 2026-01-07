using ApiServer.Application.Ports.Models;

namespace ApiServer.Application.Session.IssueTownTicket;

public sealed record IssueTownTicketResult(
    string TicketId,
    long ExpireAtMs,
    Ports.Models.Endpoint Endpoint
);

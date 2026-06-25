using ApiServer.Domain.Town;

namespace ApiServer.Application.Session.IssueTownTicket;

public sealed record IssueTownTicketResult(
    string TicketId,
    long ExpireAtMs,
    Ports.Models.Endpoint Endpoint,
    string Key = "",
    string TownRoomId = "",
    string MapId = "",
    int MaxPlayers = 0,
    TownMatchManifest? MatchManifest = null
);

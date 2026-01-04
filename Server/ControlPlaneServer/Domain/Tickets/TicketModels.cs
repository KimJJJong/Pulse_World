namespace ControlPlaneServer.Domain.Tickets;

public sealed record TicketRecord(
    string TicketId,
    string Uid,
    string Target,          // "TOWN" / "GAME"
    string Key,             // ctx(roomId)
    string IssuedServerId,  // 발급 시점에 결정한 serverId
    string PinnedServerId,  // optional (Game 서버 고정)
    bool Used,
    long ExpireAtMs
);

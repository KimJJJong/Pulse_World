using ControlPlane.Grpc.V1;

namespace ControlPlane.Domain.Tickets;

public sealed record TicketData(
    string Tid,
    string Uid,
    TicketTarget Target,
    string ServerId,
    long ExpireAtMs,
    string Key
);

public sealed record VerifyConsumeResult(
    bool Ok,
    string Uid,
    string ServerId,
    string Key,
    ErrorCode Code,
    string Reason
);

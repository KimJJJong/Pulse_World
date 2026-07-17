using ControlPlane.Grpc.V1;

namespace Server.Domain.Auth;

public sealed record PresenceSettings(
    string ServerId,
    TicketTarget TicketTarget,
    PresenceState PresenceState,
    int LeaseTtlSeconds
);

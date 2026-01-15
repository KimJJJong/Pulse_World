namespace ControlPlaneServer.Domain.Allocation;

public sealed record ReservationRecord(
    string ReservationId,
    string Uid,
    string ServerId,
    long ExpireAtMs
);

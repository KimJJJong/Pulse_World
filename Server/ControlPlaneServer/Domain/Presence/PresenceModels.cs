namespace ControlPlaneServer.Domain.Presence;

public sealed record PresenceRecord(
    string Uid,
    string State,       // "TOWN"/"GAME"
    string ServerId,
    string ConnId,
    long Epoch,
    long ExpireAtMs
);

public sealed record AttachPresenceResult(
    long NewEpoch,
    PresenceRecord? Previous
);

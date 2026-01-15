namespace ControlPlaneServer.Domain.Transition;

public sealed record TransitionRecord(
    string TransitionId,
    string Uid,
    string State,     // "MOVING_TO_GAME"
    string Ctx,       // roomId
    long ExpireAtMs
);

namespace ControlPlaneServer.Domain.Rooms;

public sealed record RoomRecord(
    string ServerId,
    string RoomId,
    string Uid,
    string Map,
    int MaxPlayers,
    long CreatedAtMs
);

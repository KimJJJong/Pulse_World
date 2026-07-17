namespace ControlPlaneServer.Domain.Registry;

public sealed record ServerRecord(
    string ServerId,
    string Type,          // "TOWN"/"GAME"
    string Host,
    int Port,
    int Capacity,
    string Region,
    string BuildVersion,
    int Load,
    int CurrentSessions,
    long LastHeartbeatMs
);

namespace Server.Infrastructure.Options;

public sealed class ControlPlaneOptions
{
    public string Address { get; init; } = ""; // http://localhost:5001
    public int TimeoutMs { get; init; } = 3000;
    public string Secret { get; init; } = "";

    public string ServerId { get; set; } = "gs1";     // verifier_server_id
    public string ServerType { get; set; } = "GAME";  // "TOWN" | "GAME"
    public int LeaseTtlSeconds { get; set; } = 10;
}

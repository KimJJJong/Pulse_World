namespace Server.Infrastructure.Options;

public sealed class ServerIdentityOptions
{
    public string ServerId { get; init; } = "town1";
    public string Type { get; init; } = "TOWN"; // "TOWN" | "GAME"
    public int LeaseTtlSeconds { get; init; } = 10;
}

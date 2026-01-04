public sealed class TownRoutingOptions
{
    public List<TownServerEntry> Servers { get; init; } = new();
}

public sealed class TownServerEntry
{
    public string ServerId { get; init; } = "";
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 0;
}

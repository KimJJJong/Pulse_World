namespace ApiServer.Infrastructure.Options;

public sealed class TownEndpointOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 12001;
}

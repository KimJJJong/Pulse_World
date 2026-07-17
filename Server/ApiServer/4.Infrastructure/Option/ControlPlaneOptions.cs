namespace ApiServer.Infrastructure.Options;

public sealed class ControlPlaneOptions
{
    public string Address { get; init; } = ""; // https://cp:5001
    public int TimeoutMs { get; init; } = 2000;

    public string Secret { get; init; } = "";
}

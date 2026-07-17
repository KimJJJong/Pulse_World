namespace ControlPlaneServer.Infra;

public sealed class TimeProvider
{
    public long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

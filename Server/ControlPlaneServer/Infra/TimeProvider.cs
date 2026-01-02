namespace ControlPlane.Infra;

public sealed class TimeProvider
{
    public long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

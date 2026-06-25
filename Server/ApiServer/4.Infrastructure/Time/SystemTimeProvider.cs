using ApiServer.Shared.Abstractions;

namespace ApiServer.Infrastructure.Time;

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

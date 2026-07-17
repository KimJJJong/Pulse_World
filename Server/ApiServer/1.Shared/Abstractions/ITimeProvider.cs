namespace ApiServer.Shared.Abstractions;

public interface ITimeProvider
{
    DateTimeOffset UtcNow { get; }
}

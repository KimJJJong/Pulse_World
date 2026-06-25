using ApiServer.Shared.Abstractions;

namespace ApiServer.Infrastructure.Security;

public sealed class GuidIdGenerator : IIdGenerator
{
    public string NewUid() => $"u_{Guid.NewGuid():N}";
    public string NewTokenId() => $"rt_{Guid.NewGuid():N}";
}

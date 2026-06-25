using ApiServer.Domain.Auth;

namespace ApiServer.Application.Ports;

public interface IRefreshTokenStore
{
    Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct);

    Task InsertAsync(RefreshToken token, CancellationToken ct);

    // rotation: old revoke + new insert 을 한 트랜잭션으로
    Task RotateAsync(
        string oldTokenId,
        DateTimeOffset now,
        string? replacedByTokenId,
        RefreshToken? newToken,
        CancellationToken ct);

    Task RevokeAllByUidAsync(string uid, DateTimeOffset now, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

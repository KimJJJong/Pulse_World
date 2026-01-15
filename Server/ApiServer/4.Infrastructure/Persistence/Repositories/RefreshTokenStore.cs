using ApiServer.Application.Ports;
using ApiServer.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly ApiDbContext _db;

    public RefreshTokenStore(ApiDbContext db)
    {
        _db = db;
    }

    public Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct)
        => _db.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public Task InsertAsync(RefreshToken token, CancellationToken ct)
    {
        _db.RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public async Task RotateAsync(
        string oldTokenId,
        DateTimeOffset now,
        string? replacedByTokenId,
        RefreshToken? newToken,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var old = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenId == oldTokenId, ct);
        if (old != null)
        {
            old.Revoke(now, replacedByTokenId);
        }

        if (newToken != null)
            _db.RefreshTokens.Add(newToken);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RevokeAllByUidAsync(string uid, DateTimeOffset now, CancellationToken ct)
    {
        // 전체 revoke는 bulk update가 더 좋지만,
        // 우선 EF로 안전하게 (나중에 ExecuteUpdate로 바꾸면 됨)
        var tokens = await _db.RefreshTokens
            .Where(x => x.Uid == uid && x.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var t in tokens)
            t.Revoke(now, replacedByTokenId: null);

        await _db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);
}

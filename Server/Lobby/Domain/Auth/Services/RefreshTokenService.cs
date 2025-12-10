using Lobby.Infrastructure.Persistence.PostgreSQL;
using System.Security.Cryptography;
using System.Text;

using Lobby.Domain.Auth.Interface;


namespace Lobby.Domain.Auth.Services;


public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _repo;
    private readonly TimeSpan _life = TimeSpan.FromDays(14);

    public RefreshTokenService(IRefreshTokenRepository repo) => _repo = repo;

    public static (string plain, byte[] hash, byte[] salt) Create()
    {
        var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[32];
        rng.GetBytes(bytes);
        string plain = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var salt = new byte[16];
        rng.GetBytes(salt);

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Combine(Encoding.UTF8.GetBytes(plain), salt));
        return (plain, hash, salt);

        static byte[] Combine(byte[] a, byte[] b)
        {
            var res = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, res, 0, a.Length);
            Buffer.BlockCopy(b, 0, res, a.Length, b.Length);
            return res;
        }
    }

    public async Task<(string plain, Guid familyId, DateTimeOffset expiresAt)> IssueAsync(string userId, Guid? familyId = null, string? ip = null, string? ua = null)
    {
        var (plain, hash, salt) = Create();
        var fam = familyId ?? Guid.NewGuid();
        var exp = DateTimeOffset.UtcNow.Add(_life);
        await _repo.InsertAsync(userId, hash, salt, fam, exp, ip, ua);
        return (plain, fam, exp);
    }

    public async Task<(bool ok, string userId, long tokenId, Guid familyId, DateTimeOffset expiresAt)?> ValidateAsync(string plain)
    {
        var found = await _repo.FindByPlainAsync(plain);
        if (found is null) return null;

        var (id, userId, _, _, familyId, expires, revoked) = found.Value;
        if (revoked != null || DateTimeOffset.UtcNow >= expires) return null;
        return (true, userId, id, familyId, expires);
    }

public async Task<(string newToken, Guid familyId, DateTimeOffset expiresAt)> RotateAsync(
    string userId, long oldTokenId, Guid familyId, string? ip, string? ua)
{
    await _repo.RevokeAsync(oldTokenId, "rotated");
    var (plain, fam, exp) = await IssueAsync(userId, familyId, ip, ua);
    return (plain, fam, exp);
}

    public Task RevokeAsync(string plain, string reason)
    {
        return RevokeInternalAsync(plain, reason);
    }

    private async Task RevokeInternalAsync(string plain, string reason)
    {
        var found = await _repo.FindByPlainAsync(plain);
        if (found is not null)
            await _repo.RevokeAsync(found.Value.id, reason);
    }

    public Task RevokeFamilyAsync(Guid familyId, string reason)
        => _repo.RevokeFamilyAsync(familyId, reason);
}
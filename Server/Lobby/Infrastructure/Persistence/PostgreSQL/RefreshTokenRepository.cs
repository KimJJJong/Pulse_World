using Npgsql;
using Dapper;
using NpgsqlTypes;
using System.Security.Cryptography;
using System.Text;

using Lobby.Domain.Auth.Interface;
using System.Net;


namespace Lobby.Infrastructure.Persistence.PostgreSQL;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly NpgsqlDataSource _ds;
    public RefreshTokenRepository(NpgsqlDataSource ds) => _ds = ds;

    // 새 RefreshToken 저장
    public async Task InsertAsync(
        string userId,
        byte[] hash,
        byte[] salt,
        Guid familyId,
        DateTimeOffset expiresAt,
        string? ip,
        string? ua)
    {
        const string sql = @"
INSERT INTO auth.user_refresh_tokens 
    (user_id, token_hash, salt, family_id, expires_at, ip, user_agent)
VALUES 
    (@user_id, @token_hash, @salt, @family_id, @expires_at, @ip, @user_agent);";

        await using var conn = await _ds.OpenConnectionAsync();

        IPAddress? ipAddress = null;
        if (!string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip, out var parsed))
            ipAddress = parsed;

        await conn.ExecuteAsync(sql, new
        {
            user_id = userId,
            token_hash = hash,
            salt,
            family_id = familyId,
            expires_at = expiresAt,
            ip = (object?)ipAddress,                // null이면 DB NULL
            user_agent = (object?)ua ?? DBNull.Value
        });
    }

    // 평문 RefreshToken으로 DB 해시 비교 조회
    public async Task<(long id, string userId, byte[] hash, byte[] salt, Guid familyId,
        DateTimeOffset expiresAt, DateTimeOffset? revokedAt)?>
        FindByPlainAsync(string plain)
    {
        const string sql = @"
SELECT id, user_id, token_hash, salt, family_id, expires_at, revoked_at
FROM auth.user_refresh_tokens
WHERE revoked_at IS NULL AND now() < expires_at;";

        await using var conn = await _ds.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var plainBytes = Encoding.UTF8.GetBytes(plain);
        using var sha = SHA256.Create();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var userId = reader.GetString(1);
            var hash = (byte[])reader["token_hash"];
            var salt = (byte[])reader["salt"];
            var family = reader.GetGuid(4);
            var expires = reader.GetFieldValue<DateTime>(5);
            var revoked = reader.IsDBNull(6) ? (DateTime?)null : reader.GetFieldValue<DateTime>(6);

            var computed = sha.ComputeHash(Combine(plainBytes, salt));
            if (computed.AsSpan().SequenceEqual(hash))
                return (id, userId, hash, salt, family, new DateTimeOffset(expires, TimeSpan.Zero), revoked);
        }
        return null;

        static byte[] Combine(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }

    // 특정 RefreshToken 폐기
    public async Task RevokeAsync(long id, string reason)
    {
        const string sql = "UPDATE auth.user_refresh_tokens SET revoked_at = now(), revoked_reason = @reason WHERE id = @id AND revoked_at IS NULL;";
        await using var conn = await _ds.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { id, reason });
    }

    // 동일 FamilyId 전체 폐기
    public async Task RevokeFamilyAsync(Guid familyId, string reason)
    {
        const string sql = "UPDATE auth.user_refresh_tokens SET revoked_at = now(), revoked_reason = @reason WHERE family_id = @familyId AND revoked_at IS NULL;";
        await using var conn = await _ds.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { familyId, reason });
    }
}

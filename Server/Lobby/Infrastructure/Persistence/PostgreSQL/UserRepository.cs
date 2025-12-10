using Lobby.Domain.Auth.Interface;
using Npgsql;
using Dapper;

namespace Lobby.Infrastructure.Persistence.PostgreSQL;

public sealed class UserRepository : IUserRepository
{
    private readonly NpgsqlDataSource _db;
    public UserRepository(NpgsqlDataSource db) => _db = db;

    public async Task UpsertGuestAsync(string userId, string displayName)
    {
        const string sql = @"
        INSERT INTO auth.users (user_id, display_name, provider, created_at, updated_at)
        VALUES (@user_id, @display_name, 'guest', now(), now())
        ON CONFLICT (user_id)
        DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now();";

        await using var conn = await _db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { user_id = userId, display_name = displayName });
    }

    public async Task UpsertGoogleAsync(string userId, string displayName)
    {
        const string sql = @"
        INSERT INTO auth.users (user_id, display_name, provider, created_at, updated_at)
        VALUES (@user_id, @display_name, 'google', now(), now())
        ON CONFLICT (user_id)
        DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now();";

        await using var conn = await _db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { user_id = userId, display_name = displayName });
    }
}

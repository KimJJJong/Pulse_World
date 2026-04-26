using ApiServer.Application.Ports;
using ApiServer.Domain.Auth;
using ApiServer.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApiDbContext _db;

    public UserRepository(ApiDbContext db)
    {
        _db = db;
    }

    public Task<User?> FindByUidAsync(string uid, CancellationToken ct)
        => _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == uid, ct);

    public Task<string?> FindUidByIdentityAsync(
        IdentityProvider provider,
        string providerSubject,
        CancellationToken ct)
        => _db.UserIdentities
            .AsNoTracking()
            .Where(x => x.Provider == provider && x.ProviderSubject == providerSubject)
            .Select(x => x.Uid)
            .FirstOrDefaultAsync(ct);

    public async Task<User> CreateUserWithIdentityAsync(
        string uid,
        IdentityProvider provider,
        string providerSubject,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // 단일 트랜잭션 보장: 유저 + 아이덴티티 같이 생성
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var user = new User(uid, now);
        _db.Users.Add(user);

        var ident = new UserIdentity(uid, provider, providerSubject, now);
        _db.UserIdentities.Add(ident);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return user;
    }

    public async Task AddIdentityAsync(
        string uid,
        IdentityProvider provider,
        string providerSubject,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // 여기서 중복 유니크 충돌이 나면 상위에서 처리(이미 연동됨 등)
        _db.UserIdentities.Add(new UserIdentity(uid, provider, providerSubject, now));
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkLoginAsync(string uid, DateTimeOffset now, CancellationToken ct)
    {
        // Tracking 최소화를 위해 업데이트만 수행
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Uid == uid, ct);
        if (user == null) return;

        user.MarkLogin(now);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAppearanceIdAsync(string uid, int appearanceId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Uid == uid, ct);
        if (user == null) return;

        user.SetAppearanceId(appearanceId);
        await _db.SaveChangesAsync(ct);
    }
}

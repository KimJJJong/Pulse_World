using ApiServer.Domain.Auth;
using ApiServer.Domain.Users;

namespace ApiServer.Application.Ports;

public interface IUserRepository
{
    Task<User?> FindByUidAsync(string uid, CancellationToken ct);

    // provider + subject 로 uid 찾기 (login 핵심)
    Task<string?> FindUidByIdentityAsync(
        IdentityProvider provider,
        string providerSubject,
        CancellationToken ct);

    // 새 유저 생성 + identity 연결 (guest/google 신규가입)
    Task<User> CreateUserWithIdentityAsync(
        string uid,
        IdentityProvider provider,
        string providerSubject,
        DateTimeOffset now,
        CancellationToken ct);

    // 기존 유저에 identity 추가 (계정 연동 같은 확장용)
    Task AddIdentityAsync(
        string uid,
        IdentityProvider provider,
        string providerSubject,
        DateTimeOffset now,
        CancellationToken ct);

    Task MarkLoginAsync(string uid, DateTimeOffset now, CancellationToken ct);

    Task UpdateAppearanceIdAsync(string uid, int appearanceId, CancellationToken ct);
}

using ApiServer.Application.Ports;
using ApiServer.Domain.Auth;
using ApiServer.Domain.Users;
using ApiServer.Infrastructure.Options;
using ApiServer.Shared.Abstractions;
using ApiServer.Shared.Security;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ApiServer.Application.Auth.LoginGuest;

public sealed class LoginGuestHandler
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenStore _refreshStore;
    private readonly IRefreshTokenHasher _hasher;
    private readonly IJwtIssuerPort _jwt;
    private readonly IIdGenerator _ids;
    private readonly ITimeProvider _time;
    private readonly SecurityOptions _sec;

    public LoginGuestHandler(
        IUserRepository users,
        IRefreshTokenStore refreshStore,
        IRefreshTokenHasher hasher,
        IJwtIssuerPort jwt,
        IIdGenerator ids,
        ITimeProvider time,
        IOptions<SecurityOptions> sec)
    {
        _users = users;
        _refreshStore = refreshStore;
        _hasher = hasher;
        _jwt = jwt;
        _ids = ids;
        _time = time;
        _sec = sec.Value;
    }

    public async Task<LoginGuestResult> HandleAsync(LoginGuestCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.DeviceId))
            throw new ApiServer.Shared.Errors.ApiException(400, ApiServer.Shared.Errors.ErrorCodes.InvalidRequest, "DeviceId required.");

        var now = _time.UtcNow;

        // 1) identity로 uid 찾기
        var uid = await _users.FindUidByIdentityAsync(IdentityProvider.Guest, cmd.DeviceId, ct);

        // 2) 없으면 생성
        if (uid == null)
        {
            uid = _ids.NewUid();
            await _users.CreateUserWithIdentityAsync(uid, IdentityProvider.Guest, cmd.DeviceId, now, ct);
        }
        else
        {
            await _users.MarkLoginAsync(uid, now, ct);
        }

        // 3) AccessToken 발급
        var extraClaims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(cmd.ClientVersion))
            extraClaims.Add(new Claim("cv", cmd.ClientVersion));

        extraClaims.Add(new Claim("provider", "guest"));

        var access = _jwt.IssueAccessToken(uid, extraClaims);
        var accessExp = now.AddMinutes(10); // JwtOptions와 동기화는 다음 단계에서(간단히 유지)

        // 4) RefreshToken 생성 + 저장
        var refreshPlain = RandomTokenGenerator.CreateBase64UrlToken(byteLength: Math.Clamp(_sec.RefreshTokenLength, 32, 128));
        var refreshHash = _hasher.Hash(refreshPlain);

        var refreshId = _ids.NewTokenId();
        var refreshExp = now.AddDays(_sec.RefreshTokenDays);

        var rt = new RefreshToken(
            tokenId: refreshId,
            uid: uid,
            tokenHash: refreshHash,
            issuedAt: now,
            expiresAt: refreshExp,
            deviceId: cmd.DeviceId,
            ip: cmd.Ip,
            userAgent: cmd.UserAgent
        );

        await _refreshStore.InsertAsync(rt, ct);
        await _refreshStore.SaveChangesAsync(ct);

        return new LoginGuestResult(uid, access, accessExp, refreshPlain, refreshExp);
    }
}

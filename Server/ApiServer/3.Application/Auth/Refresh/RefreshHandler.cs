using ApiServer.Application.Ports;
using ApiServer.Domain.Auth;
using ApiServer.Infrastructure.Options;
using ApiServer.Shared.Abstractions;
using ApiServer.Shared.Errors;
using ApiServer.Shared.Security;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ApiServer.Application.Auth.Refresh;

public sealed class RefreshHandler
{
    private readonly IRefreshTokenStore _store;
    private readonly IRefreshTokenHasher _hasher;
    private readonly IJwtIssuerPort _jwt;
    private readonly IIdGenerator _ids;
    private readonly ITimeProvider _time;
    private readonly SecurityOptions _sec;

    public RefreshHandler(
        IRefreshTokenStore store,
        IRefreshTokenHasher hasher,
        IJwtIssuerPort jwt,
        IIdGenerator ids,
        ITimeProvider time,
        IOptions<SecurityOptions> sec)
    {
        _store = store;
        _hasher = hasher;
        _jwt = jwt;
        _ids = ids;
        _time = time;
        _sec = sec.Value;
    }

    public async Task<RefreshResult> HandleAsync(RefreshCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RefreshToken))
            throw new ApiException(400, ErrorCodes.InvalidRequest, "RefreshToken required.");

        var now = _time.UtcNow;

        var hash = _hasher.Hash(cmd.RefreshToken);
        var row = await _store.FindByTokenHashAsync(hash, ct);

        if (row == null)
            throw new ApiException(401, ErrorCodes.InvalidRefresh, "Invalid refresh token.");

        // 만료
        if (row.IsExpired(now))
            throw new ApiException(401, ErrorCodes.InvalidRefresh, "Refresh token expired.");

        // 이미 revoke된 토큰을 다시 제출 => reuse 가능성
        if (row.IsRevoked)
        {
            if (_sec.RevokeAllOnReuse)
            {
                await _store.RevokeAllByUidAsync(row.Uid, now, ct);
                throw new ApiException(401, ErrorCodes.RefreshReuseDetected, "Session invalidated. Please login again.");
            }

            throw new ApiException(401, ErrorCodes.InvalidRefresh, "Refresh token revoked.");
        }

        // rotation: 새 refresh 발급
        var newPlain = RandomTokenGenerator.CreateBase64UrlToken(byteLength: Math.Clamp(_sec.RefreshTokenLength, 32, 128));
        var newHash = _hasher.Hash(newPlain);

        var newId = _ids.NewTokenId();
        var newExp = now.AddDays(_sec.RefreshTokenDays);

        var newToken = new RefreshToken(
            tokenId: newId,
            uid: row.Uid,
            tokenHash: newHash,
            issuedAt: now,
            expiresAt: newExp,
            deviceId: cmd.DeviceId,
            ip: cmd.Ip,
            userAgent: cmd.UserAgent
        );

        // old revoke + new insert (트랜잭션)
        await _store.RotateAsync(
            oldTokenId: row.TokenId,
            now: now,
            replacedByTokenId: newId,
            newToken: newToken,
            ct: ct);

        // access 발급
        var claims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(cmd.ClientVersion))
            claims.Add(new Claim("cv", cmd.ClientVersion));

        var access = _jwt.IssueAccessToken(row.Uid, claims);
        var accessExp = now.AddMinutes(10);

        return new RefreshResult(access, accessExp, newPlain, newExp);
    }
}

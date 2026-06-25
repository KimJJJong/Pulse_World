using System.Security.Claims;
using ApiServer.Application.Ports;
using ApiServer.Domain.Auth;
using ApiServer.Infrastructure.Options;
using ApiServer.Infrastructure.Steam;
using ApiServer.Shared.Abstractions;
using ApiServer.Shared.Errors;
using ApiServer.Shared.Security;
using Microsoft.Extensions.Options;

namespace ApiServer.Application.Auth.LoginSteam;

public sealed class LoginSteamHandler
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenStore _refreshStore;
    private readonly IRefreshTokenHasher _hasher;
    private readonly IJwtIssuerPort _jwt;
    private readonly IIdGenerator _ids;
    private readonly ITimeProvider _time;
    private readonly SecurityOptions _sec;
    private readonly SteamTicketVerifier _steamVerifier;

    public LoginSteamHandler(
        IUserRepository users,
        IRefreshTokenStore refreshStore,
        IRefreshTokenHasher hasher,
        IJwtIssuerPort jwt,
        IIdGenerator ids,
        ITimeProvider time,
        IOptions<SecurityOptions> sec,
        SteamTicketVerifier steamVerifier)
    {
        _users = users;
        _refreshStore = refreshStore;
        _hasher = hasher;
        _jwt = jwt;
        _ids = ids;
        _time = time;
        _sec = sec.Value;
        _steamVerifier = steamVerifier;
    }

    public async Task<LoginSteamResult> HandleAsync(LoginSteamCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.SteamId64))
            throw new ApiException(400, ErrorCodes.InvalidRequest, "SteamId64 required.");
        if (string.IsNullOrWhiteSpace(cmd.Ticket))
            throw new ApiException(400, ErrorCodes.InvalidRequest, "Steam ticket required.");
        if (string.IsNullOrWhiteSpace(cmd.Identity))
            throw new ApiException(400, ErrorCodes.InvalidRequest, "Steam identity required.");

        var validatedSteamId = await _steamVerifier.ValidateUserTicketAsync(cmd.SteamId64, cmd.Ticket, cmd.Identity, ct);
        var now = _time.UtcNow;

        var uid = await _users.FindUidByIdentityAsync(IdentityProvider.Steam, validatedSteamId, ct);
        if (uid == null)
        {
            uid = _ids.NewUid();
            await _users.CreateUserWithIdentityAsync(uid, IdentityProvider.Steam, validatedSteamId, now, ct);
        }
        else
        {
            await _users.MarkLoginAsync(uid, now, ct);
        }

        var extraClaims = new List<Claim>
        {
            new("provider", "steam"),
            new("steamid", validatedSteamId)
        };

        if (!string.IsNullOrWhiteSpace(cmd.ClientVersion))
            extraClaims.Add(new Claim("cv", cmd.ClientVersion));

        var access = _jwt.IssueAccessToken(uid, extraClaims);
        var accessExp = now.AddMinutes(10);

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
            deviceId: validatedSteamId,
            ip: cmd.Ip,
            userAgent: cmd.UserAgent
        );

        await _refreshStore.InsertAsync(rt, ct);
        await _refreshStore.SaveChangesAsync(ct);

        return new LoginSteamResult(uid, access, accessExp, refreshPlain, refreshExp);
    }
}

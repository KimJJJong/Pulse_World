using ApiServer.Application.Ports;
using ApiServer.Shared.Abstractions;
using ApiServer.Shared.Errors;

namespace ApiServer.Application.Auth.Logout;

public sealed class LogoutHandler
{
    private readonly IRefreshTokenStore _store;
    private readonly IRefreshTokenHasher _hasher;
    private readonly ITimeProvider _time;

    public LogoutHandler(IRefreshTokenStore store, IRefreshTokenHasher hasher, ITimeProvider time)
    {
        _store = store;
        _hasher = hasher;
        _time = time;
    }

    public async Task HandleAsync(LogoutCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RefreshToken))
            throw new ApiException(400, ErrorCodes.InvalidRequest, "RefreshToken required.");

        var now = _time.UtcNow;
        var hash = _hasher.Hash(cmd.RefreshToken);
        var row = await _store.FindByTokenHashAsync(hash, ct);

        if (row == null)
            return; // idempotent

        if (cmd.AllDevices)
        {
            await _store.RevokeAllByUidAsync(row.Uid, now, ct);
            return;
        }

        // 단건 revoke (RotateAsync를 활용해서 newToken null로)
        await _store.RotateAsync(row.TokenId, now, replacedByTokenId: null, newToken: null, ct: ct);
    }
}

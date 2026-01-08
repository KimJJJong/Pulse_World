using System.Threading.Tasks;

public sealed class AuthApi
{
    readonly ApiClient _api;
    readonly TokenStore _tokens;
    readonly ClientIdentity _id;

    public AuthApi(ApiClient api, TokenStore tokens, ClientIdentity id)
    {
        _api = api;
        _tokens = tokens;
        _id = id;
    }

    public Task<ApiResult<AuthDtos.AuthResponse>> LoginGuestAsync()
        => _api.PostJsonAsync<AuthDtos.AuthResponse>(
            "/auth/login/guest",
            new AuthDtos.LoginGuestRequest(_id.DeviceId, _id.ClientVersion),
            attachAuth: false);

    public async Task<ApiResult<object>> LogoutAsync(bool allDevices)
    {
        // 서버는 refreshToken 기반 logout
        var r = await _api.PostJsonAsync<object>(
            "/auth/logout",
            new AuthDtos.LogoutRequest(_tokens.RefreshToken, allDevices),
            attachAuth: true);

        if (r.Ok) _tokens.Clear();
        return r;
    }

    public void ApplyLogin(AuthDtos.AuthResponse resp)
    {
        _tokens.SaveAll(resp.Uid, resp.AccessToken, resp.AccessExpMs, resp.RefreshToken, resp.RefreshExpMs);
    }
}

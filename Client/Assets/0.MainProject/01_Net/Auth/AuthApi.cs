using System.Threading.Tasks;

public sealed class AuthApi
{
    readonly ApiClient _api;
    readonly TokenStore _tokens;
    readonly ClientIdentity _id;

    public string LastPreferredLoginMode { get; private set; } = "NotStarted";
    public string LastPreferredLoginDetail { get; private set; } = "No login attempt yet.";

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

    public Task<ApiResult<AuthDtos.AuthResponse>> LoginSteamAsync(string steamId64, string ticketHex, string identity)
        => _api.PostJsonAsync<AuthDtos.AuthResponse>(
            "/auth/login/steam",
            new AuthDtos.LoginSteamRequest(steamId64, ticketHex, identity, _id.ClientVersion),
            attachAuth: false);

    public async Task<ApiResult<AuthDtos.AuthResponse>> LoginPreferredAsync(ISteamPlatformService steamPlatform, AppConfig config)
    {
        const string webIdentity = "rhythmrpg.api";

        if (config != null && config.EnableSteam && config.PreferSteamLogin && steamPlatform != null && steamPlatform.IsInitialized)
        {
            SetLoginStatus("SteamTicket", "Requesting Steam Web API ticket.");
            var ticket = await steamPlatform.CreateWebApiTicketAsync(webIdentity);
            if (ticket.Success)
            {
                SetLoginStatus("SteamTicketOk", $"Steam ticket issued for {ticket.SteamId64}.");
                var steamLogin = await LoginSteamAsync(ticket.SteamId64, ticket.TicketHex, ticket.Identity);
                if (steamLogin.Ok)
                {
                    SetLoginStatus("Steam", $"Steam login succeeded. uid={steamLogin.Data?.Uid ?? ""}");
                    return steamLogin;
                }

                SetLoginStatus("GuestFallback", $"Steam login failed: {steamLogin.Error}");
            }
            else
            {
                SetLoginStatus("GuestFallback", $"Steam ticket failed: {ticket.Error}");
            }
        }
        else if (config != null && config.EnableSteam && config.PreferSteamLogin)
        {
            string reason = steamPlatform == null
                ? "Steam service is unavailable."
                : (!steamPlatform.IsInitialized
                    ? $"Steam is not initialized. {steamPlatform.LastError}"
                    : "Steam login was skipped.");
            SetLoginStatus("GuestFallback", reason);
        }
        else
        {
            SetLoginStatus("Guest", "Steam login is disabled in config.");
        }

        var guestLogin = await LoginGuestAsync();
        if (guestLogin.Ok)
        {
            if (LastPreferredLoginMode == "GuestFallback")
                SetLoginStatus("GuestFallback", $"{LastPreferredLoginDetail} Guest login succeeded. uid={guestLogin.Data?.Uid ?? ""}");
            else
                SetLoginStatus("Guest", $"Guest login succeeded. uid={guestLogin.Data?.Uid ?? ""}");
        }
        else
        {
            SetLoginStatus("GuestError", guestLogin.Error);
        }

        return guestLogin;
    }

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

    private void SetLoginStatus(string mode, string detail)
    {
        LastPreferredLoginMode = string.IsNullOrWhiteSpace(mode) ? "Unknown" : mode;
        LastPreferredLoginDetail = string.IsNullOrWhiteSpace(detail) ? "-" : detail;
    }
}

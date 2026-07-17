namespace ApiServer.Shared.Errors;

public static class ErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string InvalidRefresh = "invalid_refresh";
    public const string RefreshReuseDetected = "refresh_reuse_detected";
    public const string Unauthorized = "unauthorized";
    public const string SteamAuthUnavailable = "steam_auth_unavailable";
    public const string SteamAuthFailed = "steam_auth_failed";
}

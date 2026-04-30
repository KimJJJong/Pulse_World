namespace ApiServer.Presentation.Http.Contracts;

public static class AuthDtos
{
    public sealed record LoginGuestRequest(
        string DeviceId,
        string? ClientVersion
    );

    public sealed record LoginSteamRequest(
        string SteamId64,
        string Ticket,
        string Identity,
        string? ClientVersion
    );

    public sealed record RefreshRequest(
        string RefreshToken,
        string? DeviceId,
        string? ClientVersion
    );

    public sealed record LogoutRequest(
        string RefreshToken,
        bool AllDevices
    );

    public sealed record AuthResponse(
        string Uid,
        string AccessToken,
        long AccessExpMs,
        string RefreshToken,
        long RefreshExpMs
    );

    public sealed record RefreshResponse(
        string AccessToken,
        long AccessExpMs,
        string RefreshToken,
        long RefreshExpMs
    );
}

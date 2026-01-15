namespace ApiServer.Application.Auth.Logout;

public sealed record LogoutCommand(
    string RefreshToken,
    bool AllDevices
);

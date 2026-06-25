namespace ApiServer.Application.Auth.LoginGuest;

public sealed record LoginGuestCommand(
    string DeviceId,
    string? ClientVersion,
    string? Ip,
    string? UserAgent
);

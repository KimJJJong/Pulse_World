namespace ApiServer.Application.Auth.Refresh;

public sealed record RefreshCommand(
    string RefreshToken,
    string? DeviceId,
    string? ClientVersion,
    string? Ip,
    string? UserAgent
);

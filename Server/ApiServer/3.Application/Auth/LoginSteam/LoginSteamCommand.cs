namespace ApiServer.Application.Auth.LoginSteam;

public sealed record LoginSteamCommand(
    string SteamId64,
    string Ticket,
    string Identity,
    string? ClientVersion,
    string? Ip,
    string? UserAgent
);

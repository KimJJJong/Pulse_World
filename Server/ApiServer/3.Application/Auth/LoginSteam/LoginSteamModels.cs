namespace ApiServer.Application.Auth.LoginSteam;

public sealed record LoginSteamResult(
    string Uid,
    string AccessToken,
    DateTimeOffset AccessExp,
    string RefreshToken,
    DateTimeOffset RefreshExp
);

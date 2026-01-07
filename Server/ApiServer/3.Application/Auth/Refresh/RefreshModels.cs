namespace ApiServer.Application.Auth.Refresh;

public sealed record RefreshResult(
    string AccessToken,
    DateTimeOffset AccessExp,
    string RefreshToken,
    DateTimeOffset RefreshExp
);

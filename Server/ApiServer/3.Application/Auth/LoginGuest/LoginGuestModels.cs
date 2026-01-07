namespace ApiServer.Application.Auth.LoginGuest;

public sealed record LoginGuestResult(
    string Uid,
    string AccessToken,
    DateTimeOffset AccessExp,
    string RefreshToken,
    DateTimeOffset RefreshExp
);

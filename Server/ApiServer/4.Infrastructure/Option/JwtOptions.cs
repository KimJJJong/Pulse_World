namespace ApiServer.Infrastructure.Options;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";

    public int AccessTokenMinutes { get; init; } = 10;

    // RS256
    public string PrivateKeyPemPath { get; init; } = "";
    public string PublicKeyPemPath { get; init; } = "";
}

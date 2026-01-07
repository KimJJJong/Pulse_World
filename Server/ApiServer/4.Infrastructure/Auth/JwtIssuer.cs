using ApiServer.Application.Ports;
using ApiServer.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ApiServer.Infrastructure.Auth;

public sealed class JwtIssuer : IJwtIssuerPort
{
    private readonly JwtOptions _opt;
    private readonly JwtSecurityTokenHandler _handler = new();

    private readonly RsaSecurityKey _signingKey;
    private readonly SigningCredentials _creds;

    public JwtIssuer(IOptions<JwtOptions> opt)
    {
        _opt = opt.Value;

        if (string.IsNullOrWhiteSpace(_opt.PrivateKeyPemPath))
            throw new InvalidOperationException("Jwt:PrivateKeyPemPath missing.");

        // RS256 private key 로드
        var rsa = RSA.Create();
        var pem = File.ReadAllText(_opt.PrivateKeyPemPath);
        rsa.ImportFromPem(pem);

        _signingKey = new RsaSecurityKey(rsa);
        _creds = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
    }

    public string IssueAccessToken(string uid, IEnumerable<Claim> extraClaims)
    {
        var now = DateTime.UtcNow;
        var exp = now.AddMinutes(_opt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, uid),
            new(JwtRegisteredClaimNames.Iat, Epoch(now).ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("ver", "1")
        };

        if (extraClaims != null)
            claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now,
            expires: exp,
            signingCredentials: _creds
        );

        return _handler.WriteToken(token);
    }

    private static long Epoch(DateTime utc)
        => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
}

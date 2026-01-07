using ApiServer.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ApiServer.Infrastructure.Auth;

public sealed class AccessTokenValidator
{
    private readonly JwtOptions _opt;
    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly TokenValidationParameters _tvp;

    public AccessTokenValidator(IOptions<JwtOptions> opt)
    {
        _opt = opt.Value;

        if (string.IsNullOrWhiteSpace(_opt.PublicKeyPemPath))
            throw new InvalidOperationException("Jwt:PublicKeyPemPath missing.");

        var rsa = RSA.Create();
        var pem = File.ReadAllText(_opt.PublicKeyPemPath);
        rsa.ImportFromPem(pem);

        var key = new RsaSecurityKey(rsa);

        _tvp = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,

            ValidateIssuer = true,
            ValidIssuer = _opt.Issuer,

            ValidateAudience = true,
            ValidAudience = _opt.Audience,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30), // 운영에서 흔히 주는 여유

            // ClaimsPrincipal에 매핑될 claim 타입
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    }

    public ClaimsPrincipal Validate(string jwt)
    {
        // 서명/iss/aud/exp 모두 검증됨
        var principal = _handler.ValidateToken(jwt, _tvp, out var validatedToken);

        // 토큰 타입 체크(방어적)
        if (validatedToken is not JwtSecurityToken jst ||
            !string.Equals(jst.Header.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
        {
            throw new SecurityTokenException("Invalid token algorithm.");
        }

        return principal;
    }

    public static string? ExtractUid(ClaimsPrincipal principal)
    {
        // 우리가 IssueAccessToken에서 sub=uid를 넣었음
        var uid = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!string.IsNullOrWhiteSpace(uid))
            return uid;

        // 혹시 NameIdentifier로도 들어올 수 있으니 fallback
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}

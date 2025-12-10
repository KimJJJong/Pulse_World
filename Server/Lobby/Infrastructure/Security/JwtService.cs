using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Lobby.Domain.Auth.Interface;
using Microsoft.IdentityModel.Tokens;

namespace Lobby.Infrastructure.Security;

public sealed class JwtService : IJwtService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string? _privPem;
    private readonly string? _pubPem;

    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtService(IConfiguration cfg)
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        _issuer = cfg["Auth:Ticket:Issuer"] ?? "LobbyAuth";
        _audience = cfg["Auth:Ticket:Audience"] ?? "GameServer";

        string? privPath = cfg["Auth:Ticket:PrivateKeyPemPath"];
        string? pubPath = cfg["Auth:Ticket:PublicKeyPemPath"];

        _privPem = !string.IsNullOrWhiteSpace(privPath) && File.Exists(privPath)
            ? File.ReadAllText(privPath)
            : throw new InvalidOperationException("RS256 private key not found.");

        _pubPem = !string.IsNullOrWhiteSpace(pubPath) && File.Exists(pubPath)
            ? File.ReadAllText(pubPath)
            : throw new InvalidOperationException("RS256 public key not found.");
    }

    // =====================================================
    // =============== 공용 RS256 발급/검증 =================
    // =====================================================
    private SigningCredentials CreateRsaCredentials()
    {
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(_privPem!);
        return new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
    }

    private TokenValidationParameters CreateValidationParams()
    {
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(_pubPem!);
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(10),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
    }

    // =====================================================
    // ============== GameServer Ticket  ===================
    // =====================================================
    public (string token, string jti, string nonce) IssueTicket(IDictionary<string, object> claims, TimeSpan ttl)
    {
        SigningCredentials creds = CreateRsaCredentials();
        string jti = Guid.NewGuid().ToString("N");
        string nonce = Guid.NewGuid().ToString("N")[..16];

        JwtPayload payload = new JwtPayload(_issuer, _audience, null, DateTime.UtcNow, DateTime.UtcNow.Add(ttl))
        {
            ["jti"] = jti,
            ["nonce"] = nonce,
            ["ver"] = 1
        };
        foreach (var kv in claims)
            payload[kv.Key] = kv.Value;

        JwtSecurityToken token = new JwtSecurityToken(new JwtHeader(creds), payload);
        return (_handler.WriteToken(token), jti, nonce);
    }

    public (bool ok, IDictionary<string, object>? dict, string code) ValidateTicket(string token)
    {
        try
        {
            ClaimsPrincipal principal = _handler.ValidateToken(token, CreateValidationParams(), out var validated);
            JwtSecurityToken jwt = (JwtSecurityToken)validated;
            var dict = jwt.Payload.ToDictionary(kv => kv.Key, kv => kv.Value);
            return (true, dict, "ok");
        }
        catch (SecurityTokenExpiredException) { return (false, null, "expired"); }
        catch { return (false, null, "invalid"); }
    }


    // =====================================================
    // ================== AccessToken ======================
    // =====================================================
    public (string token,DateTime expireIn , string jti) IssueAccessToken(string userId, IDictionary<string, object> customClaims, TimeSpan ttl)
    {
        string jti = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, jti)
        };

        foreach (var kv in customClaims)
            claims.Add(new Claim(kv.Key, kv.Value.ToString() ?? ""));

        DateTime duration = DateTime.UtcNow.Add(ttl);

        var creds = CreateRsaCredentials();
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: duration,
            signingCredentials: creds
        );
        return (_handler.WriteToken(token),duration, jti);
    }

    public (bool ok, string? userId, IDictionary<string, object>? claims, string? code) ValidateAccessToken(string token)
    {
        try
        {
            var principal = _handler.ValidateToken(token, CreateValidationParams(), out var validated);
            var jwt = (JwtSecurityToken)validated;
            var dict = jwt.Payload.ToDictionary(kv => kv.Key, kv => kv.Value);


            // sub은 표준 claim
            var uid = jwt.Payload.Sub;
            if (string.IsNullOrWhiteSpace(uid) && dict.TryGetValue("sub", out var subObj))
                uid = subObj?.ToString();
            
            return (true, uid, dict, "ok");
        }
        catch (SecurityTokenExpiredException) { return (false, null, null, "expired"); }
        catch { return (false, null, null, "invalid"); }
    }

    // =====================================================
    // =============== 공용 Rs256 Utility ==================
    // =====================================================
    public (string token, string jti, string nonce) IssueTokenRs256(IDictionary<string, object> claims, TimeSpan ttl)
    {
        var creds = CreateRsaCredentials();
        string jti = Guid.NewGuid().ToString("N");
        string nonce = Guid.NewGuid().ToString("N")[..16];

        var payload = new JwtPayload(_issuer, _audience, null, DateTime.UtcNow, DateTime.UtcNow.Add(ttl))
        {
            ["jti"] = jti,
            ["nonce"] = nonce,
            ["ver"] = 1
        };
        foreach (var kv in claims)
            payload[kv.Key] = kv.Value;

        var token = new JwtSecurityToken(new JwtHeader(creds), payload);
        return (_handler.WriteToken(token), jti, nonce);
    }

    public (bool ok, IDictionary<string, object>? dict, string code) ValidateTokenRs256(string token)
    {
        try
        {
            ClaimsPrincipal principal = _handler.ValidateToken(token, CreateValidationParams(), out var validated);
            JwtSecurityToken jwt = (JwtSecurityToken)validated;
            var dict = jwt.Payload.ToDictionary(kv => kv.Key, kv => kv.Value);
            return (true, dict, "ok");
        }
        catch (SecurityTokenExpiredException) { return (false, null, "expired"); }
        catch { return (false, null, "invalid"); }
    }

}

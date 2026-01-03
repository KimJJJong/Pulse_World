using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Microsoft.IdentityModel.Tokens;
using Interface;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.IO;


namespace Server.withWebServer.Security;

public sealed class JwtService : IJwtService
{
    // 공통
    private readonly string _alg;      // "HS256" or "RS256(default)"
    private readonly string _issuer;
    private readonly string _audience;

    // RS256
    private readonly string _privPem; // RS256 발급(Lobby)
    private readonly string _pubPem;  // RS256 검증(Game)

    // HS256
    private readonly byte[] _hsKey;


    public JwtService(IConfiguration cfg)
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        _alg = cfg["Auth:Ticket:Algorithm"] ?? "RS256";
        _issuer = cfg["Auth:Ticket:Issuer"] ?? "LobbyAuth";
        _audience = cfg["Auth:Ticket:Audience"] ?? "GameServer";


        // RS256 Key있을때
        string privPath = cfg["Auth:Ticket:PrivateKeyPemPath"];
        string pubPath = cfg["Auth:Ticket:PublicKeyPemPath"];
        _privPem = !string.IsNullOrWhiteSpace(privPath) && File.Exists(privPath) ? File.ReadAllText(privPath) : null;
        _pubPem = !string.IsNullOrWhiteSpace(pubPath) && File.Exists(pubPath) ? File.ReadAllText(pubPath) : null;


        // HS256 키
        byte[] hs = null;
        string secret = cfg["Auth:RoomSecret"];
        if (!string.IsNullOrWhiteSpace(secret))
        {
            if (secret.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
            {
                // placeholder 같은 잘못된 base64가 와도 RS256만 쓰면 터지지 않도록 try/catch
                var raw = secret["base64:".Length..];
                try { hs = Convert.FromBase64String(raw); }
                catch { hs = null; } // invalid base64 -> HS256 안 쓰면 문제 없음
            }
            else
            {
                hs = Encoding.UTF8.GetBytes(secret);
            }
        }
        _hsKey = hs;

        // HS256을 : 선택 검증
        if (_alg.Equals("HS256", StringComparison.OrdinalIgnoreCase))
        {
            if (_hsKey is null || _hsKey.Length < 32)
                throw new InvalidOperationException("HS256 selected but Auth:RoomSecret is missing/invalid (need >= 32 bytes).");
        }


    }

    // -------- 공통 발급/검증(알고리즘 자동) --------
    public (string token, string jti, string nonce) IssueTicket(IDictionary<string, object> claims, TimeSpan ttl)
        => _alg.Equals("RS256", StringComparison.OrdinalIgnoreCase)
           ? IssueTokenRs256(claims, ttl)
           : IssueTokenHs256(claims, ttl);

    public (bool ok, IDictionary<string, object> dict, string code) ValidateTicket(string token)
        => _alg.Equals("RS256", StringComparison.OrdinalIgnoreCase)
           ? ValidateTokenRs256(token)
           : ValidateTokenHs256(token);

    // -------- HS256 --------
    private void EnsureHsKey()
    {
        if (_hsKey is null || _hsKey.Length < 32)
            throw new InvalidOperationException("HS256 operations require a valid Auth:RoomSecret (>=32 bytes).");
    }

    public (string token, string jti, string nonce)
        IssueTokenHs256
        (IDictionary<string, object> claims, TimeSpan ttl, string issuer = null!, string audience = null!)
    {
        EnsureHsKey();

        var creds = new SigningCredentials(new SymmetricSecurityKey(_hsKey), SecurityAlgorithms.HmacSha256);
        string jti = Guid.NewGuid().ToString("N");
        string nonce = Guid.NewGuid().ToString("N")[..16];

        var payload = new JwtPayload(
            issuer ?? _issuer,
            audience ?? _audience,
            claims: null,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(ttl));
        payload["jti"] = jti;
        payload["nonce"] = nonce;
        payload["ver"] = 1;
        foreach (var kv in claims) payload[kv.Key] = kv.Value;

        var token = new JwtSecurityToken(new JwtHeader(creds), payload);
        return (new JwtSecurityTokenHandler().WriteToken(token), jti, nonce);
    }

    public (bool ok, IDictionary<string, object> dict, string code)
        ValidateTokenHs256
        (string token, string issuer = null!, string audience = null!)
    {
        EnsureHsKey();
        var handler = new JwtSecurityTokenHandler();
        if (handler.ReadJwtToken(token).Header.Alg != SecurityAlgorithms.HmacSha256)
            return (false, null!, "alg_mismatch");

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer ?? _issuer,
                ValidateAudience = true,
                ValidAudience = audience ?? _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_hsKey!),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role",
            }, out var validated);

            var jwt = (JwtSecurityToken)validated;
            var dict = jwt.Payload.ToDictionary(kv => kv.Key, kv => kv.Value);
            return (true, dict, "ok");
        }
        catch (SecurityTokenExpiredException) { return (false, null, "expired"); }
        catch { return (false, null, "invalid"); }
    }

    // -------- RS256 --------
    public (string token, string jti, string nonce) IssueTokenRs256(IDictionary<string, object> claims, TimeSpan ttl)
    {
        if (_privPem is null)
            throw new InvalidOperationException("RS256 private key missing (Auth:Ticket:PrivateKeyPemPath).");

        RSA rsa = RSA.Create();
        rsa.ImportFromPem(_privPem);

        SigningCredentials creds = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        string jti = Guid.NewGuid().ToString("N");
        string nonce = Guid.NewGuid().ToString("N")[..16];

        JwtPayload payload = new JwtPayload(_issuer, _audience, null, DateTime.UtcNow, DateTime.UtcNow.Add(ttl));
        payload["jti"] = jti; payload["nonce"] = nonce; payload["ver"] = 1;
        foreach (var kv in claims) payload[kv.Key] = kv.Value;

        JwtSecurityToken token = new JwtSecurityToken(new JwtHeader(creds), payload);
        return (new JwtSecurityTokenHandler().WriteToken(token), jti, nonce);
    }

    public (bool ok, IDictionary<string, object> dict, string code) ValidateTokenRs256(string token)
    {
        if (_pubPem is null) return (false, null, "pubkey_missing");

        var handler = new JwtSecurityTokenHandler();
        if (handler.ReadJwtToken(token).Header.Alg != SecurityAlgorithms.RsaSha256)
            return (false, null, "alg_mismatch");
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(_pubPem);
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
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
                RoleClaimType = "role",
            }, out var validated);
            var jwt = (JwtSecurityToken)validated;
            var dict = jwt.Payload.ToDictionary(kv => kv.Key, kv => kv.Value);
            return (true, dict, "ok");
        }
        catch (SecurityTokenExpiredException) { return (false, null, "expired"); }
        catch { return (false, null, "invalid"); }
    }

    // ---- 기존 방 토큰 메서드 유지(삭제 가능: 아마도?) ----
    public string IssueRoomToken(string roomId, string userId, TimeSpan ttl)
    {
        var creds = new SigningCredentials(new SymmetricSecurityKey(_hsKey), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: new[] { new Claim("sub", userId), new Claim("roomId", roomId) },
            expires: DateTime.UtcNow.Add(ttl),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public (bool ok, string userId, string roomId) ValidateRoomToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = new SymmetricSecurityKey(_hsKey),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role",
            }, out _);
            var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var roomId = principal.FindFirst("roomId")?.Value;
            return (true, userId, roomId);
        }
        catch { return (false, null, null); }
    }
}

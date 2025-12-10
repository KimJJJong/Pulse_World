// Shared/JwtUtil.cs
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Shared;

public static class JwtUtil
{
    public static string IssueRs256(string privatePem, string issuer, string audience, object claims, TimeSpan life, out string jti, out string nonce)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privatePem);
        var cred = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var now = DateTimeOffset.UtcNow;

        jti = Guid.NewGuid().ToString("N");
        nonce = Guid.NewGuid().ToString("N").Substring(0, 16);

        // default PayLoad
        var payload = new JwtPayload(issuer, audience, null, now.UtcDateTime, now.Add(life).UtcDateTime);
       
        // 사용자 정의 클레임 넣기( object -> dictionary 변환 후 주입 )
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(claims))!;
        dict["jti"] = jti; dict["nonce"] = nonce; dict["ver"] = 1;
        foreach (var kv in dict) payload[kv.Key] = kv.Value;

        var token = new JwtSecurityToken(new JwtHeader(cred), payload);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static (
        bool ok,                            // 검증 여부
        IDictionary<string, object> dict,  // 전체 playLoad Dictionary
        string code)                        // 사유 코드
        ValidateRs256(string publicPem, string issuer, string audience, string token)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicPem);
        var parms = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(10),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa)
        };

            var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, parms, out var validatedToken);

            var jwt = (JwtSecurityToken)validatedToken;
            var dict = jwt.Payload.ToDictionary( kv => kv.Key, kv => kv.Value);
            //var dict = principal.Claims.ToDictionary(c => c.Type, c => (object)c.Value);
            return (true, dict, "ok");
        }
        catch (SecurityTokenExpiredException) { return (false, null, "expired"); }
        catch { return (false, null, "invalid"); }
    }
}

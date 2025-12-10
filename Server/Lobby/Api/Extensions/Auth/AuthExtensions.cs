using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Lobby.Api.Extensions.Auth;

public static class AuthExtensions
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        IConfiguration ticket = config.GetSection("Auth:Ticket");

        var issuer = ticket["Issuer"] ?? throw new InvalidOperationException("Missing Auth:Ticket:Issuer");
        var audience = ticket["Audience"] ?? throw new InvalidOperationException("Missing Auth:Ticket:Audience");
        var algo = ticket["Algorithm"] ?? "RS256";

        if (!string.Equals(algo, "RS256", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported JWT algorithm: {algo}");

        var publicKeyPath = ticket["PublicKeyPemPath"]
            ?? throw new InvalidOperationException("Missing Auth:Ticket:PublicKeyPemPath");

        // PEM -> RSA 공개키 로드
        var rsa = RSA.Create();
        var pem = File.ReadAllText(publicKeyPath);
        rsa.ImportFromPem(pem);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new RsaSecurityKey(rsa),
                    ClockSkew = TimeSpan.FromSeconds(5) // 유예시간
                };
            });

        services.AddAuthorization();
        return services;
    }
}

using ApiServer.Infrastructure.Options;
using ApiServer.Shared.Errors;
using Microsoft.Extensions.Options;
using ApiServer.Presentation.Http;

namespace ApiServer.Presentation.Http.Middleware;

public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiKey;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration config, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        // "SystemApiKey" from appsettings
        _apiKey = config.GetValue<string>("SystemApiKey") ?? throw new Exception("SystemApiKey not configured");
    }

    public async Task Invoke(HttpContext ctx)
    {
        // 1. Check if the path is for Server-to-Server (Internal)
        // You might want to filter by path prefix like "/internal" or check header presence
        if (!ctx.Request.Headers.TryGetValue("X-Server-Secret", out var receivedKey))
        {
            // If check fails, pass to next middleware (likely AccessTokenAuthMiddleware will handle user auth)
            // OR if you want to strictly separate endpoints, you can block here.
            // For now, let's assume if the header is missing, it's a user request.
            await _next(ctx);
            return;
        }

        // 2. Validate Key
        if (!string.Equals(receivedKey, _apiKey))
        {
            var ip = ctx.GetRemoteIpAddress();
            _logger.LogWarning("Auth Failed: Invalid Server Secret. IP={ip}", ip);
            throw new ApiException(401, ErrorCodes.Unauthorized, "Invalid Server Secret.");
        }

        // 3. Set System User to Context
        // Bypass AccessTokenAuthMiddleware by setting a flag or User principal
        // We can create a "System" principal
        var identity = new System.Security.Claims.ClaimsIdentity("System");
        identity.AddClaim(new System.Security.Claims.Claim("uid", "SYSTEM"));
        identity.AddClaim(new System.Security.Claims.Claim("role", "System"));
        
        ctx.User = new System.Security.Claims.ClaimsPrincipal(identity);
        ctx.Items["uid"] = "SYSTEM";

        _logger.LogInformation("System Auth Success");

        await _next(ctx);
    }
}

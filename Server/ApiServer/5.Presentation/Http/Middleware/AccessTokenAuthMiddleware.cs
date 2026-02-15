using ApiServer.Infrastructure.Auth;
using ApiServer.Shared.Errors;
using Microsoft.IdentityModel.Tokens;

namespace ApiServer.Presentation.Http.Middleware;

public sealed class AccessTokenAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AccessTokenAuthMiddleware> _logger;

    public AccessTokenAuthMiddleware(RequestDelegate next, ILogger<AccessTokenAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx, AccessTokenValidator validator)
    {
        var path = ctx.Request.Path.Value ?? "";

        // 익명 허용 경로
        if (IsAnonymousAllowed(path))
        {
            await _next(ctx);
            return;
        }

        // 이미 이전 미들웨어(ApiKeyAuth)에서 인증된 경우 패스
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            await _next(ctx);
            return;
        }

        var auth = ctx.Request.Headers.Authorization.ToString();
        var token = "";

        if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = auth["Bearer ".Length..].Trim();
        }
        // WebSocket support
        else if (ctx.Request.Query.ContainsKey("access_token"))
        {
            token = ctx.Request.Query["access_token"].ToString();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Auth Failed: Missing Token. Path={path}", path);
            throw new ApiException(401, ErrorCodes.Unauthorized, "Missing Authorization Bearer token.");
        }

        try
        {
            var principal = validator.Validate(token);
            var uid = AccessTokenValidator.ExtractUid(principal);

            if (string.IsNullOrWhiteSpace(uid))
            {
                 _logger.LogWarning("Auth Failed: Token missing uid. Path={path}", path);
                 throw new ApiException(401, ErrorCodes.Unauthorized, "Token missing uid(sub).");
            }
            
            _logger.LogInformation("Auth Success: Uid={uid}, Path={path}", uid, path);

            ctx.User = principal;
            ctx.Items["uid"] = uid;

            await _next(ctx);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Auth Failed: Token Expired. Path={path}", path);
            throw new ApiException(401, ErrorCodes.Unauthorized, "Access token expired.");
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Auth Failed: Invalid Token. Path={path}", path);
            throw new ApiException(401, ErrorCodes.Unauthorized, "Invalid access token.");
        }
    }

    private static bool IsAnonymousAllowed(string path)
    {
        // auth endpoints
        if (path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase))
            return true;

        // swagger
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;

        // health
        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

using ApiServer.Infrastructure.Auth;
using ApiServer.Shared.Errors;
using Microsoft.IdentityModel.Tokens;

namespace ApiServer.Presentation.Http.Middleware;

public sealed class AccessTokenAuthMiddleware
{
    private readonly RequestDelegate _next;

    public AccessTokenAuthMiddleware(RequestDelegate next)
    {
        _next = next;
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

        var auth = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new ApiException(401, ErrorCodes.Unauthorized, "Missing Authorization Bearer token.");

        var token = auth["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new ApiException(401, ErrorCodes.Unauthorized, "Empty bearer token.");

        try
        {
            var principal = validator.Validate(token);
            var uid = AccessTokenValidator.ExtractUid(principal);

            if (string.IsNullOrWhiteSpace(uid))
                throw new ApiException(401, ErrorCodes.Unauthorized, "Token missing uid(sub).");

            ctx.User = principal;
            ctx.Items["uid"] = uid;

            await _next(ctx);
        }
        catch (SecurityTokenExpiredException)
        {
            throw new ApiException(401, ErrorCodes.Unauthorized, "Access token expired.");
        }
        catch (SecurityTokenException)
        {
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

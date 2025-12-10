using Lobby.Domain.Auth.Interface;
using Microsoft.AspNetCore.Http.Features;


namespace Lobby.Api.Middleware;

public sealed class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IJwtService _jwt;
    private readonly ILogger<JwtAuthMiddleware> _logger;

    public JwtAuthMiddleware(RequestDelegate next, IJwtService jwt, ILogger<JwtAuthMiddleware> logger)
    {
        _next = next;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "(unknown)";
        // 로그인, 헬스체크, WS는 예외 처리
        if (path.StartsWith("/login") || path.StartsWith("/health") || path.StartsWith("/ws")
        || path.StartsWith("/auth") || path.StartsWith("/auth/google/callback", StringComparison.Ordinal))
        {
            await _next(ctx);
            return;
        }

        var traceId = ctx.TraceIdentifier;
        var authHeader = ctx.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            _logger.LogWarning(
                "[AUTH-FAIL] Missing token | path={Path} | trace={TraceId} | ip={IP}",
                path, traceId, ctx.Connection.RemoteIpAddress?.ToString());

            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "missing_access_token", traceId });
            return;
        }

        var token = authHeader["Bearer ".Length..];
        var (ok, uid, claims, code) = _jwt.ValidateAccessToken(token);

        if (!ok || string.IsNullOrWhiteSpace(uid))
        {
            _logger.LogWarning(
                "[AUTH-FAIL] Invalid JWT | path={Path} | trace={TraceId} | code={Code} | ip={IP}",
                path, traceId, code, ctx.Connection.RemoteIpAddress?.ToString());

            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = code, traceId });
            return;
        }

        // 인증 성공: 컨텍스트에 저장
        ctx.Items["UserId"] = uid!;
        ctx.Items["UserClaims"] = claims;

        await _next(ctx);
    }
}


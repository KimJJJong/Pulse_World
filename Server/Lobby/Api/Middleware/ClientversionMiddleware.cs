using Lobby.Api.Config;
using Microsoft.Extensions.Options;
using System;

namespace Lobby.Api.Middleware;

public sealed class ClientVersionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AppOptions _opt;
    private readonly ILogger<ClientVersionMiddleware> _logger;
    public ClientVersionMiddleware(RequestDelegate next, IOptions<AppOptions> opt, ILogger<ClientVersionMiddleware> logger)
    {
        _next = next; _opt = opt.Value; _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value?? "(Unknown)";

        if( path.StartsWith("/auth/google/callback", StringComparison.Ordinal))
        {
            _logger.LogInformation("[ClientVersion] Skip version check for { Path}", path);
            await _next(ctx);
            return;
        }

        // WS 핸들셰이크는 쿼리스트링/헤더 둘 다 허용
        string? v = null;
        if (ctx.WebSockets.IsWebSocketRequest)
            v = ctx.Request.Headers[_opt.Versioning.HeaderName].FirstOrDefault()
              ?? ctx.Request.Query[_opt.Versioning.HeaderName].FirstOrDefault();
        else
            v = ctx.Request.Headers[_opt.Versioning.HeaderName].FirstOrDefault();

        if (v is null || CompareSemVer(v, _opt.Versioning.MinClientVersion) < 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "client_version_unsupported",
                required = _opt.Versioning.MinClientVersion,
                got = v ?? "(missing)"
            });
            return;
        }

        await _next(ctx);
    }

    // 단순 SemVer 비교: "major.minor.patch"
    public static int CompareSemVer(string a, string b)
    {
        int[] A = a.Split('.', 3, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).DefaultIfEmpty(0).ToArray();
        int[] B = b.Split('.', 3, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).DefaultIfEmpty(0).ToArray();
        Array.Resize(ref A, 3); Array.Resize(ref B, 3);
        for (int i = 0; i < 3; i++) { if (A[i] != B[i]) return A[i].CompareTo(B[i]); }
        return 0;
    }
}

using ApiServer.Presentation.Http;

namespace ApiServer.Presentation.Http.Middleware;

public sealed class FirewallMiddleware
{
    private readonly RequestDelegate _next;
    private readonly FirewallManager _firewall;

    public FirewallMiddleware(RequestDelegate next, FirewallManager firewall)
    {
        _next = next;
        _firewall = firewall;
    }

    public async Task Invoke(HttpContext ctx)
    {
        var ip = ctx.GetRemoteIpAddress();
        var path = ctx.Request.Path.Value ?? "";

        // 1. Check if IP is already banned
        if (_firewall.IsBanned(ip, out var expiry))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // 2. Check if the path is a malicious probe
        if (_firewall.IsMaliciousPath(path))
        {
            // Ban the IP for 24 hours
            _firewall.BanIp(ip, TimeSpan.FromDays(1), $"Probing malicious path: {path}");
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(ctx);
    }
}

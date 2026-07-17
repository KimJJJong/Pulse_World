namespace ApiServer.Presentation.Http;

public static class HttpContextExtensions
{
    public static string RequireUid(this HttpContext ctx)
    {
        if (ctx.Items.TryGetValue("uid", out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
            return s;

        throw new InvalidOperationException("uid not found in HttpContext. AccessTokenAuthMiddleware missing?");
    }

    public static string GetRemoteIpAddress(this HttpContext context)
    {
        string? ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(ip))
        {
            ip = context.Connection.RemoteIpAddress?.ToString();
        }
        else
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
            ip = ip.Split(',').FirstOrDefault()?.Trim();
        }

        return ip ?? "unknown";
    }
}

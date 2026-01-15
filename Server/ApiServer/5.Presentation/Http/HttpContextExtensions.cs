namespace ApiServer.Presentation.Http;

public static class HttpContextExtensions
{
    public static string RequireUid(this HttpContext ctx)
    {
        if (ctx.Items.TryGetValue("uid", out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
            return s;

        throw new InvalidOperationException("uid not found in HttpContext. AccessTokenAuthMiddleware missing?");
    }
}

using ApiServer.Shared.Errors;
using System.Text.Json;

namespace ApiServer.Presentation.Http.Middleware;

public sealed class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web);

    public ProblemDetailsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ApiException ex)
        {
            ctx.Response.StatusCode = ex.StatusCode;
            ctx.Response.ContentType = "application/problem+json";

            var body = new
            {
                type = "about:blank",
                title = ex.ErrorCode,
                status = ex.StatusCode,
                detail = ex.Message
            };

            await ctx.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOpt));
        }
    }
}

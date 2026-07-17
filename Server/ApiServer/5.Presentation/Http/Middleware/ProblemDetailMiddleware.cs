using ApiServer.Shared.Errors;
using System.Text.Json;

namespace ApiServer.Presentation.Http.Middleware;

public sealed class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web);

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled Exception: Path={path}", ctx.Request.Path.Value);

            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/problem+json";

            var body = new
            {
                type = "about:blank",
                title = "InternalServerError",
                status = 500,
                detail = "An unexpected error occurred on the server."
            };

            await ctx.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOpt));
        }
    }
}

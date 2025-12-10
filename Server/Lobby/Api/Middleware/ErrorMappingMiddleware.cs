using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Lobby.Api.Middleware;

public sealed class ErrorMappingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorMappingMiddleware> _log;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ErrorMappingMiddleware(RequestDelegate next, ILogger<ErrorMappingMiddleware> log)
    {
        _next = next; _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // 응답이 이미 시작됐다면 상태코드/헤더 건드리지 말고 종료
            if (ctx.Response.HasStarted)
            {
                _log.LogWarning("Client canceled after response started. Aborting connection. TraceId={TraceId}", ctx.TraceIdentifier);
                SafeAbort(ctx);
                return;
            }

            ctx.Response.Clear(); // 아직 시작 전이면 안전
            ctx.Response.StatusCode = 499; // Client Closed Request (비표준)
            await WriteError(ctx, "client_closed", "Client canceled the request.");
        }
        catch (Exception ex)
        {
            if (ctx.Response.HasStarted)
            {
                _log.LogError(ex, "Unhandled exception after response started. Aborting connection. TraceId={TraceId}", ctx.TraceIdentifier);
                // 이미 바디가 나간 후엔 상태코드/본문 수정 불가 -> 연결 중단
                SafeAbort(ctx);
                return;
            }

            _log.LogError(ex, "Unhandled exception before response started. TraceId={TraceId}", ctx.TraceIdentifier);
            ctx.Response.Clear();
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteError(ctx, "internal_error", ex.Message);
        }
    }

    private static async Task WriteError(HttpContext ctx, string code, string message)
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var payload = new { error = code, message, traceId = ctx.TraceIdentifier };
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, _json));
    }

    private static void SafeAbort(HttpContext ctx)
    {
        try { ctx.Abort(); } catch { /* ignore */ }
    }
}

public static class ErrorMappingExtensions
{
    public static IApplicationBuilder UseStandardErrors(this IApplicationBuilder app)
        => app.UseMiddleware<ErrorMappingMiddleware>();
}

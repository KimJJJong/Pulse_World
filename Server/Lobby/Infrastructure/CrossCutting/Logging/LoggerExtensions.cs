using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Lobby.Infrastructure.CrossCutting.Logging;

public static class LoggerExtensions
{
    /// <summary>
    /// 요청 단위 공통 스코프(traceId, path, method, clientIp, userAgent)를 잡아준다.
    /// </summary>
    public static IDisposable BeginScopeWithTrace(this ILogger logger, HttpContext ctx)
    {
        var scope = new Dictionary<string, object?>
        {
            ["traceId"] = ctx.TraceIdentifier,
            ["path"] = ctx.Request.Path.Value,
            ["method"] = ctx.Request.Method,
            ["clientIp"] = ctx.Connection.RemoteIpAddress?.ToString(),
            ["ua"] = ctx.Request.Headers.UserAgent.ToString()
        };
        return logger.BeginScope(scope);
    }
}

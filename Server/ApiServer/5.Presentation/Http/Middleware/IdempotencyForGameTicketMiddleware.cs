using ApiServer.Shared.Errors;
using ApiServer.Shared.Http.Idempotency;
using System.Text;

namespace ApiServer.Presentation.Http.Middleware;

/// <summary>
/// POST /session/ticket/game에만 적용.
/// Idempotency-Key 없으면 400.
/// </summary>
public sealed class IdempotencyForGameTicketMiddleware
{
    private readonly RequestDelegate _next;

    public IdempotencyForGameTicketMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx, IIdempotencyStore store)
    {
        // 경로/메서드 필터
        if (!IsTarget(ctx))
        {
            await _next(ctx);
            return;
        }

        var uid = ctx.Items.TryGetValue("uid", out var v) ? v as string : null;
        if (string.IsNullOrWhiteSpace(uid))
            throw new ApiException(401, ErrorCodes.Unauthorized, "uid missing.");

        var idemKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idemKey))
            throw new ApiException(400, ErrorCodes.InvalidRequest, "Missing Idempotency-Key header.");

        // 저장 키: 유저 단위 격리 + 경로 포함
        var storeKey = $"idem:{uid}:{ctx.Request.Path}:{idemKey}";

        var ttl = TimeSpan.FromSeconds(30);

        var (entry, inFlight) = await store.TryBeginAsync(storeKey, ttl, ctx.RequestAborted);
        if (entry != null)
        {
            // 저장된 결과 재사용
            ctx.Response.StatusCode = entry.StatusCode;
            ctx.Response.ContentType = entry.ContentType;
            await ctx.Response.Body.WriteAsync(entry.Body, ctx.RequestAborted);
            return;
        }

        if (inFlight)
        {
            // 처리중이면 충돌/재시도 유도
            throw new ApiException(409, "idempotency_in_flight", "Request is already being processed. Retry shortly.");
        }

        // 응답 캡처
        var originalBody = ctx.Response.Body;
        await using var mem = new MemoryStream();
        ctx.Response.Body = mem;

        try
        {
            await _next(ctx);

            // 2xx만 캐시 (권장). 실패 응답은 캐시하지 않음.
            if (ctx.Response.StatusCode >= 200 && ctx.Response.StatusCode < 300)
            {
                var bodyBytes = mem.ToArray();
                var contentType = ctx.Response.ContentType ?? "application/json";

                var saved = new IdempotencyEntry
                {
                    Completed = true,
                    StatusCode = ctx.Response.StatusCode,
                    ContentType = contentType,
                    Body = bodyBytes,
                    ExpireAt = DateTimeOffset.UtcNow.Add(ttl)
                };

                await store.CompleteAsync(storeKey, saved, ctx.RequestAborted);
            }
            else
            {
                await store.AbandonAsync(storeKey, ctx.RequestAborted);
            }

            // 원래 응답 스트림으로 복사
            mem.Position = 0;
            ctx.Response.Body = originalBody;
            await mem.CopyToAsync(originalBody, ctx.RequestAborted);
        }
        catch
        {
            ctx.Response.Body = originalBody;
            await store.AbandonAsync(storeKey, ctx.RequestAborted);
            throw;
        }
    }

    private static bool IsTarget(HttpContext ctx)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method))
            return false;

        return string.Equals(ctx.Request.Path, "/session/ticket/game", StringComparison.OrdinalIgnoreCase);
    }
}

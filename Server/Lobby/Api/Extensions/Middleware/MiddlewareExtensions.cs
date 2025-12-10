using Lobby.Api.Middleware;
using Lobby.Api.WebSockets;
using Lobby.Infrastructure.Persistence.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lobby.Api.Extensions.Middleware;

public static class MiddlewareExtensions
{
    public static WebApplication UseLobbyPipeline(this WebApplication app)
    {
        var wsOpts = new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) };

        // --- Startup Log ---
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var cfg = app.Services.GetRequiredService<IConfiguration>();
            StartupEcho.Dump("Lobby(Web)", cfg);
        });

        // --- Middleware chain ---
        app.UseStandardErrors();
        app.UseMiddleware<ClientVersionMiddleware>();
        app.UseMiddleware<JwtAuthMiddleware>();
        app.UseRateLimiter();                     //  RateLimiter 활성화
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseCors();
        app.UseWebSockets(wsOpts);

        // --- Controllers ---
        app.MapControllers();

        // --- WebSocket endpoint ---
        app.Map("/ws/room/{roomId}", async (HttpContext ctx, string roomId, RoomSocketHub hub) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await hub.HandleAsync(roomId, ws, ctx.RequestAborted);
        });

        // --- Health Check ---
        app.MapGet("/health/live", () => Results.Ok(new { ok = true }));
        app.MapGet("/health/ready",
            (InMemoryRoomStore store) => Results.Ok(new { ok = true, rooms = store.Rooms.Count }));

        return app;
    }
}

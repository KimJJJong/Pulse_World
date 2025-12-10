using Lobby.Api.Config;
using System.Threading.RateLimiting;

namespace Lobby.Api.Extensions;

public static partial class ServiceExtensions
{
    public static IServiceCollection AddLobbyRateLimiter(this IServiceCollection services, AppOptions appOpt)
    {
        services.AddRateLimiter(opts =>
        {
            opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = appOpt.RateLimit.GlobalRpsPerIp,
                        Window = TimeSpan.FromSeconds(1),
                        AutoReplenishment = true
                    }));

            opts.AddPolicy("create_room", _ =>
                RateLimitPartition.GetTokenBucketLimiter("create_room",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = appOpt.RateLimit.CreateRoomPerMin,
                        TokensPerPeriod = appOpt.RateLimit.CreateRoomPerMin,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        AutoReplenishment = true
                    }));

            opts.AddPolicy("join_room", _ =>
                RateLimitPartition.GetTokenBucketLimiter("join_room",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = appOpt.RateLimit.JoinPerMin,
                        TokensPerPeriod = appOpt.RateLimit.JoinPerMin,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        AutoReplenishment = true
                    }));
        });

        return services;
    }
}

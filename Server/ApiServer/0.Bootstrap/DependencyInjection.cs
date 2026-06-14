using ApiServer.Application.Auth.LoginGuest;
using ApiServer.Application.Auth.LoginSteam;
using ApiServer.Application.Auth.Logout;
using ApiServer.Application.Auth.Refresh;
using ApiServer.Application.Ports;
using ApiServer.Application.Services;
using ApiServer.Application.Session.IssueGameTicket;
using ApiServer.Application.Session.IssueTownTicket;
using ApiServer.Domain.GameMatch;
using ApiServer.Domain.GameResult;
using ApiServer.Domain.Town;
using ApiServer.Domain.WaitingRoom;
using ApiServer.Infrastructure.Auth;
using ApiServer.Infrastructure.ControlPlaneClient;
using ApiServer.Infrastructure.Idempotency;
using ApiServer.Infrastructure.Options;
using ApiServer.Infrastructure.Persistence;
using ApiServer.Infrastructure.Persistence.Repositories;
using ApiServer.Infrastructure.Security;
using ApiServer.Infrastructure.Steam;
using ApiServer.Infrastructure.Time;
using ApiServer.Presentation.WebSockets;
using ApiServer.Shared.Abstractions;
using ApiServer.Shared.Http.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiServer.Bootstrap;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddApplication();
        services.AddInfrastructure(config);
        return services;
    }

    private static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginGuestHandler>();
        services.AddScoped<LoginSteamHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<IssueTownTicketHandler>();
        services.AddScoped<IssueGameTicketHandler>();
        return services;
    }

    private static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddSingleton<IItemTemplateService, ItemTemplateService>();

        services.AddDbContext<ApiDbContext>((sp, opt) =>
        {
            var db = sp.GetRequiredService<IOptions<DbOptions>>().Value;
            opt.UseNpgsql(db.ConnectionString);
        });

        services.AddScoped<ITimeProvider, SystemTimeProvider>();
        services.AddScoped<IIdGenerator, GuidIdGenerator>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IStarterEquipmentService, StarterEquipmentService>();
        services.AddHttpClient<SteamTicketVerifier>();

        services.Configure<RedisOptions>(config.GetSection("Redis"));
        services.AddSingleton<RedisStore>();
        services.AddScoped<WaitingRoomService>();
        services.AddScoped<TownRoomService>();
        services.AddScoped<GameResultService>();
        services.AddScoped<GameMatchService>();

        services.AddSingleton<IJwtIssuerPort, JwtIssuer>();
        services.AddSingleton<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddSingleton<IControlPlanePort, GrpcControlPlaneAdapter>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        services.AddSingleton<AccessTokenValidator>();

        services.AddSingleton<ConnectionManager>();
        services.AddScoped<RoomWebSocketHandler>();

        return services;
    }
}

using ApiServer.Application.Auth.LoginGuest;
using ApiServer.Application.Auth.Logout;
using ApiServer.Application.Auth.Refresh;
using ApiServer.Application.Ports;
using ApiServer.Application.Session.IssueGameTicket;
using ApiServer.Application.Session.IssueTownTicket;
using ApiServer.Infrastructure.Auth;
using ApiServer.Infrastructure.ControlPlaneClient;
using ApiServer.Infrastructure.Idempotency;
using ApiServer.Infrastructure.Options;
using ApiServer.Infrastructure.Persistence;
using ApiServer.Infrastructure.Persistence.Repositories;
using ApiServer.Infrastructure.Security;
using ApiServer.Infrastructure.Time;
using ApiServer.Domain.WaitingRoom;
using ApiServer.Shared.Abstractions;
using ApiServer.Shared.Http.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


using ApiServer.Presentation.WebSockets;

namespace ApiServer.Bootstrap;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Application
        services.AddApplication();

        // Infrastructure
        services.AddInfrastructure(config);

        return services;
    }

    private static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // UseCase Handler 등록은 다음 단계에서

        //Auth
        services.AddScoped<LoginGuestHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        //Ticket
        services.AddScoped<IssueTownTicketHandler>();
        services.AddScoped<IssueGameTicketHandler>();
        return services;
    }

    private static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // DbContext / JWT / Google / CP Client 등록 예정
        // DbContext 
        services.AddDbContext<ApiDbContext>((sp, opt) =>
        {
            var db = sp.GetRequiredService<IOptions<DbOptions>>().Value;
            opt.UseNpgsql(db.ConnectionString);
        });

        // Providers 
        services.AddScoped<ITimeProvider, SystemTimeProvider>();
        services.AddScoped<IIdGenerator, GuidIdGenerator>();

        //Repositores / Stores
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        
        // Redis
        services.Configure<RedisOptions>(config.GetSection("Redis"));
        services.AddSingleton<RedisStore>();
        services.AddScoped<WaitingRoomService>();

        // Auth infra
        services.AddSingleton<IJwtIssuerPort, JwtIssuer>();
        services.AddSingleton<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddSingleton<IControlPlanePort, GrpcControlPlaneAdapter>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        services.AddSingleton<AccessTokenValidator>();

        // WebSockets
        services.AddSingleton<ConnectionManager>();
        services.AddScoped<RoomWebSocketHandler>();

        return services;
    }
}

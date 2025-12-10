using Lobby.Api.Config;
using Lobby.Infrastructure.Persistence.InMemory;
using Lobby.Infrastructure.Persistence.PostgreSQL;
using Lobby.Infrastructure.Security;
using Lobby.Domain.Auth.Interface;
using Lobby.Domain.Auth.Services;
using Lobby.Domain.Shared;

namespace Lobby.Api.Extensions;

public static partial class ServiceExtensions
{
    public static IServiceCollection AddLobbyDomainServices(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<ITicketIssuer, TicketIssuer>();
        services.AddSingleton<IGameServerResolver, StaticGsResolver>();

        services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
        return services;
    }
}

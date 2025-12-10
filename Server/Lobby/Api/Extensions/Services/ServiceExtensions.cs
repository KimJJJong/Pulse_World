using Lobby.Api.Config;
using Lobby.Infrastructure;
using System.Data.Entity;

namespace Lobby.Api.Extensions;

public static partial class ServiceExtensions
{
    public static IServiceCollection AddLobbyServices(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<GoogleAuthOptions>(config.GetSection("Auth:GoogleAuth"));    
        services.Configure<AppOptions>(config.GetSection("App"));
        var appOpt = config.GetSection("App").Get<AppOptions>() ?? new AppOptions();


        services.AddLobbyDatabase(config);
        services.AddLobbyDomainServices();
        services.AddLobbyRoomServices();
        services.AddLobbyRateLimiter(appOpt);

        return services;
    }
}

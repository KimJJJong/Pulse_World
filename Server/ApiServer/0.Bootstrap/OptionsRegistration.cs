using ApiServer.Infrastructure.Options;

namespace ApiServer.Bootstrap;

public static class OptionsRegistration
{
    public static IServiceCollection AddApiOptions(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<DbOptions>(config.GetSection("Db"));
        services.Configure<ControlPlaneOptions>(config.GetSection("ControlPlane"));
        services.Configure<SecurityOptions>(config.GetSection("Security"));
        //services.Configure<TownEndpointOptions>(config.GetSection("TownEndpoint"));

        return services;
    }
}

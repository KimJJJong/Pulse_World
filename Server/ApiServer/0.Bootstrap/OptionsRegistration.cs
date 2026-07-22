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
        services.AddOptions<MongoOptions>()
            .Bind(config.GetSection("Mongo"))
            .Validate(
                options => !options.Enabled || (
                    !string.IsNullOrWhiteSpace(options.ConnectionString) &&
                    !string.IsNullOrWhiteSpace(options.DatabaseName) &&
                    !string.IsNullOrWhiteSpace(options.GameResultsCollection)),
                "Mongo connection, database, and collection are required when enabled.")
            .ValidateOnStart();
        services.Configure<ControlPlaneOptions>(config.GetSection("ControlPlane"));
        services.Configure<SecurityOptions>(config.GetSection("Security"));
        services.Configure<SteamOptions>(config.GetSection("Steam"));
        //services.Configure<TownEndpointOptions>(config.GetSection("TownEndpoint"));

        return services;
    }
}

using Npgsql;
using StackExchange.Redis;

namespace Lobby.Api.Extensions;

public static partial class ServiceExtensions
{
    public static IServiceCollection AddLobbyDatabase(this IServiceCollection services, IConfiguration config)
    {
        //  Redis (ConnectionStrings:Redis)
        var redisConn = config.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConn))
            throw new Exception("Redis connection string not found. Expected: ConnectionStrings:Redis");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConn));

        services.AddSingleton(sp =>
            sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

        //  PostgreSQL (ConnectionStrings:Database)
        var cs = config.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(cs))
            throw new Exception("Database connection string not found. Expected: ConnectionStrings:Database");

        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(cs);
            return builder.Build();
        });

        return services;
    }
}

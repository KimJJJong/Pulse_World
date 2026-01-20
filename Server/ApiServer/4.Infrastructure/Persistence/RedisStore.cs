using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ApiServer.Infrastructure.Persistence;

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public int Database { get; set; } = 1; // Default to 1 for api
    public string Prefix { get; set; } = "lobby:";
}

public sealed class RedisStore
{
    private readonly ConnectionMultiplexer _mux;
    private readonly IDatabase _db;
    private readonly RedisOptions _opt;

    public RedisStore(IOptions<RedisOptions> redisOpt)
    {
        _opt = redisOpt.Value;

        _mux = ConnectionMultiplexer.Connect(_opt.ConnectionString);
        _db = _mux.GetDatabase(_opt.Database);
    }

    public IDatabase Db => _db;
    public string Prefix => _opt.Prefix;

    public string KeyWaitingRoom(string roomId) => $"{Prefix}waiting_room:{roomId}";
    public string KeyWaitingRoomIndex() => $"{Prefix}waiting_rooms:index";
}

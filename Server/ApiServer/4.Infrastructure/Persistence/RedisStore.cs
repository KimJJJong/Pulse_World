using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<RedisStore> _log;

    public RedisStore(IOptions<RedisOptions> redisOpt, ILogger<RedisStore> log)
    {
        _opt = redisOpt.Value;
        _log = log;

        try
        {
            _mux = ConnectionMultiplexer.Connect(_opt.ConnectionString);
            _db = _mux.GetDatabase(_opt.Database);

            var endpoints = string.Join(",", _mux.GetEndPoints().Select(e => e.ToString()));
            _log.LogInformation($"[RedisStore] Connected to {endpoints}, Database={_opt.Database}, Prefix={_opt.Prefix}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, $"[RedisStore] Failed to connect to Redis: {_opt.ConnectionString}");
            throw;
        }
    }

    public IDatabase Db => _db;
    public string Prefix => _opt.Prefix;

    public string KeyWaitingRoom(string roomId) => $"{Prefix}waiting_room:{roomId}";
    public string KeyWaitingRoomIndex() => $"{Prefix}waiting_rooms:index";
    public string KeyGameResult(string roomId) => $"{Prefix}game_result:{roomId}";
    public string KeyGameResultIndex() => $"{Prefix}game_results:index";
    public string KeyGameRewardLedger(string uid) => $"{Prefix}game_reward_ledger:{uid}";
}

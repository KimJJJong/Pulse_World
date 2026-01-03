using ControlPlane.Grpc.V1;
using ControlPlane.Infra;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ControlPlane.Domain.Registry;

public sealed class ServerRegistryService
{
    private readonly RedisStore _redis;
    private readonly RegistryOptions _opt;

    public ServerRegistryService(RedisStore redis, IOptions<RegistryOptions> opt)
    {
        _redis = redis;
        _opt = opt.Value;
    }

    // server:{type}:{serverId} => HASH(host,port,cap,region,build,load,sessions,updatedAt)
    public async Task RegisterAsync(RegisterServerRequest r, long nowMs)
    {
        var key = _redis.Key($"{RegistryKeys.ServerPrefix}{(int)r.Type}:{r.ServerId}");
        Console.WriteLine($"[REG] Register key={key}");

        await _redis.Db.HashSetAsync(key, new HashEntry[]
        {
            new("host", r.Endpoint?.Host ?? ""),
            new("port", r.Endpoint?.Port ?? 0),
            new("cap", r.Capacity),
            new("region", r.Region ?? ""),
            new("build", r.BuildVersion ?? ""),
            new("load", 0),
            new("sessions", 0),
            new("updatedAt", nowMs),
        });

        var exists = await _redis.Db.KeyExistsAsync(key);
        Console.WriteLine($"[REG] Register key exists={exists}");


        await _redis.Db.KeyExpireAsync(key, TimeSpan.FromSeconds(_opt.HeartbeatTtlSeconds));
    }

    public async Task<bool> HeartbeatAsync(HeartbeatRequest r, long nowMs)
    {
        var key = _redis.Key($"{RegistryKeys.ServerPrefix}{(int)r.Type}:{r.ServerId}");
        Console.WriteLine($"[REG] Heartbeat key={key}");

        var exists = await _redis.Db.KeyExistsAsync(key);
        Console.WriteLine($"[REG] Heartbeat exists={exists}");

        if (!exists) return false;
        if (!await _redis.Db.KeyExistsAsync(key))
        {
            Console.WriteLine($"[REG] Heartbeat NOT FOUND key={key}");
            return false;
        }


        await _redis.Db.HashSetAsync(key, new HashEntry[]
        {
            new("load", r.Load),
            new("sessions", r.CurrentSessions),
            new("updatedAt", nowMs),
        });

        await _redis.Db.KeyExpireAsync(key, TimeSpan.FromSeconds(_opt.HeartbeatTtlSeconds));
        return true;
    }

    public async Task<ServerEndpoint?> GetEndpointAsync(TicketTarget target, string serverId)
    {
        var type = target switch
        {
            TicketTarget.Town => Grpc.V1.ServerType.Town,
            TicketTarget.Game => Grpc.V1.ServerType.Game,
            _ => Grpc.V1.ServerType.Unspecified
        };
        if (type == Grpc.V1.ServerType.Unspecified)
            return null;

        var key = _redis.Key($"{RegistryKeys.ServerPrefix}{(int)type}:{serverId}");

        var host = await _redis.Db.HashGetAsync(key, "host");
        var port = await _redis.Db.HashGetAsync(key, "port");

        if (!host.HasValue || !port.HasValue)
            return null;

        if (!int.TryParse(port.ToString(), out int p) || p <= 0)
            return null;

        return new ServerEndpoint { Host = host.ToString() ?? "", Port = p };
    }
}

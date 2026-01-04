using ControlPlaneServer.Infra;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ControlPlaneServer.Domain.Registry;

public sealed class ServerRegistryService
{
    private readonly RedisStore _redis;
    private readonly Infra.TimeProvider _time;
    private readonly ControlPlaneOptions _opt;

    public ServerRegistryService(RedisStore redis, Infra.TimeProvider time, IOptions<ControlPlaneOptions> opt)
    {
        _redis = redis;
        _time = time;
        _opt = opt.Value;
    }

    public async Task RegisterAsync(ServerRecord s)
    {
        string type = s.Type; // "TOWN"/"GAME"
        string key = _redis.KeyServer(type, s.ServerId);
        string index = _redis.KeyServersIndex(type);

        long now = _time.NowMs();

        var entries = new HashEntry[]
        {
            new("serverId", s.ServerId),
            new("type", type),
            new("host", s.Host),
            new("port", s.Port),
            new("capacity", s.Capacity),
            new("region", s.Region ?? ""),
            new("buildVersion", s.BuildVersion ?? ""),
            new("load", s.Load),
            new("currentSessions", s.CurrentSessions),
            new("lastHeartbeatMs", now)
        };

        await _redis.Db.HashSetAsync(key, entries);
        await _redis.Db.SetAddAsync(index, s.ServerId);
    }

    public async Task HeartbeatAsync(string type, string serverId, int load, int currentSessions)
    {
        string key = _redis.KeyServer(type, serverId);
        if (!await _redis.Db.KeyExistsAsync(key))
            return;

        long now = _time.NowMs();
        await _redis.Db.HashSetAsync(key, new HashEntry[]
        {
            new("load", load),
            new("currentSessions", currentSessions),
            new("lastHeartbeatMs", now)
        });
    }

    public async Task<ServerRecord?> GetAsync(string type, string serverId)
    {
        string key = _redis.KeyServer(type, serverId);
        if (!await _redis.Db.KeyExistsAsync(key))
            return null;

        var v = await _redis.Db.HashGetAsync(key, new RedisValue[]
        {
            "serverId","type","host","port","capacity","region","buildVersion","load","currentSessions","lastHeartbeatMs"
        });

        return new ServerRecord(
            ServerId: v[0],
            Type: v[1],
            Host: v[2],
            Port: (int)v[3],
            Capacity: (int)v[4],
            Region: v[5],
            BuildVersion: v[6],
            Load: (int)v[7],
            CurrentSessions: (int)v[8],
            LastHeartbeatMs: (long)v[9]
        );
    }

    public async Task<IReadOnlyList<ServerRecord>> ListAsync(string type)
    {
        string index = _redis.KeyServersIndex(type);
        var ids = await _redis.Db.SetMembersAsync(index);

        var list = new List<ServerRecord>(ids.Length);
        foreach (var id in ids)
        {
            var s = await GetAsync(type, id!);
            if (s != null) list.Add(s);
        }
        return list;
    }
}

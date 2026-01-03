using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ControlPlane.Infra;

public sealed class RedisStore : IDisposable
{
    private readonly ConnectionMultiplexer _mux;
    public IDatabase Db { get; }
    public string Prefix { get; }

    public RedisStore(IOptions<RedisOptions> opt)
    {

        _mux = ConnectionMultiplexer.Connect(opt.Value.ConnectionString);
        Db = _mux.GetDatabase();
        Prefix = string.IsNullOrWhiteSpace(opt.Value.KeyPrefix) ? "cp:" : opt.Value.KeyPrefix;
        Console.WriteLine($"[REDIS] prefix={Prefix} db={(Db.Multiplexer.GetEndPoints().FirstOrDefault()?.ToString() ?? "")}");
    }

    public string Key(string raw) => Prefix + raw;
    public void Dispose() => _mux.Dispose();
}

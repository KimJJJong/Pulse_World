using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Server.Server.Game.Services;

public sealed class HeartbeatService : BackgroundService
{
    private readonly IDatabase _db;
    private readonly IConfiguration _cfg;

    public HeartbeatService(IDatabase db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var id = _cfg["App:GameServer:Id"]!;
        var key = $"gs:{id}";
        var host = _cfg["App:GameServer:Host"]!;
        var port = int.Parse(_cfg["App:GameServer:Port"]!);
        var tick = int.Parse(_cfg["App:GameServer:TickRate"]!);
        var cap = int.Parse(_cfg["App:GameServer:Cap"]!);
        var hb = int.Parse(_cfg["App:GameServer:HeartbeatSec"]!);

        while (!ct.IsCancellationRequested)
        {
            var used = await _db.HashGetAsync(key, "used");
            if (used.IsNull) await _db.HashSetAsync(key, new HashEntry[] { new("used", 0) });

            await _db.HashSetAsync(key, new HashEntry[]
            {
                new("host", host), new("port", port.ToString()),
                new("tickRate", tick.ToString()),
                new("cap", cap.ToString()),
                new("ver", "1"),
                new("updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
            });
            await _db.KeyExpireAsync(key, TimeSpan.FromSeconds(hb * 3)); // TTL 여유
            await _db.SetAddAsync("gs:alive", id);

            await Task.Delay(TimeSpan.FromSeconds(hb), ct);
        }
    }
}

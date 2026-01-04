using ControlPlaneServer.Infra;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ControlPlaneServer.Domain.Tickets;

public sealed class TicketService
{
    private readonly RedisStore _redis;
    private readonly Infra.TimeProvider _time;
    private readonly ControlPlaneOptions _opt;

    public TicketService(RedisStore redis, Infra.TimeProvider time, IOptions<ControlPlaneOptions> opt)
    {
        _redis = redis;
        _time = time;
        _opt = opt.Value;
    }

    // ticket:{tid} Hash:
    // uid, target, key, issuedServerId, pinnedServerId, used, expireAtMs
    public async Task<TicketRecord> IssueAsync(string uid, string target, string key, string issuedServerId, string pinnedServerId, int ttlSeconds)
    {
        ttlSeconds = ttlSeconds > 0 ? ttlSeconds : _opt.TicketDefaultTtlSeconds;

        string tid = Guid.NewGuid().ToString("N");
        long now = _time.NowMs();
        long expAt = now + ttlSeconds * 1000L;

        string redisKey = _redis.KeyTicket(tid);

        var entries = new HashEntry[]
        {
            new("uid", uid),
            new("target", target),
            new("key", key ?? ""),
            new("issuedServerId", issuedServerId ?? ""),
            new("pinnedServerId", pinnedServerId ?? ""),
            new("used", "0"),
            new("expireAtMs", expAt.ToString())
        };

        await _redis.Db.HashSetAsync(redisKey, entries);
        await _redis.Db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(ttlSeconds));

        return new TicketRecord(tid, uid, target, key ?? "", issuedServerId ?? "", pinnedServerId ?? "", false, expAt);
    }

    public async Task<(bool ok, string uid, string key, string issuedServerId, string pinnedServerId, long expireAtMs, int errCode)> VerifyAsync(
        string ticketId, string expectedTarget)
    {
        string redisKey = _redis.KeyTicket(ticketId);
        if (!await _redis.Db.KeyExistsAsync(redisKey))
            return (false, "", "", "", "", 0, 10);

        var vals = await _redis.Db.HashGetAsync(redisKey, new RedisValue[]
        {
            "uid","target","key","issuedServerId","pinnedServerId","used","expireAtMs"
        });

        string uid = vals[0];
        string target = vals[1];
        string key = vals[2];
        string issuedServerId = vals[3];
        string pinned = vals[4];
        string used = vals[5];
        long expireAt = long.TryParse(vals[6], out var tmp) ? tmp : 0;

        long now = _time.NowMs();

        if (expireAt <= now) return (false, "", "", issuedServerId, pinned, expireAt, 11);
        if (!string.Equals(target, expectedTarget, StringComparison.Ordinal)) return (false, "", "", issuedServerId, pinned, expireAt, 13);
        if (used == "1") return (false, "", "", issuedServerId, pinned, expireAt, 12);

        return (true, uid, key, issuedServerId, pinned, expireAt, 0);
    }

    public async Task<(bool ok, string uid, string key, string issuedServerId, string pinnedServerId, long expireAtMs, int errCode)>
        ReserveOrConsumeAsync(string ticketId, string expectedTarget, string verifierServerId, string connId, long nowMs)
    {
        var redisKey = _redis.KeyTicket(ticketId);

        var rr = await _redis.EvalReserveOrConsumeTicketAsync(redisKey, expectedTarget, verifierServerId, connId, nowMs);

        // { ok, errCode, uid, key, issuedServerId, pinnedServerId, expireAtMs }
        var arr = (RedisResult[])rr!;
        int ok = (int)arr[0];
        int err = (int)arr[1];
        string uid = (string)arr[2];
        string key = (string)arr[3];
        string issued = (string)arr[4];
        string pinned = (string)arr[5];
        long expAt = (long)arr[6];

        return (ok == 1, uid, key, issued, pinned, expAt, err);
    }
}

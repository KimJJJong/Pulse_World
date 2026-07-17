using ControlPlaneServer.Domain.Registry;
using ControlPlaneServer.Infra;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ControlPlaneServer.Domain.Allocation;

public sealed class AllocatorService
{
    private readonly ServerRegistryService _reg;
    private readonly RedisStore _redis;
    private readonly Infra.TimeProvider _time;
    private readonly ControlPlaneOptions _opt;

    public AllocatorService(ServerRegistryService reg, RedisStore redis, Infra.TimeProvider time, IOptions<ControlPlaneOptions> opt)
    {
        _reg = reg;
        _redis = redis;
        _time = time;
        _opt = opt.Value;
    }

    public async Task<(bool ok, ServerRecord? server, ReservationRecord? reservation)> AllocateGameServerAsync(string uid, string region, int reserveTtlSeconds)
    {
        reserveTtlSeconds = reserveTtlSeconds > 0 ? reserveTtlSeconds : _opt.ReservationTtlSeconds;

        var servers = await _reg.ListAsync("Game");

        if (!string.IsNullOrWhiteSpace(region))
            servers = servers.Where(s => string.Equals(s.Region, region, StringComparison.OrdinalIgnoreCase)).ToList();

        var pick = servers
            .OrderBy(s => s.Load)
            .ThenBy(s => s.CurrentSessions)
            .FirstOrDefault();

        if (pick == null)
            return (false, null, null);

        string rid = Guid.NewGuid().ToString("N");
        long now = _time.NowMs();
        long expAt = now + reserveTtlSeconds * 1000L;

        string rkey = _redis.KeyReservation(rid);

        await _redis.Db.HashSetAsync(rkey, new HashEntry[]
        {
            new("uid", uid),
            new("serverId", pick.ServerId),
            new("expireAtMs", expAt.ToString())
        });
        await _redis.Db.KeyExpireAsync(rkey, TimeSpan.FromSeconds(reserveTtlSeconds));

        return (true, pick, new ReservationRecord(rid, uid, pick.ServerId, expAt));
    }

    public async Task<bool> ValidateReservationAsync(string reservationId, string uid, string serverId, long nowMs)
    {
        string rkey = _redis.KeyReservation(reservationId);
        if (!await _redis.Db.KeyExistsAsync(rkey))
            return false;

        var v = await _redis.Db.HashGetAsync(rkey, new RedisValue[] { "uid", "serverId", "expireAtMs" });
        string rUid = v[0];
        string rServerId = v[1];
        long expAt = long.TryParse(v[2], out var tmp) ? tmp : 0;

        if (expAt <= nowMs) return false;
        if (!string.Equals(rUid, uid, StringComparison.Ordinal)) return false;
        if (!string.Equals(rServerId, serverId, StringComparison.Ordinal)) return false;
        return true;
    }
    public async Task<bool> ConsumeReservationAsync(string reservationId)
    {
        string rkey = _redis.KeyReservation(reservationId);
        // 방 생성 성공 후 예약을 제거(정석: 재사용 방지)
        return await _redis.Db.KeyDeleteAsync(rkey);
    }

}

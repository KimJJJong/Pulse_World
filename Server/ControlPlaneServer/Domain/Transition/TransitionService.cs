using ControlPlaneServer.Infra;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ControlPlaneServer.Domain.Transition;

public sealed class TransitionService
{
    private readonly RedisStore _redis;
    private readonly Infra.TimeProvider _time;
    private readonly ControlPlaneOptions _opt;

    public TransitionService(RedisStore redis, Infra.TimeProvider time, IOptions<ControlPlaneOptions> opt)
    {
        _redis = redis;
        _time = time;
        _opt = opt.Value;
    }

    // transition:{uid} = state, ctx, transitionId, expireAtMs
    public async Task<TransitionRecord> BeginOrReuseAsync(string uid, string toState, string ctx, int ttlSeconds)
    {
        ttlSeconds = ttlSeconds > 0 ? ttlSeconds : _opt.TransitionTtlSeconds;

        string key = _redis.KeyTransition(uid);
        long now = _time.NowMs();

        // 재사용: 살아있으면 그대로
        if (await _redis.Db.KeyExistsAsync(key))
        {
            var v = await _redis.Db.HashGetAsync(key, new RedisValue[] { "transitionId", "expireAtMs", "state", "ctx" });
            string existingId = v[0];
            long expAt = long.TryParse(v[1], out var tmp) ? tmp : 0;
            string state = v[2];
            string oldCtx = v[3];

            if (expAt > now && state == toState && oldCtx == (ctx ?? ""))
                return new TransitionRecord(existingId, uid, state, oldCtx, expAt);
        }

        string transitionId = Guid.NewGuid().ToString("N");
        long expireAt = now + ttlSeconds * 1000L;

        await _redis.Db.HashSetAsync(key, new HashEntry[]
        {
            new("transitionId", transitionId),
            new("state", toState),
            new("ctx", ctx ?? ""),
            new("expireAtMs", expireAt.ToString())
        });
        await _redis.Db.KeyExpireAsync(key, TimeSpan.FromSeconds(ttlSeconds));

        return new TransitionRecord(transitionId, uid, toState, ctx ?? "", expireAt);
    }
}

using ControlPlaneServer.Infra;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ControlPlane.Grpc.V1;

namespace ControlPlaneServer.Domain.Presence;

public sealed class PresenceService
{
    private readonly RedisStore _redis;
    private readonly Infra.TimeProvider _time;
    private readonly ControlPlaneOptions _opt;
    private readonly ControlEventHub _hub;

    public PresenceService(RedisStore redis, Infra.TimeProvider time, IOptions<ControlPlaneOptions> opt, ControlEventHub hub)
    {
        _redis = redis;
        _time = time;
        _opt = opt.Value;
        _hub = hub;
    }

    public async Task<(bool ok, int errCode, long newEpoch, PresenceRecord? prev)> AttachAsync(
        string uid, string newState, string serverId, string connId, int leaseTtlSec, long nowMs)
    {
        leaseTtlSec = leaseTtlSec > 0 ? leaseTtlSec : _opt.LeaseTtlSeconds;

        var key = _redis.KeyPresence(uid);

        var rr = await _redis.EvalAttachPresenceAsync(key, newState, serverId, connId, leaseTtlSec, nowMs);

        // { ok, errCode, newEpoch, prevState, prevServerId, prevConnId, prevEpoch }
        var arr = (RedisResult[])rr!;
        int ok = (int)arr[0];
        int err = (int)arr[1];
        long newEpoch = (long)arr[2];
        string prevState = (string)arr[3];
        string prevServerId = (string)arr[4];
        string prevConnId = (string)arr[5];
        long prevEpoch = (long)arr[6];

        PresenceRecord? prev = null;
        if (!string.IsNullOrEmpty(prevServerId))
        {
            // expAt은 Attach Lua에서 now+ttl로 재설정되므로 “이전 expAt”은 굳이 안 들고가도 됨
            prev = new PresenceRecord(uid, prevState, prevServerId, prevConnId, prevEpoch, ExpireAtMs: 0);
        }

        // 단일 실시간 연결 정책: 이전 서버가 있고, 다른 서버면 Kick 발행
        if (prev != null && prev.ServerId != serverId)
        {
            var reason = (newState == "Game") ? KickReason.MovedToGame : KickReason.DuplicateLogin;

            _hub.PublishToServer(prev.ServerId, new ControlEvent
            {
                Kick = new KickEvent
                {
                    Uid = uid,
                    MinEpoch = newEpoch,
                    Reason = reason,
                    Message = $"Superseded by new presence: {newState}@{serverId}"
                }
            });
        }

        return (ok == 1, err, newEpoch, prev);
    }

    public async Task<(bool ok, int errCode)> RenewLeaseAsync(string uid, string serverId, string connId, long epoch, int leaseTtlSec, long nowMs)
    {
        leaseTtlSec = leaseTtlSec > 0 ? leaseTtlSec : _opt.LeaseTtlSeconds;

        var key = _redis.KeyPresence(uid);
        var rr = await _redis.EvalRenewLeaseAsync(key, serverId, connId, epoch, leaseTtlSec, nowMs);

        // { ok, errCode }
        var arr = (RedisResult[])rr!;
        int ok = (int)arr[0];
        int err = (int)arr[1];

        return (ok == 1, err);
    }
}

using ControlPlane.Grpc.V1;
using Microsoft.Extensions.Options;
using Server.Infrastructure.ControlPlaneClient;
using Server.Infrastructure.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Domain.Auth;

public sealed class PresenceLeaseRenewer
{
    private readonly GrpcControlPlaneClient _cp;
    private readonly ServerIdentityOptions _me;

    public PresenceLeaseRenewer(GrpcControlPlaneClient cp, IOptions<ServerIdentityOptions> me)
    {
        _cp = cp;
        _me = me.Value;
    }

    public async Task RunAsync(
        string uid,
        string connId,
        long epoch,
        Func<bool> isConnected,
        Action<string> onInvalid,
        CancellationToken ct)
    {
        // ttl=10이면 3~5초마다 renew 권장
        var periodMs = Math.Max(1000, (_me.LeaseTtlSeconds * 1000) / 2);

        while (!ct.IsCancellationRequested && isConnected())
        {
            try
            {
                //Console.WriteLine($"[RenewLoop] uid={uid} epoch={epoch} connId={connId} periodMs={periodMs}");

                await Task.Delay((int)periodMs, ct);

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var resp = await _cp.RenewLeaseAsync(new RenewLeaseRequest
                {
                    Uid = uid,
                    ServerId = _me.ServerId,
                    ConnId = connId,
                    Epoch = epoch,
                    LeaseTtlSeconds = _me.LeaseTtlSeconds,
                    NowMs = nowMs
                }, ct);
                //Console.WriteLine($"[RenewResp] ok={resp.Ok} code={resp.Error?.Code} msg={resp.Error?.Message}");

                if (!resp.Ok)
                {
                    // epoch mismatch / not found => 이 연결은 유효하지 않음
                    onInvalid($"renew failed: {resp.Error?.Code} {resp.Error?.Message}");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // 네트워크 순간 장애는 재시도 여지. 다만 너무 길게는 유지하면 안 됨.
                // 여기서는 1회 오류면 다음 루프로 재시도.
                // (원하면 연속 실패 N회시 종료 정책 추가 가능)
                onInvalid($"renew exception: {ex.Message}");
                return;
            }
        }
    }
}

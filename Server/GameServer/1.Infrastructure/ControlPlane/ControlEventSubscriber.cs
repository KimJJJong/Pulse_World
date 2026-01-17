using ControlPlane.Grpc.V1;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using Server.Infrastructure.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Infrastructure.ControlPlaneClient;

/// <summary>
/// CP 이벤트 스트림 구독. Kick을 받아 로컬 연결을 끊는다.
/// </summary>
public sealed class ControlEventSubscriber : BackgroundService
{
    private readonly GrpcControlPlaneClient _cp;
    private readonly ServerOptions _me;
    private readonly ILogger<ControlEventSubscriber> _log;

    // 실제 서버에서는 "uid -> connection" 맵이 필요.
    // 여기선 인터페이스로 분리.
    private readonly IConnectionKicker _kicker;

    public ControlEventSubscriber(
        GrpcControlPlaneClient cp,
        IOptions<ServerOptions> me,
        IConnectionKicker kicker,
        ILogger<ControlEventSubscriber> log)
    {
        _cp = cp;
        _me = me.Value;
        _kicker = kicker;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var type = _me.Role.Name == "Game" ? ServerType.TypeGame : ServerType.TypeTown;

                using var call = _cp.SubscribeControlEvents(new SubscribeControlEventsRequest
                {
                    ServerId = _me.ServerId
                }, stoppingToken);

                await foreach (var ev in call.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    if (ev.PayloadCase == ControlEvent.PayloadOneofCase.Kick)
                    {
                        var k = ev.Kick;
                        _log.LogWarning("KickEvent uid={uid} minEpoch={epoch} reason={reason} msg={msg}",
                            k.Uid, k.MinEpoch, k.Reason, k.Message);

                        _kicker.KickIfEpochAtLeast(k.Uid, k.MinEpoch, $"kick:{k.Reason}:{k.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ControlEvent stream failed. retry in 1s");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}

/// <summary>
/// uid에 매핑된 TCP 연결을 epoch 조건으로 종료.
/// (Town/Game 서버의 ConnectionRegistry가 구현)
/// </summary>
public interface IConnectionKicker
{
    void KickIfEpochAtLeast(string uid, long minEpoch, string reason);
}

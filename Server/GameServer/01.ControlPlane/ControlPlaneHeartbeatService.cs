using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using Server.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;


public sealed class ControlPlaneHeartbeatService : BackgroundService
{
    private readonly ILogger<ControlPlaneHeartbeatService> _log;
    private readonly ControlPlaneRegistrar _reg;
    private readonly IRoleModuleResolver _roleResolver;
    private readonly ServerOptions _opt;

    public ControlPlaneHeartbeatService(
        ILogger<ControlPlaneHeartbeatService> log,
        ControlPlaneRegistrar reg,
        IRoleModuleResolver roleResolver,
        IOptions<ServerOptions> opt)
    {
        _log = log;
        _reg = reg;
        _roleResolver = roleResolver;
        _opt = opt.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // 서버 시작 시 “등록 1회”
        await _reg.RegisterOnceAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var role = _roleResolver.Resolve();
        _log.LogInformation("[CP] Heartbeat started id={Id} role={Role}", _opt.ServerId, role.Name);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var resp = await _reg.Client.HeartbeatAsync(new ControlPlane.Grpc.V1.HeartbeatRequest
                {
                    ServerId = _opt.ServerId,
                    Type = role.ToServerType(),
                    Load = 0,
                    CurrentSessions = SessionManager.Instance.Count     //TODO : SessionManager바뀔 수 도 있지 않음?
                }, cancellationToken: ct);

                if (!resp.Ok)
                    _log.LogWarning("[CP] Heartbeat fail code={Code} msg={Msg}", resp.Error?.Code, resp.Error?.Message);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[CP] Heartbeat exception");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_opt.HeartbeatSec), ct); }
            catch { }
        }
    }
}

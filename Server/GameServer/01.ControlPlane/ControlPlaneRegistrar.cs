using ControlPlane.Grpc.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using Server.Infrastructure.Options;
using Server.Runtime;
using Shared.ControlPlane;
using System;
using System.Threading;
using System.Threading.Tasks;


public sealed class ControlPlaneRegistrar
{
    private readonly ILogger<ControlPlaneRegistrar> _log;
    private readonly IRoleModuleResolver _roleResolver;
    private readonly ServerOptions _opt;
    private readonly ControlPlaneOptions _cpOpt;

    private ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient _cp;

    public ControlPlaneRegistrar(
        ILogger<ControlPlaneRegistrar> log,
        IRoleModuleResolver roleResolver,
        IOptions<ServerOptions> opt,
        IOptions<ControlPlaneOptions> cpOption)
    {
        _log = log;
        _roleResolver = roleResolver;
        _opt = opt.Value;
        _cpOpt = cpOption.Value;
    }

    public ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient Client
        => _cp ?? throw new InvalidOperationException("CP client not initialized.");

    public async Task RegisterOnceAsync(CancellationToken ct)
    {
        //var tmpOpt = new ControlPlaneClientOptions(_cpOpt.Address, _cpOpt.Secret);
        var sharedOpt = new ControlPlaneClientOptions
        {
            Address = _cpOpt.Address,
            Secret = _cpOpt.Secret
        };

        var invoker = GrpcInvokerFactory.CreateControlPlaneInvoker(sharedOpt);
        _cp = new ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient(invoker);


        var role = _roleResolver.Resolve();

        var req = new RegisterServerRequest
        {
            ServerId = _opt.ServerId,
            Type = role.ToServerType(),
            Endpoint = new ServerEndpoint { Host = _opt.Public.Host, Port = _opt.Public.Port },
            Capacity = _opt.Capacity,
            Region = _opt.Region,
            BuildVersion = _opt.BuildVersion
        };

        var resp = await _cp.RegisterServerAsync(req, cancellationToken: ct);

        if (!resp.Ok)
            throw new Exception($"[CP] Register failed (No Error Details in proto)");

        _log.LogInformation("[CP] Registered ok now={Now} type={Type} id={Id}",
            resp.ServerNowMs, req.Type, req.ServerId);
    }
}

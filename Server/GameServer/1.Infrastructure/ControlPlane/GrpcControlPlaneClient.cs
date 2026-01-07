using ControlPlane.Grpc.V1;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Server.Infrastructure.Options;
using Shared.ControlPlane;
using System;
using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Server.Infrastructure.ControlPlaneClient;
/// <summary>
/// CP gRPC 래퍼. 항상 x-cp-secret 포함, timeout/deadline 강제.
/// </summary>
public sealed class GrpcControlPlaneClient
{
    private readonly ControlPlaneOptions _opt;
    private readonly ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient _cp;
    public GrpcControlPlaneClient(IOptions<ControlPlaneOptions> opt/*, GrpcChannelFactory factory*/)
    {
        _opt = opt.Value;

        var sharedOpt = new ControlPlaneClientOptions
        {
            Address = _opt.Address,
            Secret = _opt.Secret
        };


        _cp = new ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient(GrpcInvokerFactory.CreateControlPlaneInvoker(sharedOpt));
    }

    private Metadata SecretHeaders()
        => new Metadata { { "x-cp-secret", _opt.Secret ?? "" } };

    private CallOptions CallOpts(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(500, _opt.TimeoutMs));
        return new CallOptions(headers: SecretHeaders(), deadline: deadline, cancellationToken: ct);
    }

    public Task<ReserveOrConsumeTicketResponse> ReserveOrConsumeTicketAsync(ReserveOrConsumeTicketRequest req, CancellationToken ct)
        => _cp.ReserveOrConsumeTicketAsync(req, CallOpts(ct)).ResponseAsync;

    public Task<AttachConnectionResponse> AttachConnectionAsync(AttachConnectionRequest req, CancellationToken ct)
        => _cp.AttachConnectionAsync(req, CallOpts(ct)).ResponseAsync;

    public Task<RenewLeaseResponse> RenewLeaseAsync(RenewLeaseRequest req, CancellationToken ct)
        => _cp.RenewLeaseAsync(req, CallOpts(ct)).ResponseAsync;

    public AsyncServerStreamingCall<ControlEvent> SubscribeControlEvents(SubscribeControlEventsRequest req, CancellationToken ct)
        => _cp.SubscribeControlEvents(req, CallOpts(ct));
}

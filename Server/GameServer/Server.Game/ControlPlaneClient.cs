using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using ControlPlane.Grpc.V1;

public sealed class ControlPlaneClient
{
    private readonly ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient _cp;
    private readonly string _serverId;

    public ControlPlaneClient(ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient cp, string serverId)
    {
        _serverId = serverId;

        _cp = cp;
    }

    public async Task<ReserveOrConsumeTicketResponse> ReserveOrConsumeAsync(
        string ticketId, TicketTarget expected, string connId, long nowMs, CancellationToken ct)
    {
        return await _cp.ReserveOrConsumeTicketAsync(new ReserveOrConsumeTicketRequest
        {
            TicketId = ticketId,
            ExpectedTarget = expected,
            VerifierServerId = _serverId,
            ConnId = connId,
            NowMs = nowMs
        }, cancellationToken: ct);
    }

    public async Task<AttachConnectionResponse> AttachAsync(
        string uid, PresenceState state, string connId, int leaseTtlSec, long nowMs, CancellationToken ct)
    {
        return await _cp.AttachConnectionAsync(new AttachConnectionRequest
        {
            Uid = uid,
            State = state,
            ServerId = _serverId,
            ConnId = connId,
            LeaseTtlSeconds = leaseTtlSec,
            NowMs = nowMs
        }, cancellationToken: ct);
    }

    public async Task<RenewLeaseResponse> RenewLeaseAsync(
        string uid, string connId, long epoch, int leaseTtlSec, long nowMs, CancellationToken ct)
    {
        return await _cp.RenewLeaseAsync(new RenewLeaseRequest
        {
            Uid = uid,
            ServerId = _serverId,
            ConnId = connId,
            Epoch = epoch,
            LeaseTtlSeconds = leaseTtlSec,
            NowMs = nowMs
        }, cancellationToken: ct);
    }

    // CP -> Server Kick 이벤트 구독(서버 시작 시 1번)
    public AsyncServerStreamingCall<ControlEvent> SubscribeKickEvents(ServerType type, CancellationToken ct)
    {
        return _cp.SubscribeControlEvents(new SubscribeControlEventsRequest
        {
            ServerId = _serverId,
            Type = type
        }, cancellationToken: ct);
    }
}

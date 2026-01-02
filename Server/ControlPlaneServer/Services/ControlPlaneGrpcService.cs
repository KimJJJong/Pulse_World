using ControlPlane.Grpc.V1;
using ControlPlane.Domain.Allocation;
using ControlPlane.Domain.Registry;
using ControlPlane.Domain.Tickets;
using ControlPlane.Infra;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ControlPlane.Services;

public sealed class ControlPlaneGrpcService : Grpc.V1.ControlPlane.ControlPlaneBase
{
    private readonly AllocatorService _alloc;
    private readonly ServerRegistryService _registry;
    private readonly TicketService _tickets;
    private readonly Infra.TimeProvider _time;
    private readonly SecurityOptions _sec;

    public ControlPlaneGrpcService(
        AllocatorService alloc,
        ServerRegistryService registry,
        TicketService tickets,
        Infra.TimeProvider time,
        IOptions<SecurityOptions> sec)
    {
        _alloc = alloc;
        _registry = registry;
        _tickets = tickets;
        _time = time;
        _sec = sec.Value;
    }

    public override async Task<IssueTicketResponse> IssueTicket(IssueTicketRequest request, ServerCallContext context)
    {
        if (!Auth(context))
            return FailIssue(ErrorCode.Unauthorized, "unauthorized");

        if (string.IsNullOrWhiteSpace(request.Uid))
            return FailIssue(ErrorCode.Unspecified, "uid required");

        if (request.Target == TicketTarget.Unspecified)
            return FailIssue(ErrorCode.Unspecified, "target required");

        long now = _time.NowMs();

        string serverId = _alloc.PickServerId(request.Target, request.PreferredServerId);
        var endpoint = await _registry.GetEndpointAsync(request.Target, serverId);
        if (endpoint == null)
            return FailIssue(ErrorCode.ServerNotFound, $"server not found: {serverId}");

        var t = await _tickets.IssueAsync(
            request.Uid,
            request.Target,
            serverId,
            request.Key ?? "",
            request.TtlSeconds > 0 ? request.TtlSeconds : null,
            now);

        return new IssueTicketResponse
        {
            Ok = true,
            TicketId = t.Tid,
            ExpireAtMs = t.ExpireAtMs,
            ServerId = serverId,
            Endpoint = endpoint
        };
    }

    public override async Task<VerifyTicketResponse> VerifyTicket(VerifyTicketRequest request, ServerCallContext context)
    {
        if (!Auth(context))
            return FailVerify(ErrorCode.Unauthorized, "unauthorized");

        if (string.IsNullOrWhiteSpace(request.TicketId))
            return FailVerify(ErrorCode.Unspecified, "ticket_id required");

        if (request.ExpectedTarget == TicketTarget.Unspecified)
            return FailVerify(ErrorCode.Unspecified, "expected_target required");

        long now = _time.NowMs();

        var r = await _tickets.VerifyAndConsumeAsync(request.TicketId, request.ExpectedTarget, now);
        if (!r.Ok)
            return FailVerify(r.Code, r.Reason);

        return new VerifyTicketResponse
        {
            Ok = true,
            Uid = r.Uid,
            IssuedServerId = r.ServerId,
            Key = r.Key
        };
    }

    public override async Task<RegisterServerResponse> RegisterServer(RegisterServerRequest request, ServerCallContext context)
    {
        if (!Auth(context))
            return FailRegister(ErrorCode.Unauthorized, "unauthorized");

        if (string.IsNullOrWhiteSpace(request.ServerId))
            return FailRegister(ErrorCode.Unspecified, "server_id required");

        if (request.Type == ServerType.Unspecified)
            return FailRegister(ErrorCode.Unspecified, "type required");

        if (request.Endpoint == null || string.IsNullOrWhiteSpace(request.Endpoint.Host) || request.Endpoint.Port <= 0)
            return FailRegister(ErrorCode.Unspecified, "endpoint required");

        long now = _time.NowMs();
        await _registry.RegisterAsync(request, now);

        return new RegisterServerResponse { Ok = true, ServerNowMs = now };
    }

    public override async Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        if (!Auth(context))
            return FailHeartbeat(ErrorCode.Unauthorized, "unauthorized");

        if (string.IsNullOrWhiteSpace(request.ServerId))
            return FailHeartbeat(ErrorCode.Unspecified, "server_id required");

        if (request.Type == ServerType.Unspecified)
            return FailHeartbeat(ErrorCode.Unspecified, "type required");

        long now = _time.NowMs();
        bool ok = await _registry.HeartbeatAsync(request, now);
        if (!ok)
            return FailHeartbeat(ErrorCode.ServerNotFound, "server not found");

        return new HeartbeatResponse { Ok = true, ServerNowMs = now };
    }

    private bool Auth(ServerCallContext ctx)
    {
        // 최소: shared secret header
        // caller should send metadata: "x-cp-secret: <secret>"
        var secret = ctx.RequestHeaders.GetValue("x-cp-secret");
        return !string.IsNullOrWhiteSpace(_sec.ServiceSharedSecret) && secret == _sec.ServiceSharedSecret;
    }

    private static IssueTicketResponse FailIssue(ErrorCode code, string msg)
        => new() { Ok = false, Error = new Error { Code = code, Message = msg } };

    private static VerifyTicketResponse FailVerify(ErrorCode code, string msg)
        => new() { Ok = false, Error = new Error { Code = code, Message = msg } };

    private static RegisterServerResponse FailRegister(ErrorCode code, string msg)
        => new() { Ok = false, Error = new Error { Code = code, Message = msg } };

    private static HeartbeatResponse FailHeartbeat(ErrorCode code, string msg)
        => new() { Ok = false, Error = new Error { Code = code, Message = msg } };
}

using ApiServer.Application.Ports;
using ApiServer.Application.Ports.Models;
using ApiServer.Infrastructure.Options;
using ApiServer.Shared.Errors;
using ControlPlane.Grpc.V1;
using Grpc.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using Shared.ControlPlane;

namespace ApiServer.Infrastructure.ControlPlaneClient;

public sealed class GrpcControlPlaneAdapter : IControlPlanePort
{
    private readonly ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient _client;

    // 요청별 기본 deadline (너가 원하면 옵션으로 빼도 됨)
    private readonly TimeSpan _deadline;

    public GrpcControlPlaneAdapter(IOptions<ControlPlaneOptions> opt)
    {
        var o = opt.Value;
        _deadline = TimeSpan.FromMilliseconds(Math.Max(200, o.TimeoutMs));

        // Shared 옵션으로 매핑
        var sharedOpt = new ControlPlaneClientOptions
        {
            Address = o.Address,
            Secret = o.Secret
        };

        CallInvoker invoker = GrpcInvokerFactory.CreateControlPlaneInvoker(sharedOpt);

        _client = new ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient(invoker);
    }

    private static ApiException ToApiException(Error err, string fallback)
    {
        var msg = string.IsNullOrWhiteSpace(err?.Message) ? fallback : err.Message;

        return err?.Code switch
        {
            ErrorCode.Unauthorized => new ApiException(401, "cp_unauthorized", msg),

            ErrorCode.TicketNotFound => new ApiException(400, "cp_ticket_not_found", msg),
            ErrorCode.TicketExpired => new ApiException(400, "cp_ticket_expired", msg),
            ErrorCode.TicketAlreadyUsed => new ApiException(400, "cp_ticket_used", msg),
            ErrorCode.TicketTargetMismatch => new ApiException(400, "cp_ticket_target_mismatch", msg),
            ErrorCode.TicketPinnedServerMismatch => new ApiException(400, "cp_ticket_pinned_mismatch", msg),

            ErrorCode.TransitionNotFound => new ApiException(400, "cp_transition_not_found", msg),
            ErrorCode.TransitionExpired => new ApiException(400, "cp_transition_expired", msg),

            ErrorCode.AllocationFailed => new ApiException(503, "cp_allocation_failed", msg),
            ErrorCode.ReservationInvalid => new ApiException(400, "cp_reservation_invalid", msg),

            _ => new ApiException(502, "cp_error", msg)
        };
    }

private CallOptions MakeCallOptions(CancellationToken ct)
    => new CallOptions(deadline: DateTime.UtcNow.Add(_deadline), cancellationToken: ct);

    public async Task<(string transitionId, long expireAtMs)> BeginOrReuseTransitionAsync(
        string uid, string toState, string ctx, int ttlSeconds, long nowMs, CancellationToken ct)
    {
        // proto 상 MOVING_TO_GAME만 사용
        var req = new BeginOrReuseTransitionRequest
        {
            Uid = uid,
            ToState = TransitionState.MovingToGame,
            Ctx = ctx ?? "",
            TtlSeconds = ttlSeconds
        };

        BeginOrReuseTransitionResponse resp;
        try
        {
            resp = await _client.BeginOrReuseTransitionAsync(req, MakeCallOptions(ct));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new ApiException(504, "cp_timeout", "ControlPlane timeout.");
        }
        catch (RpcException ex)
        {
            throw new ApiException(502, "cp_rpc_failed", ex.Status.Detail);
        }

        if (!resp.Ok)
            throw ToApiException(resp.Error, "BeginOrReuseTransition failed.");

        return (resp.TransitionId, resp.ExpireAtMs);
    }

    public async Task<(string serverId, Application.Ports.Models.Endpoint endpoint, string reservationId, long expireAtMs)> AllocateGameServerAsync(
        string uid, string region, int reserveTtlSeconds, long nowMs, CancellationToken ct)
    {
        var req = new AllocateGameServerRequest
        {
            Uid = uid,
            Region = region ?? "",
            ReserveTtlSeconds = reserveTtlSeconds,
            NowMs = nowMs
        };

        AllocateGameServerResponse resp;
        try
        {
            resp = await _client.AllocateGameServerAsync(req, MakeCallOptions(ct));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new ApiException(504, "cp_timeout", "ControlPlane timeout.");
        }
        catch (RpcException ex)
        {
            throw new ApiException(502, "cp_rpc_failed", ex.Status.Detail);
        }

        if (!resp.Ok)
            throw ToApiException(resp.Error, "AllocateGameServer failed.");

        var ep = new Application.Ports.Models.Endpoint(resp.Endpoint.Host, resp.Endpoint.Port);
        return (resp.ServerId, ep, resp.ReservationId, resp.ExpireAtMs);
    }

    public async Task CreateRoomAsync(
        string uid, string serverId, string reservationId, string roomId, string map, int maxPlayers, long nowMs, CancellationToken ct)
    {
        var req = new CreateRoomRequest
        {
            Uid = uid,
            ServerId = serverId,
            ReservationId = reservationId,
            RoomId = roomId,
            Map = map,
            MaxPlayers = maxPlayers,
            NowMs = nowMs
        };

        CreateRoomResponse resp;
        try
        {
            resp = await _client.CreateRoomAsync(req, MakeCallOptions(ct));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new ApiException(504, "cp_timeout", "ControlPlane timeout.");
        }
        catch (RpcException ex)
        {
            throw new ApiException(502, "cp_rpc_failed", ex.Status.Detail);
        }

        if (!resp.Ok)
            throw ToApiException(resp.Error, "CreateRoom failed.");
    }

    public async Task<(string ticketId, long expireAtMs, string serverId, string key, Application.Ports.Models.Endpoint endpoint)> IssueTicketAsync(
        string uid, string target, string key, string preferredServerId, int ttlSeconds, CancellationToken ct)
    {
        var tgt = target == "GAME" ? TicketTarget.Game : TicketTarget.Town;

        var req = new IssueTicketRequest
        {
            Uid = uid,
            Target = tgt,
            Key = key ?? "",
            PreferredServerId = preferredServerId ?? "",
            TtlSeconds = ttlSeconds
        };

        IssueTicketResponse resp;
        try
        {
            resp = await _client.IssueTicketAsync(req, MakeCallOptions(ct));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new ApiException(504, "cp_timeout", "ControlPlane timeout.");
        }
        catch (RpcException ex)
        {
            throw new ApiException(502, "cp_rpc_failed", $"Server not found: {preferredServerId}");
        }

        if (!resp.Ok)
            throw ToApiException(resp.Error, "IssueTicket failed.");

        var endPoint = new Application.Ports.Models.Endpoint(resp.Endpoint.Host, resp.Endpoint.Port) ;

        return (resp.TicketId, resp.ExpireAtMs, resp.ServerId, resp.Key, endPoint);
    }
}


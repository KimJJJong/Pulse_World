using ControlPlane.Grpc.V1;
using ControlPlaneServer.Domain.Allocation;
using ControlPlaneServer.Domain.Presence;
using ControlPlaneServer.Domain.Registry;
using ControlPlaneServer.Domain.Rooms;
using ControlPlaneServer.Domain.Tickets;
using ControlPlaneServer.Domain.Transition;
using ControlPlaneServer.Infra;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ControlPlaneServer.Services;

public sealed class ControlPlaneGrpcService : ControlPlane.Grpc.V1.ControlPlane.ControlPlaneBase
{
    private readonly ControlPlaneOptions _opt;
    private readonly Infra.TimeProvider _time;

    private readonly TicketService _tickets;
    private readonly ServerRegistryService _registry;
    private readonly AllocatorService _alloc;
    private readonly TransitionService _trans;
    private readonly PresenceService _presence;
    private readonly ControlEventHub _hub;
    private readonly RoomService _room;

    public ControlPlaneGrpcService(
        IOptions<ControlPlaneOptions> opt,
        Infra.TimeProvider time,
        TicketService tickets,
        ServerRegistryService registry,
        AllocatorService alloc,
        TransitionService trans,
        PresenceService presence,
        ControlEventHub hub,
        RoomService room)
    {
        _opt = opt.Value;
        _time = time;
        _tickets = tickets;
        _registry = registry;
        _alloc = alloc;
        _trans = trans;
        _presence = presence;
        _hub = hub;
        _room = room;
    }

    private void RequireSecret(ServerCallContext ctx)
    {
        // 네가 이미 쓰는 방식에 맞춰서 header key 변경 가능
        var md = ctx.RequestHeaders;
        var secret = md.GetValue("x-cp-secret") ?? "";
        if (secret != _opt.Secret)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid control-plane secret"));
    }

    private static Error MakeError(ErrorCode code, string msg) => new Error { Code = code, Message = msg };

    public override async Task<IssueTicketResponse> IssueTicket(IssueTicketRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        // issuedServerId/endpoint는 호출 주체(API)가 결정/전달하는 구조도 가능하지만,
        // 여기서는 "preferred_server_id"를 pinned처럼 사용 가능하도록 남겨둠.
        // 실제 운영에선 API가 AllocateGameServer로 serverId 받은 뒤 IssueTicket에 preferred_server_id를 넣는 방식이 깔끔함.
        var target = request.Target == TicketTarget.Town ? "TOWN" : "GAME";
        var pinned = request.PreferredServerId ?? "";
        var ttl = request.TtlSeconds > 0 ? request.TtlSeconds : _opt.TicketDefaultTtlSeconds;

        // issuedServerId는 일단 pinned가 있으면 pinned로, 없으면 "" (API가 endpoint를 같이 내려주는 구조라면 여기 비워도 됨)
        var issuedServerId = pinned;

        var t = await _tickets.IssueAsync(
            uid: request.Uid,
            target: target,
            key: request.Key ?? "",
            issuedServerId: issuedServerId,
            pinnedServerId: pinned,
            ttlSeconds: ttl
        );

        var server = await _registry.GetAsync(target, pinned);

        if (server == null)
        {
            return new IssueTicketResponse
            {
                Ok = false,
                Error = MakeError(ErrorCode.ServerNotFound, $"Server not found: {issuedServerId}")
            };
        }

        Console.WriteLine($"[IssueTicket] EndPoint: {server.Host} : {server.Port} ReqUID : {request.Uid} || Target : {target} || key : {request.Key} || IssueServerId : {issuedServerId} || Pinned :{pinned} ||ttl :{ttl} " );

        return new IssueTicketResponse
        {
            Ok = true,
            TicketId = t.TicketId,
            ExpireAtMs = t.ExpireAtMs,
            ServerId = issuedServerId,
            Key = request.Key ?? "",
            Endpoint = new ServerEndpoint { Host = server.Host, Port = server.Port }
        };
    }

    public override async Task<VerifyTicketResponse> VerifyTicket(VerifyTicketRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        var expected = request.ExpectedTarget == TicketTarget.Town ? "TOWN" : "GAME";
        var (ok, uid, key, issued, pinned, expAt, err) = await _tickets.VerifyAsync(request.TicketId, expected);

        if (!ok)
        {
            return new VerifyTicketResponse
            {
                Ok = false,
                Error = MakeError((ErrorCode)MapTicketErr(err), $"Verify failed (code={err})")
            };
        }

        return new VerifyTicketResponse
        {
            Ok = true,
            Uid = uid,
            Key = key,
            IssuedServerId = issued
        };
    }

    public override async Task<ReserveOrConsumeTicketResponse> ReserveOrConsumeTicket(ReserveOrConsumeTicketRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        var expected = request.ExpectedTarget == TicketTarget.Town ? "TOWN" : "GAME";
        //Console.WriteLine($"expected : {expected} || tickId :{request.TicketId} || VerifireServerid:{request.VerifierServerId}|| ConnId :{request.ConnId}|| ");
        var (ok, uid, key, issued, pinned, expAt, err) =
            await _tickets.ReserveOrConsumeAsync(request.TicketId, expected, request.VerifierServerId, request.ConnId, request.NowMs);

        if (!ok)
        {
            return new ReserveOrConsumeTicketResponse
            {
                Ok = false,
                Error = MakeError((ErrorCode)MapTicketErr(err), $"Reserve/Consume failed (code={err})"),
                IssuedServerId = issued ?? "",
                PinnedServerId = pinned ?? "",
                ExpireAtMs = expAt
            };
        }

        return new ReserveOrConsumeTicketResponse
        {
            Ok = true,
            Uid = uid,
            Key = key,
            IssuedServerId = issued,
            PinnedServerId = pinned,
            ExpireAtMs = expAt
        };
    }

    public override async Task<AttachConnectionResponse> AttachConnection(AttachConnectionRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        string newState = request.State == PresenceState.Town ? "TOWN" : "GAME";

        var (ok, err, newEpoch, prev) = await _presence.AttachAsync(
            request.Uid,
            newState,
            request.ServerId,
            request.ConnId,
            request.LeaseTtlSeconds,
            request.NowMs
        );

        if (!ok)
        {
            return new AttachConnectionResponse
            {
                Ok = false,
                Error = MakeError((ErrorCode)MapPresenceErr(err), $"Attach failed (code={err})")
            };
        }

        var resp = new AttachConnectionResponse
        {
            Ok = true,
            Epoch = newEpoch
        };

        if (prev != null)
        {
            resp.PrevState = prev.State == "TOWN" ? PresenceState.Town : PresenceState.Game;
            resp.PrevServerId = prev.ServerId;
            resp.PrevConnId = prev.ConnId;
            resp.PrevEpoch = prev.Epoch;
        }

        return resp;
    }

    public override async Task<RenewLeaseResponse> RenewLease(RenewLeaseRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        var (ok, err) = await _presence.RenewLeaseAsync(
            request.Uid,
            request.ServerId,
            request.ConnId,
            request.Epoch,
            request.LeaseTtlSeconds,
            request.NowMs
        );
        //Console.WriteLine($"[RenewLease]Uid : {request.Uid} || sID: {request.ServerId}|| ttl:{request.LeaseTtlSeconds}");
        if (!ok)
        {
            Console.WriteLine($"[No]Uid : {request.Uid} || sID: {request.ServerId}|| Err:{err}");

            return new RenewLeaseResponse
            {
                Ok = false,
                Error = MakeError((ErrorCode)MapPresenceErr(err), $"Renew failed (code={err})")
            };
        }

        return new RenewLeaseResponse
        {
            Ok = true,
            ServerNowMs = _time.NowMs()
        };
    }

    public override async Task<BeginOrReuseTransitionResponse> BeginOrReuseTransition(BeginOrReuseTransitionRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        // 현재는 MOVING_TO_GAME만 사용
        var tr = await _trans.BeginOrReuseAsync(
            uid: request.Uid,
            toState: "MOVING_TO_GAME",
            ctx: request.Ctx ?? "",
            ttlSeconds: request.TtlSeconds
        );

        return new BeginOrReuseTransitionResponse
        {
            Ok = true,
            TransitionId = tr.TransitionId,
            ExpireAtMs = tr.ExpireAtMs
        };
    }

    public override async Task<AllocateGameServerResponse> AllocateGameServer(AllocateGameServerRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        var (ok, server, reservation) = await _alloc.AllocateGameServerAsync(
            request.Uid,
            request.Region ?? "",
            request.ReserveTtlSeconds
        );
        //Console.WriteLine($"[AllocateGameServer]Uid: {request.Uid} || Server: {server} || Reservation: {reservation} ");
        if (!ok || server == null || reservation == null)
        {
            return new AllocateGameServerResponse
            {
                Ok = false,
                Error = MakeError(ErrorCode.AllocationFailed, "No available game server")
            };
        }

        return new AllocateGameServerResponse
        {
            Ok = true,
            ServerId = server.ServerId,
            Endpoint = new ServerEndpoint { Host = server.Host, Port = server.Port },
            ReservationId = reservation.ReservationId,
            ExpireAtMs = reservation.ExpireAtMs
        };
    }

    public override async Task<CreateRoomResponse> CreateRoom(CreateRoomRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        long now = request.NowMs > 0 ? request.NowMs : _time.NowMs();

        // 1) reservation 검증 (uid/serverId/expire)
        bool valid = await _alloc.ValidateReservationAsync(
            reservationId: request.ReservationId,
            uid: request.Uid,
            serverId: request.ServerId,
            nowMs: now
        );

        if (!valid)
        {
            return new CreateRoomResponse
            {
                Ok = false,
                Error = MakeError(ErrorCode.ReservationInvalid, "Invalid or expired reservation")
            };
        }

        // 2) room record 멱등 생성
        var (created, room) = await _room.CreateIfAbsentAsync(
            serverId: request.ServerId,
            roomId: request.RoomId,
            uid: request.Uid,
            map: request.Map,
            maxPlayers: request.MaxPlayers,
            nowMs: now
        );

        // 3) reservation 소모(정석)
        // 이미 room이 존재하는 경우에도 예약은 "소모"해도 OK지만,
        // 재시도 시나리오 고려해서 created일 때만 소모해도 됨.
        if (created)
            await _alloc.ConsumeReservationAsync(request.ReservationId);

        return new CreateRoomResponse { Ok = true };
    }


    public override async Task<RegisterServerResponse> RegisterServer(RegisterServerRequest request, ServerCallContext context)
    {
        RequireSecret(context);

        var type = request.Type == ServerType.Town ? "TOWN" : "GAME";
        var s = new ServerRecord(
            ServerId: request.ServerId,
            Type: type,
            Host: request.Endpoint?.Host ?? "127.0.0.1",
            Port: request.Endpoint?.Port ?? 0,
            Capacity: request.Capacity,
            Region: request.Region ?? "",
            BuildVersion: request.BuildVersion ?? "",
            Load: 0,
            CurrentSessions: 0,
            LastHeartbeatMs: _time.NowMs()
        );

        Console.WriteLine($"[RegisterServer] tpy : {s.Type} || serverID :{s.ServerId} || Host : {s.Host} || Port: {s.Port}");

        await _registry.RegisterAsync(s);

        return new RegisterServerResponse { Ok = true, ServerNowMs = _time.NowMs() };
    }

    public override async Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {

        RequireSecret(context);

        var type = request.Type == ServerType.Town ? "TOWN" : "GAME";

        await _registry.HeartbeatAsync(type, request.ServerId, request.Load, request.CurrentSessions);

        return new HeartbeatResponse { Ok = true, ServerNowMs = _time.NowMs() };
    }

    public override async Task SubscribeControlEvents(SubscribeControlEventsRequest request, IServerStreamWriter<ControlEvent> responseStream, ServerCallContext context)
    {
        RequireSecret(context);

        var reader = _hub.Subscribe(request.ServerId);

        await foreach (var ev in reader.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(ev);
        }
    }

    private static int MapTicketErr(int err)
        => err switch
        {
            10 => (int)ErrorCode.TicketNotFound,
            11 => (int)ErrorCode.TicketExpired,
            12 => (int)ErrorCode.TicketAlreadyUsed,
            13 => (int)ErrorCode.TicketTargetMismatch,
            14 => (int)ErrorCode.TicketPinnedServerMismatch,
            _ => (int)ErrorCode.Unspecified
        };

    private static int MapPresenceErr(int err)
        => err switch
        {
            30 => (int)ErrorCode.PresenceEpochMismatch,
            31 => (int)ErrorCode.PresenceNotFound,
            _ => (int)ErrorCode.Unspecified
        };
}

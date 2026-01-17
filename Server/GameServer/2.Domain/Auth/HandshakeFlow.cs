using ControlPlane.Grpc.V1;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using Server.Infrastructure.ControlPlaneClient;
using Server.Infrastructure.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Domain.Auth;

/// <summary>
/// TCP Handshake 시 CP를 통해 ticket 검증 + presence attach.
/// </summary>
public sealed class HandshakeFlow
{
    private readonly GrpcControlPlaneClient _cp;
    private readonly ServerOptions _me;

    public HandshakeFlow(GrpcControlPlaneClient cp, IOptions<ServerOptions> me)
    {
        _cp = cp;
        _me = me.Value;
    }

    private TicketTarget MyTicketTarget()
        => _me.Role.Name == "Game" ? TicketTarget.Game : TicketTarget.Town;

    private PresenceState MyPresenceState()
        => _me.Role.Name == "Game" ? PresenceState.StateGame : PresenceState.StateTown;

    public async Task<HandshakeResult> RunAsync(
        string ticketId,
        string connId,
        long nowMs,
        CancellationToken ct)
    {
        // 1) ReserveOrConsumeTicket (서버가 어떤 타겟인지로 expected_target 결정)
        Console.WriteLine($"[RunAsync] TicketId: {ticketId} || expectedTarget: {MyTicketTarget()} ||VerifierServerId: {_me.ServerId}|| connectionId: {connId}");
        var v = await _cp.ReserveOrConsumeTicketAsync(new ReserveOrConsumeTicketRequest
        {
            TicketId = ticketId,
            ExpectedTarget = MyTicketTarget(),
            VerifierServerId = _me.ServerId,
            ConnId = connId,
            NowMs = nowMs
        }, ct);

        if (!v.Ok)
            return HandshakeResult.Fail($"ticket failed: {v.Error?.Code} {v.Error?.Message}");

        // 2) AttachConnection (single realtime enforcement)
        var a = await _cp.AttachConnectionAsync(new AttachConnectionRequest
        {
            Uid = v.Uid,
            State = MyPresenceState(),
            ServerId = _me.ServerId,
            ConnId = connId,
            LeaseTtlSeconds = _me.PresenceLeaseTtlSeconds,
            NowMs = nowMs
        }, ct);

        if (!a.Ok)
            return HandshakeResult.Fail($"attach failed: {a.Error?.Code} {a.Error?.Message}");

        return HandshakeResult.Ok(
            uid: v.Uid,
            key: v.Key ?? "",
            epoch: a.Epoch,
            serverRole : (int)MyPresenceState(),
            prev: a
        );
    }
}

public sealed record HandshakeResult(
    bool Success,
    string ErrorMessage,
    string Uid,
    string Key,
    long Epoch,
    int ServerRole,
    AttachConnectionResponse? Prev
)
{
    public static HandshakeResult Fail(string msg)
        => new(false, msg, "", "", 0,0, null);

    public static HandshakeResult Ok(string uid, string key, long epoch,int serverRole, AttachConnectionResponse prev)
        => new(true, "", uid, key, epoch,serverRole, prev);
}

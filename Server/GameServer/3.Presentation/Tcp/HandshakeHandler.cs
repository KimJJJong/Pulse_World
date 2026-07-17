using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Server.Domain.Auth;
using Server.Domain.Connections;
using System;
using System.Threading;
using System.Threading.Tasks;

//namespace Server.Presentation.Tcp.PacketHandlers;

///// <summary>
///// 네 서버의 패킷 시스템에 맞춰 호출만 맞추면 됨.
///// </summary>
//public sealed class HandshakeHandler
//{
//    private readonly HandshakeFlow _flow;
//    private readonly PresenceLeaseRenewer _renewer;
//    private readonly ConnectionRegistry _registry;
//    private readonly ILogger<HandshakeHandler> _log;

//    public HandshakeHandler(HandshakeFlow flow, PresenceLeaseRenewer renewer,ConnectionRegistry  registry, ILogger<HandshakeHandler> log)
//    {
//        _flow = flow;
//        _renewer = renewer;
//        _registry = registry;
//        _log = log;
//    }

//    public async Task HandleAsync(
//        ITcpConnection conn,
//        string ticketId,
//        string clientNonce,
//        CancellationToken ct)
//    {
//        // connId는 서버가 만든 식별자(네 proto 요구)
//        var connId = conn.ConnId;
//        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

//        var res = await _flow.RunAsync(ticketId, connId, nowMs, ct);

//        if (!res.Success)
//        {
//            _log.LogWarning("Handshake failed connId={connId} err={err}", connId, res.ErrorMessage);
//            await conn.SendHandshakeFailAsync(res.ErrorMessage);
//            conn.Close("handshake_fail");
//            return;
//        }

//        // conn에 uid/epoch/key 저장 (네 서버 세션 구조에 맞춰)
//        conn.BindAuth(res.Uid, res.Epoch, res.Key);

//        _registry.Bind(res.Uid, res.Epoch, conn);


//        await conn.SendHandshakeOkAsync(res.Uid, res.Epoch, res.ServerRole,res.Key);

//        // lease renew 시작(연결 살아있는 동안)
//        _ = Task.Run(async () =>
//        {
//            await _renewer.RunAsync(
//                uid: res.Uid,
//                connId: connId,
//                epoch: res.Epoch,
//                isConnected: () => conn.IsConnected,
//                onInvalid: (reason) =>
//                {
//                    _log.LogWarning("Lease invalid uid={uid} connId={connId} reason={reason}",
//                        res.Uid, connId, reason);
//                    conn.Close("lease_invalid:" + reason);

//                    _registry.UnbindIfMatch(res.Uid, connId, res.Epoch);
//                },
//                ct: conn.ConnectionToken
//            );
//        });
//    }
//}

///// <summary>
///// 네 TCP 세션/토큰(UserToken 등)에 맞춰 어댑트하면 됨.
///// </summary>
public interface ITcpConnection
{
    string ConnId { get; }
    bool IsConnected { get; }
    CancellationToken ConnectionToken { get; }

    void BindAuth(string uid, long epoch, string key);

    Task SendHandshakeOkAsync(string uid, long epoch, int serverRole, string key);
    Task SendHandshakeFailAsync(string reason);

    void Close(string reason);
}

//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Util;

//public sealed class HandshakeService
//{
//    private readonly IControlPlaneClient _cp;
//    private readonly ConnectionIndex _index;
//    private readonly string _serverId;
//    private readonly ConnState _state;
//    private readonly TicketTarget _expectedTarget;
//    private readonly int _leaseTtlSec;
//    private readonly int _tickRate;

//    public HandshakeService(
//        IControlPlaneClient cp,
//        ConnectionIndex index,
//        string serverId,
//        ConnState state,
//        TicketTarget expectedTarget,
//        int leaseTtlSec,
//        int tickRate)
//    {
//        _cp = cp;
//        _index = index;
//        _serverId = serverId;
//        _state = state;
//        _expectedTarget = expectedTarget;
//        _leaseTtlSec = leaseTtlSec;
//        _tickRate = tickRate;
//    }

//    public async Task HandleHandshakeAsync(ClientSession s, CS_Handshake p, CancellationToken ct)
//    {
//        if (s.Handshaked)
//        {
//            SendFail(s, 1202, "ALREADY_HANDSHAKED");
//            s.Disconnect();
//            return;
//        }

//        // connId 정석: "serverId:sessionId"
//        s.ConnId = $"{_serverId}:{s.SessionID}";

//        // 1) Ticket 검증/소비
//        var tv = await _cp.ReserveOrConsumeTicketAsync(
//            ticketId: p.ticketId,
//            expectedTarget: _expectedTarget,
//            verifierServerId: _serverId,
//            connId: s.ConnId,
//            clientNonce: p.clientNonce,
//            ct: ct);

//        if (!tv.Ok || string.IsNullOrWhiteSpace(tv.Uid))
//        {
//            SendFail(s, tv.ErrorCode != 0 ? tv.ErrorCode : 1000, tv.ErrorMsg ?? "TICKET_FAIL");
//            s.Disconnect();
//            return;
//        }

//        // 2) Presence attach (epoch++)
//        var at = await _cp.AttachConnectionAsync(tv.Uid!, _state, s.ConnId, _leaseTtlSec, ct);
//        if (!at.Ok)
//        {
//            SendFail(s, at.ErrorCode != 0 ? at.ErrorCode : 1100, at.ErrorMsg ?? "ATTACH_FAIL");
//            s.Disconnect();
//            return;
//        }

//        // 3) 세션 바인딩
//        s.Uid = tv.Uid;
//        s.Ctx = tv.Ctx ?? "";
//        s.Epoch = at.Epoch;
//        s.State = _state;
//        s.Handshaked = true;

//        _index.Bind(s.Uid, s);

//        // 4) 성공 응답
//        var ok = new SC_HandshakeOk
//        {
//            connState = (int)_state,
//            epoch = s.Epoch,
//            uid = s.Uid,
//            ctx = s.Ctx,
//            serverTimeMs = AppRef.ServerTimeMs(),
//            tickRate = _tickRate
//        };
//        s.Send(ok.Write());

//        // 5) lease 루프 시작
//        _ = LeaseLoopAsync(s, ct);
//    }

//    private async Task LeaseLoopAsync(ClientSession s, CancellationToken serverCt)
//    {
//        // 정석: TTL/3 주기
//        var intervalMs = Math.Max(1000, (_leaseTtlSec * 1000) / 3);

//        while (!serverCt.IsCancellationRequested && s.IsConnected)
//        {
//            try
//            {
//                await Task.Delay(intervalMs, serverCt);

//                if (!s.Handshaked || string.IsNullOrEmpty(s.Uid) || string.IsNullOrEmpty(s.ConnId))
//                    continue;

//                bool ok = await _cp.RenewLeaseAsync(
//                    uid: s.Uid!,
//                    state: s.State,
//                    connId: s.ConnId!,
//                    epoch: s.Epoch,
//                    leaseTtlSec: _leaseTtlSec,
//                    ct: serverCt);

//                if (!ok)
//                {
//                    var fd = new SC_ForcedDisconnect { code = 3, reason = "LeaseExpired", epoch = s.Epoch };
//                    s.Send(fd.Write());
//                    s.Disconnect();
//                    return;
//                }
//            }
//            catch (OperationCanceledException) { return; }
//            catch
//            {
//                // 네트워크 일시 오류는 1~2번 정도는 허용 가능.
//                // 정석은 "연속 실패 카운트"를 두는 건데, 여기선 단순화.
//            }
//        }
//    }

//    private static void SendFail(ClientSession s, int code, string msg)
//    {
//        var fail = new SC_HandshakeFail { code = code, msg = msg, serverTimeMs = AppRef.ServerTimeMs() };
//        s.Send(fail.Write());
//    }
//}

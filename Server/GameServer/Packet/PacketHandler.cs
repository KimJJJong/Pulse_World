using Server;
using ServerCore;
using Shared;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Util;
partial class PacketHandler
{
    public static async Task CS_HandshakeAsync(PacketSession session, CS_Handshake req)
    {
        var s = (ClientSession)session; // 네 세션 타입 확정
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 0) 기본 검증
        if (req == null || string.IsNullOrWhiteSpace(req.ticketId))
        {
            await s.SendHandshakeFailAsync("missing_ticket");
            s.Close("missing_ticket");
            return;
        }

        // 1) CP: ReserveOrConsumeTicket + AttachConnection
        var flow = ServerServices.HandshakeFlow;
        var registry = ServerServices.Registry;
        var renewer = ServerServices.LeaseRenewer;

        var res = await flow.RunAsync(
            ticketId: req.ticketId,
            connId: s.ConnId,
            nowMs: nowMs,
            ct: s.ConnectionToken
        );

        if (!res.Success)
        {
            await s.SendHandshakeFailAsync(res.ErrorMessage);
            s.Close("handshake_fail:" + res.ErrorMessage);
            return;
        }

        // 2) 세션에 auth 바인딩
        s.BindAuth(res.Uid, res.Epoch, res.Key);

        // 3) uid -> conn registry 바인딩 (epoch 최신만 유지)
        registry.Bind(res.Uid, res.Epoch, s);

        // 4) OK 응답
        await s.SendHandshakeOkAsync(res.Uid, res.Epoch, res.Key);

        // 5) Lease renew 시작 (연결 동안 유지)
        _ = Task.Run(async () =>
        {
            await renewer.RunAsync(
                uid: res.Uid,
                connId: s.ConnId,
                epoch: res.Epoch,
                isConnected: () => s.IsConnected,
                onInvalid: (reason) =>
                {
                    s.Close("lease_invalid:" + reason);
                    registry.UnbindIfMatch(res.Uid, s.ConnId, res.Epoch);
                },
                ct: s.ConnectionToken
            );
        });
    }

    //public static void CS_LoadedHandler(PacketSession s, IPacket p)
    //{
    //    var session = (ClientSession)s;
    //    var req = (CS_Loaded)p;

    //    if (session.MatchId != req.matchId /*|| session.Uid != req.uid : UID자체를 클라측에서 받아 올 일이 없는게 맞다*/  )
    //    {
    //        Console.WriteLine(
    //    $"[CS_LoadedHandler] Mismatch! session.MatchId={session.MatchId}, req.matchId={req.matchId}, ");
    //        return;
    //    }

    //    var room = GameManager.GetOrCreate(session.MatchId);
    //    session.Loaded = true;

    //    bool allReady =  room.MarkLoadedAsync(session);


    //    if (allReady)
    //    {
    //        var startAtMs = AppRef.ServerTimeMs() + 800; // 0.8초 후 시작

    //        // 참가자 정보 스냅샷
    //        var players = room.GetPlayersSnapshot(); // (uid, side, loaded) 목록

    //        Console.WriteLine($"[GameReady] : {players} ");
    //        room.Broadcast(new SC_AllPlayersLoaded
    //        {
    //            matchId = session.MatchId,
    //            playerss = players
    //                .Select(pl => new SC_AllPlayersLoaded.Players { uid = pl.uid, slot = pl.slot, loaded = pl.loaded })
    //                .ToList()
    //        });

    //        room.Broadcast(new SC_GameBegin
    //        {
    //            matchId = session.MatchId!,
    //            startAtMs = startAtMs,
    //            startTick = 0
    //        });

    //        room.ScheduleStart(startAtMs);
    //    }
    //}
    public static void CS_HandshakeHandler(PacketSession session, IPacket packet)
    {

    }
    public static void CS_PingHandler(PacketSession session, IPacket packet)
    {
        ClientSession _session = (ClientSession)session;
        CS_Ping req= (CS_Ping)packet;

        var now = AppRef.ServerTimeMs();
        _session.LastPingAtMs = now;
        _session.LastPingSeq = req.seq;

        SC_Pong pong = new SC_Pong
        {
            seq = req.seq,
            clientSendMs = req.clientSendMs,
            serverRecvMs = now,
            serverSendMs = AppRef.ServerTimeMs()
        };
        //Console.WriteLine($"[PONG] now={now} recv={pong.serverRecvMs} send={pong.serverSendMs}");



        _session.Send(pong.Write());

    }

    public static void CS_ActionRequestHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_ActionRequest req = (CS_ActionRequest)packet;

        GameManager.TryGet(clientSession.MatchId, out var room);//clientSession.roo
        if(room == null)
        {
            session.Send(new SC_Warn { code = 2000, msg ="ROOM_NOT_FOUND"}.Write());
            return;
        }

        room.OnCS_ActionRequest(clientSession, req);

    }

    public static void CS_CalibHitHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_CalibHit req = (CS_CalibHit)packet;

        GameManager.TryGet(clientSession.MatchId, out var room);//clientSession.roo
        if (room == null)
        {
            session.Send(new SC_Warn { code = 2000, msg = "ROOM_NOT_FOUND" }.Write());
            return;
        }

        room.OnCS_CalibHit(clientSession, req);
    }
}

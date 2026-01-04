using Server;
using ServerCore;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using Util;
using Shared;
partial class PacketHandler
{
    public static int LeaseTtlSec = 30;
    public static async void CS_Handshake(PacketSession session, IPacket packet)
    {
        var s = (ClientSession)session;
        var req = (CS_Handshake)packet;

        if (!string.IsNullOrEmpty(s.Uid))
            return;

        var nowMs = AppRef.ServerTimeMs();
        s.ConnId = Guid.NewGuid().ToString("N");

        try
        {
            var ticketResponse = await Program.CP.ReserveOrConsumeAsync(
                req.ticketId, ControlPlane.Grpc.V1.TicketTarget.Game,
                s.ConnId, nowMs, CancellationToken.None);

            if (!ticketResponse.Ok)
            {
                s.Send((new SC_HandshakeFail { errorCode = (int)ticketResponse.Error.Code, message = ticketResponse.Error.Message }).Write());
                s.Disconnect();
                return;
            }

            var a = await Program.CP.AttachAsync(
                ticketResponse.Uid, ControlPlane.Grpc.V1.PresenceState.Game,
                s.ConnId, LeaseTtlSec, nowMs, CancellationToken.None);

            if (!a.Ok)
            {
                s.Send((new SC_HandshakeFail { errorCode = (int)a.Error.Code, message = a.Error.Message }).Write());
                s.Disconnect();
                return;
            }

            s.Uid = ticketResponse.Uid;
            s.Epoch = a.Epoch;
            s.RealtimeState = ControlPlane.Grpc.V1.ServerType.Game; // GAME
            s.RoomId = ticketResponse.Key ?? "";

            // 여기서 roomId로 룸 join
            // if (!RoomManager.Instance.TryJoin(s.RoomId, s)) { fail... }

            s.Send((new SC_HandshakeOk
            {
                State = 2,
                Epoch = s.Epoch,
                Uid = s.Uid,
                RoomId = s.RoomId,
                ServerNowMs = nowMs,
                LeaseTtlSec = LeaseTtlSec
            }).Write());
        }
        catch (Exception ex)
        {
            s.Send((new SC_HandshakeFail { errorCode = 9999, message = ex.Message }).Write());
            s.Disconnect();
        }
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

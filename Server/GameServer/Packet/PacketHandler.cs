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
    public static async void CS_HandshakeHandler(PacketSession session, IPacket packet)
    {
        CS_Handshake req = (CS_Handshake)packet;
        var s = (ClientSession)session;                         
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 0) 기본 검증
        if (req == null || string.IsNullOrWhiteSpace(req.TicketId))
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
            ticketId: req.TicketId,
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
        // [ping-fix] Console.WriteLine → LogManager. Handshake 는 드물지만 hot-path 일관성 확보.
        LogManager.Instance.LogInfo("Handshake", $"uid={res.Uid} epoch={res.Epoch} key={res.Key}");
        // 3) uid -> conn registry 바인딩 (epoch 최신만 유지)
        registry.Bind(res.Uid, res.Epoch, s);

        // 4) OK 응답
        await s.SendHandshakeOkAsync(res.Uid, res.Epoch,res.ServerRole , res.Key);

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
                    // [ping-fix] Console.WriteLine → LogManager
                    LogManager.Instance.LogWarning("LeaseRenewer",
                        $"onInvalid uid={res.Uid} epoch={res.Epoch} reason={reason}");

                    s.Close("lease_invalid:" + reason);
                    registry.UnbindIfMatch(res.Uid, s.ConnId, res.Epoch);

                    if (!string.IsNullOrEmpty(s.CurrentWorldId))
                    {
                        // 1) Try Town
                        if (TownManager.TryGet(s.CurrentWorldId, out var town))
                        {
                            LogManager.Instance.LogInfo("LeaseRenewer", $"Remove from Town: {town.TownId}");
                            town.RemovePlayer(res.Uid, res.Epoch);
                        }
                        // 2) Try Game
                        else if (GameManager.TryGet(s.CurrentWorldId, out var game))
                        {
                            LogManager.Instance.LogInfo("LeaseRenewer", $"Remove from Game: {game.MatchId}");
                            game.RemovePlayer(res.Uid, res.Epoch);
                        }
                        else
                        {
                             LogManager.Instance.LogWarning("LeaseRenewer",
                                $"World not found for: {s.CurrentWorldId}");
                        }
                    }
                },
                ct: s.ConnectionToken
            );
        });
    }

   

    public static void CS_MapEnterHandler(PacketSession session, IPacket packet)
    {
        var s = (ClientSession)session;
        var req = (CS_MapEnter)packet;
        // [ping-fix] Console.WriteLine → LogManager (드물지만 정리)
        LogManager.Instance.LogInfo("MapEnter", $"mapId={req.MapId} key={s.Key}");

        if (!s.HasAuth)
        {
            LogManager.Instance.LogWarning("MapEnter", $"Fail NoAuth uid={s.Uid} key={s.Key}");
            s.Close("map_enter_without_auth");
            return;
        }

        // 1) Game Mode (Key exists -> MatchId)
        if (!string.IsNullOrEmpty(s.Key))
        {
            int max = req.MaxPlayers > 0 ? req.MaxPlayers : 2;
            var room = GameManager.GetOrCreate(s.Key, req.MapId, max);

            LogManager.Instance.LogInfo("GameRoom",
                $"Entering matchId={s.Key} count={room.GetPlayersSnapshot().Count()}");

            if (!room.BindOrReattach(s, out var actorId))
            {
                LogManager.Instance.LogWarning("MapEnter", $"Game BindFail uid={s.Uid} key={s.Key}");
                s.Close("game_bind_fail");
                return;
            }

            // Game은 "로딩 완료" 상태가 되어야 시작하므로 MarkLoaded
            if (room.MarkLoadedAsync(s))
            {
                LogManager.Instance.LogInfo("GameRoom", "All Loaded! Scheduling Start...");
                var startAtMs = AppRef.ServerTimeMs() + 1000; // 1초 뒤 시작
                room.BroadcastGameStart(startAtMs);
            }
        }
        // 2) Town Mode (Key empty)
        else
        {
            var townId = "Town_01"; // Default Town
            LogManager.Instance.LogInfo("TownRoom", $"Enter townId={townId}");
            var room = TownManager.GetOrCreate(townId);

            if (!room.BindOrReattach(s, out var actorId))
            {
                LogManager.Instance.LogWarning("MapEnter", $"Town BindFail uid={s.Uid}");
                s.Close("town_bind_fail");
                return;
            }

            // Town은 즉시 InitMap 전송 (BindOrReattach 내부에서 처리됨)
        }
    }
    public static void CS_ReadyHandler(PacketSession session, IPacket packet)
    {
        ClientSession _session = (ClientSession)session;
        CS_MapEnter req = (CS_MapEnter)packet;

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


    public static void CS_TownActionRequestHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_TownActionRequest req = (CS_TownActionRequest)packet;

        TownManager.TryGet(clientSession.CurrentWorldId, out var action);
        if (action == null)
        {
            session.Send(new SC_Warn { code = 2000, msg = $"ROOM_NOT_FOUND : CurrentWorkdId: {clientSession.CurrentWorldId}" }.Write());
            return;
        }
        action.OnCS_TownActionRequest(clientSession, req);
    }
    public static void CS_ActionRequestHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_ActionRequest req = (CS_ActionRequest)packet;

        //TownManager.TryGet(clientSession.CurrentWorldId, out var action);
        //if (action == null)
        //{
        //    session.Send(new SC_Warn { code = 2000, msg = "ROOM_NOT_FOUND" }.Write());
        //    return;
        //}
        //action.OnCS_ActionRequest(clientSession, req);

        GameManager.TryGet(clientSession.CurrentWorldId, out var room);//clientSession.roo
        if (room == null)
        {
            session.Send(new SC_Warn { code = 2000, msg = $"ROOM_NOT_FOUND : CurrentWorkdId: {clientSession.CurrentWorldId}" }.Write());
            return;
        }

        room.OnCS_ActionRequest(clientSession, req);

    }

    public static void CS_CalibHitHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_CalibHit req = (CS_CalibHit)packet;

        GameManager.TryGet(clientSession.CurrentWorldId, out var room);//clientSession.roo
        if (room == null)
        {
            session.Send(new SC_Warn { code = 2000, msg = $"ROOM_NOT_FOUND : CurrentWorkdId: {clientSession.CurrentWorldId}" }.Write());
            return;
        }

        room.OnCS_CalibHit(clientSession, req);
    }
}

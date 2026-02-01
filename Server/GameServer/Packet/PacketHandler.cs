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
        Console.WriteLine($"[HandShake] UID : {res.Uid} || Epoch : {res.Epoch} || Key : {res.Key}");
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
                    Console.WriteLine($"[onInvalid] uid={res.Uid} epoch={res.Epoch} reason={reason}");

                    s.Close("lease_invalid:" + reason);
                    registry.UnbindIfMatch(res.Uid, s.ConnId, res.Epoch);

                    if (!string.IsNullOrEmpty(s.CurrentWorldId))
                    {
                        // 1) Try Town
                        if (TownManager.TryGet(s.CurrentWorldId, out var town))
                        {
                            Console.WriteLine($"[LeaseInvalid] Removing from Town: {town.TownId}");
                            town.RemovePlayer(res.Uid, res.Epoch);
                        }
                        // 2) Try Game
                        else if (GameManager.TryGet(s.CurrentWorldId, out var game))
                        {
                            Console.WriteLine($"[LeaseInvalid] Removing from Game: {game.MatchId}");
                            game.RemovePlayer(res.Uid, res.Epoch);
                        }
                        else
                        {
                             Console.WriteLine($"[LeaseInvalid] World not found for: {s.CurrentWorldId}");
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
        Console.WriteLine($"[IN]CS_MapEnterHandler : MapId: {req.MapId} Key: {s.Key}");

        if (!s.HasAuth)
        {
            Console.WriteLine($"[CS_MapEnterHandler] Fail: NoAuth. Uid={s.Uid} Key={s.Key}");
            s.Close("map_enter_without_auth");
            return;
        }

        // 1) Game Mode (Key exists -> MatchId)
        if (!string.IsNullOrEmpty(s.Key))
        {
            // GameRoom 생성 (Lazy)
            // 주의: CP와 동기화된 Map 정보가 없으므로, 최초 생성 시 req.MapId를 신뢰하거나 기본값 사용
            // GameRoom 내부에서 MapId를 설정할 수 있는 메소드가 필요할 수 있음 (현재는 Default 0)
            int max = req.MaxPlayers > 0 ? req.MaxPlayers : 2;
            var room = GameManager.GetOrCreate(s.Key, req.MapId, max);
            
            Console.WriteLine($"[GameRoom] Entering MatchId: {s.Key} (Count: {room.GetPlayersSnapshot().Count()})");

            // (Optional) Room이 막 생성되었다면 MapId 등을 초기화
            // if (room.MapId == 0) room.SetMapId(req.MapId); 

            if (!room.BindOrReattach(s, out var actorId))
            {
                Console.WriteLine($"[CS_MapEnterHandler] BindFail: {s.Uid} -> {s.Key}");
                s.Close("game_bind_fail");
                return;
            }

            // Game은 "로딩 완료" 상태가 되어야 시작하므로 MarkLoaded
            if (room.MarkLoadedAsync(s))
            {
                Console.WriteLine($"[GameRoom] All Loaded! Scheduling Start...");
                // 모두 준비 완료되면 게임 시작 등을 처리 (GameRoom 내부)
                var startAtMs = AppRef.ServerTimeMs() + 1000; // 1초 뒤 시작
                room.BroadcastGameStart(startAtMs);
            }
        }
        // 2) Town Mode (Key empty)
        else
        {
            var townId = "Town_01"; // Default Town
            Console.WriteLine($"[TownRoom] Enter: {townId}");
            var room = TownManager.GetOrCreate(townId);

            if (!room.BindOrReattach(s, out var actorId))
            {
                Console.WriteLine($"[CS_MapEnterHandler] Town BindFail: {s.Uid}");
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

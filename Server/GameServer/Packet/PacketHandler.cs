using Server;
using ServerCore;
using StackExchange.Redis;
using System;
using System.Linq;


using Util;
using Shared;
using System.Diagnostics;
class PacketHandler
{
    public static async void CS_JoinGameHandler(PacketSession session, IPacket packet)
    {
        var s = (ClientSession)session;
        var req = (CS_JoinGame)packet;


        try
        {
            // 0) 방어: 널/비어있는 필드 빠르게 필터
            if (string.IsNullOrEmpty(req.ticket))
            {
                session.Send(new SC_Error { code = 4000, message = "TicketMissing" }.Write());
                session.Disconnect();
                return;
            }

            // 1) JWT 검증 (RS256) - 기존 TicketValidator 사용
            //    TicketValidator 가 RS256 공개키를 이용해 signature/lifetime 검증
            var (ok, claims, code) = AppRef.Jwt.ValidateTicket(req.ticket);
            if (!ok || claims == null)
            {
                session.Send(new SC_Error { code = 4000, message = $"JwtInvalid:{code}" }.Write());
                session.Disconnect();
                return;
            }

            // 1-1) 클레임 추출 
            string matchId = claims.TryGetValue("matchId", out var _m) ? _m?.ToString() ?? "" : "";
            string uid = claims.TryGetValue("uid", out var _u) ? _u?.ToString() ?? "" : "";
            string nonce = claims.TryGetValue("nonce", out var _n) ? _n?.ToString() ?? "" : "";
            string jti = claims.TryGetValue("jti", out var _j) ? _j?.ToString() ?? "" : "";

            int slot = -1;
            if (claims.TryGetValue("slot", out var _slot))
            {
                int.TryParse(_slot?.ToString(), out slot);
            }
            Console.WriteLine($"GetFuking Slot :{slot}");

            int tokenTickRate = int.TryParse(claims.TryGetValue("tickRate", out var _tr) ? _tr?.ToString() : null, out var t) ? t : -1;
            int tokenProtoVer = int.TryParse(claims.TryGetValue("protoVer", out var _pv) ? _pv?.ToString() : null, out var pv) ? pv : -1;

            // 1-2) protoVer 강제 검사
            int clientProto = (req.protoVer > 0) ? req.protoVer : tokenProtoVer; // 우선 req.protoVer 사용
            if (clientProto != AppRef.ProtoVer)
            {
                session.Send(new SC_Error { code = 4001, message = $"ProtoMismatch: expect={AppRef.ProtoVer} got={clientProto}" }.Write());
                session.Disconnect();
                return;
            }

            // 1-3) 요청 본문과 토큰의 일치성 검사 (MITM/오용 방지)
            /*            if (req.matchId != matchId || req.uid != uid)
                        {
                            session.Send(new SC_Error { code = 4000, message = "ClaimMismatch" }.Write());
                            session.Disconnect();
                            return;
                        }*/
            if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(nonce) || slot < 0)
            {
                session.Send(new SC_Error { code = 4000, message = "ClaimMissing" }.Write());
                session.Disconnect();
                return;
            }

            // 2) Redis 원자 검증 + 좌석 점유 (EVAL)
            string tag = "{" + matchId + "}"; //  공통 해시태그

            var keyMatch = (StackExchange.Redis.RedisKey)$"match:{tag}";
            var keyConn = (StackExchange.Redis.RedisKey)$"match:{tag}:conn";
            var keyNonce = (StackExchange.Redis.RedisKey)$"ticket:{tag}:nonce:{nonce}";
            var keyBlack = string.IsNullOrEmpty(jti)
                ? (StackExchange.Redis.RedisKey)$"ticket:{tag}:black:_none"
                : (StackExchange.Redis.RedisKey)$"ticket:{tag}:black:{jti}";

            RedisKey[] keys = new StackExchange.Redis.RedisKey[] { keyMatch, keyConn, keyNonce, keyBlack };

            RedisValue[] args = new StackExchange.Redis.RedisValue[]
            {
                RedisAuth.ExpectedGsKey, // 로비가 저장해둔 match.gsId와 비교
                slot,               
                uid,
                "120",                   // conn TTL
                "900",                   // nonce TTL
                "1"                      // allowRejoin
            };

            RedisResult eval = await RedisAuth.DB.ScriptEvaluateAsync(RedisAuth.VerifyScriptText, keys, args);
            string rs = eval.ToString() ?? "";

            if (!(rs.Contains("ok=claimed") || rs.Contains("ok=rejoin")))
            {
                if (rs.Contains("wrong_gs"))
                {
                    var storedGsId = await RedisAuth.DB.HashGetAsync($"match:{{{matchId}}}", "gsId");
                    Console.WriteLine($"wrong_gs: match={matchId}, stored={storedGsId}, expected={RedisAuth.ExpectedGsKey}");
                }

                session.Send(new SC_Error { code = 4002, message = $"GuardFail:{rs}" }.Write());
                session.Disconnect();
                return;
            }

            // 3) 룸 바인딩 (세션 필드 선세팅  입장)
            GameRoom room = RoomManager.GetOrCreate(matchId);

            s.MatchId = matchId;
            s.Uid = uid;
            s.Slot = slot;
            s.Loaded = false;
            Console.WriteLine( $"Matchid : {matchId} || Slot : {slot}");
            if (!room.Bind(slot, s))
            {
                session.Send(new SC_Error { code = 4003, message = "RoomBindFailed" }.Write());
                session.Disconnect();
                return;
            }

            //// 4) 수락 응답  
            //var sc = new SC_MakeMapData
            //{
            //    map = room.MapId,
            //    playerSlotNumber = (side == 'A') ? 0 : 1
            //};
            //s.Send(sc.Write());

            // 4-1)  동기화 정보 송신: 서버가 최종 tickRate/정책 공지
            //     - 기존 프로젝트에 맞춰 SC_Welcome 새로 추가하거나,
            //       MakeMapData 확장 대신 별도 패킷으로 분리하는 것을 추천.
            var welcome = new SC_Welcome
            {
                matchId = matchId,
                slot = slot,
                serverTimeMs = AppRef.ServerTimeMs(),
                tickRate = AppRef.TickRate,
                startTick = 0,
                map = room.MapId.ToString(),
                seed = room.Seed,
                latencyBudgetMs = 600,
                startPolicy = "AllLoaded"
            };
            s.Send(welcome.Write());

            // 5) 상태 갱신: Redis conn -> joined
            await RedisAuth.DB.HashSetAsync($"match:{matchId}:conn", uid, "joined");
            await RedisAuth.DB.KeyExpireAsync($"match:{matchId}:conn", TimeSpan.FromMinutes(15));

            // 6) 이후: 클라가 CS_Loaded를 보내면 룸에서 Loaded 수집 -> 전원 완료 시 SC_AllPlayersLoaded & SC_GameBegin BroadCast
        }
        catch (Exception ex)
        {
            session.Send(new SC_Error { code = 5000, message = "JoinException" }.Write());
            session.Disconnect();
            Console.WriteLine($"[ERR]  {ex}");
            //LogManager.Instance.LogDebug("Join", $"{s.MatchId} ?? -, {s.Uid} ?? -, {ex.Message}");
            //LogMainger.JoinGuardFail(Program.Logger, s.MatchId ?? -, s.Uid ?? -, ex.Message);
        }


    }


    public static void CS_LoadedHandler(PacketSession s, IPacket p)
    {
        var session = (ClientSession)s;
        var req = (CS_Loaded)p;

        if (session.MatchId != req.matchId /*|| session.Uid != req.uid : UID자체를 클라측에서 받아 올 일이 없는게 맞다*/  )
        {
            Console.WriteLine(
        $"[CS_LoadedHandler] Mismatch! session.MatchId={session.MatchId}, req.matchId={req.matchId}, ");
            return;
        }

        var room = RoomManager.GetOrCreate(session.MatchId);
        session.Loaded = true;

        bool allReady =  room.MarkLoadedAsync(session);


        if (allReady)
        {
            var startAtMs = AppRef.ServerTimeMs() + 800; // 0.8초 후 시작

            // 참가자 정보 스냅샷
            var players = room.GetPlayersSnapshot(); // (uid, side, loaded) 목록

            Console.WriteLine($"[GameReady] : {players} ");
            room.Broadcast(new SC_AllPlayersLoaded
            {
                matchId = session.MatchId,
                playerss = players
                    .Select(pl => new SC_AllPlayersLoaded.Players { uid = pl.uid, slot = pl.slot, loaded = pl.loaded })
                    .ToList()
            });

            room.Broadcast(new SC_GameBegin
            {
                matchId = session.MatchId!,
                startAtMs = startAtMs,
                startTick = 0
            });

            room.ScheduleStart(startAtMs);
        }
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
        _session.Send(pong.Write());

    }

    public static void CS_ActionRequestHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_ActionRequest req = (CS_ActionRequest)packet;

        RoomManager.TryGet(clientSession.MatchId, out var room);//clientSession.roo
        if(room == null)
        {
            session.Send(new SC_Warn { code = 2000, msg ="ROOM_NOT_FOUND"}.Write());
            return;
        }

        room.OnCS_ActionRequest(clientSession, req);

    }
}

using Server;
using ServerCore;
using Shared;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Util;
using GameServer.Infrastructure.Api.Dto;
partial class PacketHandler
{
    private const string RelayKeyPrefix = "p2p:";
    private const string TownRelayKeyPrefix = "townp2p:";

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
                    LogManager.Instance.LogWarning(
                        "SessionLifecycle",
                        $"event=lease_invalid reason={reason} uid={res.Uid} epoch={res.Epoch} conn={s.ConnId} key={s.Key} world={EmptyToDash(s.CurrentWorldId)}");

                    s.Close("lease_invalid:" + reason);
                    registry.UnbindIfMatch(res.Uid, s.ConnId, res.Epoch);

                    if (!string.IsNullOrEmpty(s.CurrentWorldId))
                    {
                        // 1) Try Town
                        if (TownManager.TryGet(s.CurrentWorldId, out var town))
                        {
                            LogManager.Instance.LogInfo("LeaseRenewer", $"Remove from Town: {town.TownId}");
                            LogManager.Instance.LogInfo(
                                "SessionLifecycle",
                                $"event=lease_invalid_route action=remove roomType=Town world={town.TownId} uid={res.Uid} epoch={res.Epoch} conn={s.ConnId}");
                            town.RemovePlayer(res.Uid, res.Epoch);
                        }
                        // 2) Try Game
                        else if (GameManager.TryGet(s.CurrentWorldId, out var game))
                        {
                            LogManager.Instance.LogInfo("LeaseRenewer", $"Remove from Game: {game.MatchId}");
                            LogManager.Instance.LogInfo(
                                "SessionLifecycle",
                                $"event=lease_invalid_route action=remove roomType=Game world={game.MatchId} uid={res.Uid} epoch={res.Epoch} conn={s.ConnId}");
                            game.RemovePlayer(res.Uid, res.Epoch);
                        }
                        else if (TownP2PRelayManager.TryGet(s.CurrentWorldId, out var townRelay))
                        {
                            LogManager.Instance.LogInfo("LeaseRenewer", $"Remove from Town P2P: {s.CurrentWorldId}");
                            LogManager.Instance.LogInfo(
                                "SessionLifecycle",
                                $"event=lease_invalid_route action=remove roomType=TownP2P world={s.CurrentWorldId} uid={res.Uid} epoch={res.Epoch} conn={s.ConnId}");
                            townRelay.RemovePlayer(res.Uid, res.Epoch);
                        }
                        else if (P2PRelayManager.TryGet(s.CurrentWorldId, out var relay))
                        {
                            LogManager.Instance.LogInfo("LeaseRenewer", $"Remove from Game P2P: {s.CurrentWorldId}");
                            LogManager.Instance.LogInfo(
                                "SessionLifecycle",
                                $"event=lease_invalid_route action=remove roomType=GameP2P world={s.CurrentWorldId} uid={res.Uid} epoch={res.Epoch} conn={s.ConnId}");
                            relay.RemovePlayer(res.Uid, res.Epoch);
                        }
                        else
                        {
                             LogManager.Instance.LogWarning("LeaseRenewer",
                                $"World not found for: {s.CurrentWorldId}");
                             LogManager.Instance.LogWarning(
                                "SessionLifecycle",
                                $"event=lease_invalid_route action=missing_world world={s.CurrentWorldId} uid={res.Uid} epoch={res.Epoch} conn={s.ConnId}");
                        }
                    }
                },
                ct: s.ConnectionToken
            );
        });
    }

   

    public static async void CS_MapEnterHandler(PacketSession session, IPacket packet)
    {
        var s = (ClientSession)session;
        var req = (CS_MapEnter)packet;
        // [ping-fix] Console.WriteLine → LogManager (드물지만 정리)
        LogManager.Instance.LogInfo("MapEnter", $"mapId={req.MapId} key={s.Key}");
        var previousWorld = s.CurrentWorldId;
        LogManager.Instance.LogInfo(
            "SessionLifecycle",
            $"event=map_enter_request uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={EmptyToDash(s.Key)} from={EmptyToDash(previousWorld)} map={EmptyToDash(req.MapId)} max={req.MaxPlayers}");

        if (!s.HasAuth)
        {
            LogManager.Instance.LogWarning("MapEnter", $"Fail NoAuth uid={s.Uid} key={s.Key}");
            LogManager.Instance.LogWarning(
                "SessionLifecycle",
                $"event=map_enter_fail reason=no_auth uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={EmptyToDash(s.Key)} from={EmptyToDash(previousWorld)} map={EmptyToDash(req.MapId)}");
            s.Close("map_enter_without_auth");
            return;
        }

        // 1) Town P2P Mode
        if (!string.IsNullOrEmpty(s.Key) && s.Key.StartsWith(TownRelayKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            int max = req.MaxPlayers > 0 ? req.MaxPlayers : 16;
            var roomId = s.Key.Substring(TownRelayKeyPrefix.Length);
            var manifest = await TryLoadTownRelayManifestAsync(roomId);
            var mapId = !string.IsNullOrWhiteSpace(manifest?.MapId) ? manifest.MapId : req.MapId;
            var room = TownP2PRelayManager.GetOrCreate(s.Key, mapId, max);
            if (manifest != null)
            {
                room.UpdateHostPreferences(
                    manifest.HostUid ?? "",
                    manifest.Participants);
            }

            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=map_enter_route route=TownP2P uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} from={EmptyToDash(previousWorld)} world={s.Key} room={roomId} map={EmptyToDash(mapId)} max={max} manifest={manifest != null}");
            LogManager.Instance.LogInfo("TownP2PRelayRoom", $"Entering relayId={s.Key}");
            if (!room.BindOrReattach(s, out var actorId))
            {
                LogManager.Instance.LogWarning("MapEnter", $"TownRelay BindFail uid={s.Uid} key={s.Key}");
                LogManager.Instance.LogWarning(
                    "SessionLifecycle",
                    $"event=map_enter_fail reason=bind_fail route=TownP2P uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} world={s.Key} room={roomId} map={EmptyToDash(mapId)}");
                s.Close("town_relay_bind_fail");
                return;
            }

            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=map_enter_ok route=TownP2P uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} world={s.CurrentWorldId} actor={actorId} seat={s.SeatIndex} map={EmptyToDash(mapId)}");
        }
        // 2) Game P2P Mode (Key exists -> MatchId)
        else if (!string.IsNullOrEmpty(s.Key) && s.Key.StartsWith(RelayKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            int max = req.MaxPlayers > 0 ? req.MaxPlayers : 2;
            var roomId = s.Key.Substring(RelayKeyPrefix.Length);
            var manifest = await TryLoadRelayManifestAsync(roomId);
            var room = P2PRelayManager.GetOrCreate(s.Key, req.MapId, max);
            if (manifest != null)
            {
                room.UpdateHostPreferences(
                    manifest.HostUid ?? "",
                    manifest.Participants);
            }

            LogManager.Instance.LogInfo("P2PRelayRoom",
                $"Entering relayId={s.Key} count={room.GetPlayersSnapshot().Count()}");
            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=map_enter_route route=GameP2P uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} from={EmptyToDash(previousWorld)} world={s.Key} room={roomId} map={EmptyToDash(req.MapId)} max={max} manifest={manifest != null}");

            if (!room.BindOrReattach(s, out var actorId))
            {
                LogManager.Instance.LogWarning("MapEnter", $"Relay BindFail uid={s.Uid} key={s.Key}");
                LogManager.Instance.LogWarning(
                    "SessionLifecycle",
                    $"event=map_enter_fail reason=bind_fail route=GameP2P uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} world={s.Key} room={roomId} map={EmptyToDash(req.MapId)}");
                s.Close("relay_bind_fail");
                return;
            }

            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=map_enter_ok route=GameP2P uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} world={s.CurrentWorldId} actor={actorId} seat={s.SeatIndex} map={EmptyToDash(req.MapId)}");
        }
        else if (!string.IsNullOrEmpty(s.Key))
        {
            int max = req.MaxPlayers > 0 ? req.MaxPlayers : 2;
            var room = GameManager.GetOrCreate(s.Key, req.MapId, max);

            LogManager.Instance.LogInfo("GameRoom",
                $"Entering matchId={s.Key} count={room.GetPlayersSnapshot().Count()}");
            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=map_enter_route route=Game uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} from={EmptyToDash(previousWorld)} world={s.Key} map={EmptyToDash(req.MapId)} max={max}");

            if (!room.BindOrReattach(s, out var actorId))
            {
                LogManager.Instance.LogWarning("MapEnter", $"Game BindFail uid={s.Uid} key={s.Key}");
                LogManager.Instance.LogWarning(
                    "SessionLifecycle",
                    $"event=map_enter_fail reason=bind_fail route=Game uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} world={s.Key} map={EmptyToDash(req.MapId)}");
                s.Close("game_bind_fail");
                return;
            }

            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=map_enter_ok route=Game uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={s.Key} world={s.CurrentWorldId} actor={actorId} seat={s.SeatIndex} map={EmptyToDash(req.MapId)}");

            // Game은 "로딩 완료" 상태가 되어야 시작하므로 MarkLoaded
            if (room.MarkLoadedAsync(s))
            {
                LogManager.Instance.LogInfo("GameRoom", "All Loaded! Scheduling Start...");
                var startAtMs = AppRef.ServerTimeMs() + GameStartTuning.ReadyLeadMs;
                room.BroadcastGameStart(startAtMs);
            }
        }
        // 4) Legacy Town Mode (Key empty)
        else
        {
            var townId = NormalizeTownMapId(req.MapId);
            LogManager.Instance.LogInfo("TownRoom", $"Enter townId={townId}");
            var room = TownManager.GetOrCreate(townId);
            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=map_enter_route route=Town uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} key={EmptyToDash(s.Key)} from={EmptyToDash(previousWorld)} world={townId} map={EmptyToDash(req.MapId)}");

            if (!room.BindOrReattach(s, out var actorId))
            {
                LogManager.Instance.LogWarning("MapEnter", $"Town BindFail uid={s.Uid}");
                LogManager.Instance.LogWarning(
                    "SessionLifecycle",
                    $"event=map_enter_fail reason=bind_fail route=Town uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} world={townId} map={EmptyToDash(req.MapId)}");
                s.Close("town_bind_fail");
                return;
            }

            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=map_enter_ok route=Town uid={s.Uid} epoch={s.Epoch} conn={s.ConnId} world={s.CurrentWorldId} actor={actorId} seat={s.SeatIndex} map={EmptyToDash(req.MapId)}");

            // Town은 즉시 InitMap 전송 (BindOrReattach 내부에서 처리됨)
        }
    }

    private static string NormalizeTownMapId(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
            return "Town_01";

        var trimmed = mapId.Trim();
        return string.Equals(trimmed, "town", StringComparison.OrdinalIgnoreCase)
            ? "Town_01"
            : trimmed;
    }

    private static string EmptyToDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    public static void CS_ReadyHandler(PacketSession session, IPacket packet)
    {
        ClientSession _session = (ClientSession)session;
        CS_MapEnter req = (CS_MapEnter)packet;

    }

    private static async Task<GameMatchManifestResponse?> TryLoadRelayManifestAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return null;

        try
        {
            var manifest = await ServerServices.ApiClient.GetGameMatchManifestAsync(roomId);
            if (manifest == null)
            {
                LogManager.Instance.LogWarning("P2PRelayRoom", $"Manifest missing roomId={roomId}");
                return null;
            }

            LogManager.Instance.LogInfo(
                "P2PRelayRoom",
                $"Manifest loaded roomId={roomId} hostUid={manifest.HostUid} participants={manifest.Participants?.Count ?? 0}");
            return manifest;
        }
        catch (Exception ex)
        {
            LogManager.Instance.LogError("P2PRelayRoom", $"Manifest load failed roomId={roomId} err={ex.Message}");
            return null;
        }
    }

    private static async Task<GameMatchManifestResponse?> TryLoadTownRelayManifestAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return null;

        try
        {
            var manifest = await ServerServices.ApiClient.GetTownMatchManifestAsync(roomId);
            if (manifest == null)
            {
                LogManager.Instance.LogWarning("TownP2PRelayRoom", $"Manifest missing roomId={roomId}");
                return null;
            }

            LogManager.Instance.LogInfo(
                "TownP2PRelayRoom",
                $"Manifest loaded roomId={roomId} hostUid={manifest.HostUid} participants={manifest.Participants?.Count ?? 0}");
            return manifest;
        }
        catch (Exception ex)
        {
            LogManager.Instance.LogError("TownP2PRelayRoom", $"Manifest load failed roomId={roomId} err={ex.Message}");
            return null;
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
        //Console.WriteLine($"[PONG] now={now} recv={pong.serverRecvMs} send={pong.serverSendMs}");



        _session.Send(pong.Write());

    }


    public static void CS_TownActionRequestHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_TownActionRequest req = (CS_TownActionRequest)packet;

        if (TownP2PRelayManager.TryGet(clientSession.CurrentWorldId, out var relayRoom))
        {
            relayRoom.OnCS_TownActionRequest(clientSession, req);
            return;
        }

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

        if (P2PRelayManager.TryGet(clientSession.CurrentWorldId, out var relayRoom))
        {
            relayRoom.OnCS_ActionRequest(clientSession, req);
            return;
        }

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

        if (P2PRelayManager.TryGet(clientSession.CurrentWorldId, out var relayRoom))
        {
            relayRoom.OnCS_CalibHit(clientSession, req);
            return;
        }

        GameManager.TryGet(clientSession.CurrentWorldId, out var room);//clientSession.roo
        if (room == null)
        {
            session.Send(new SC_Warn { code = 2000, msg = $"ROOM_NOT_FOUND : CurrentWorkdId: {clientSession.CurrentWorldId}" }.Write());
            return;
        }

        room.OnCS_CalibHit(clientSession, req);
    }

    public static void CS_P2PPayloadHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_P2PPayload req = (CS_P2PPayload)packet;

        if (TownP2PRelayManager.TryGet(clientSession.CurrentWorldId, out var townRelayRoom))
        {
            townRelayRoom.OnCS_P2PPayload(clientSession, req);
            return;
        }

        if (P2PRelayManager.TryGet(clientSession.CurrentWorldId, out var relayRoom))
        {
            relayRoom.OnCS_P2PPayload(clientSession, req);
            return;
        }

        LogManager.Instance.LogWarning(
            "P2PRelay",
            $"Payload dropped. Relay room not found world={clientSession.CurrentWorldId} uid={clientSession.Uid} actor={clientSession.ActorId} protocol={DescribeP2PPayloadProtocol(req)}");
    }

    public static void CS_P2PGameResultHandler(PacketSession session, IPacket packet)
    {
        ClientSession clientSession = (ClientSession)session;
        CS_P2PGameResult req = (CS_P2PGameResult)packet;

        if (P2PRelayManager.TryGet(clientSession.CurrentWorldId, out var relayRoom))
        {
            relayRoom.OnCS_P2PGameResult(clientSession, req);
            return;
        }

        LogManager.Instance.LogWarning("P2PRelay", $"GameResult dropped. Relay room not found world={clientSession.CurrentWorldId}");
    }

    private static string DescribeP2PPayloadProtocol(CS_P2PPayload req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Payload))
            return "-";

        try
        {
            var bytes = Convert.FromBase64String(req.Payload);
            if (bytes.Length < 4)
                return "ShortPayload";

            ushort protocol = BitConverter.ToUInt16(bytes, 2);
            return Enum.IsDefined(typeof(PacketID), (int)protocol)
                ? $"{(PacketID)protocol}({protocol})"
                : protocol.ToString();
        }
        catch
        {
            return "DecodeFailed";
        }
    }
}

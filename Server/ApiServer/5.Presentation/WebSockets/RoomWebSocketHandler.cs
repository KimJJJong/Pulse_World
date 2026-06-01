using ApiServer.Application.Ports;
using ApiServer.Domain.GameMatch;
using ApiServer.Domain.Town;
using ApiServer.Domain.WaitingRoom;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ApiServer.Presentation.WebSockets;

public sealed class RoomWebSocketHandler
{
    private readonly IControlPlanePort _cp;
    private readonly GameMatchService _gameMatch;
    private readonly TownRoomService _townRooms;
    private readonly WaitingRoomService _waitingRoom;
    private readonly ConnectionManager _conns;
    private readonly ILogger<RoomWebSocketHandler> _logger;

    public RoomWebSocketHandler(
        IControlPlanePort cp,
        GameMatchService gameMatch,
        TownRoomService townRooms,
        WaitingRoomService waitingRoom,
        ConnectionManager conns,
        ILogger<RoomWebSocketHandler> logger)
    {
        _cp = cp;
        _gameMatch = gameMatch;
        _townRooms = townRooms;
        _waitingRoom = waitingRoom;
        _conns = conns;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var uid = context.Items["uid"]?.ToString() ?? context.Request.Query["uid"].ToString();
        var name = context.Request.Query["name"].ToString();
        var roomId = context.Request.Query["roomId"].ToString();
        var steamId64 = context.Request.Query["steamId64"].ToString();
        var clientVersion = context.Request.Headers["X-Client-Version"].ToString();

        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(roomId))
        {
            context.Response.StatusCode = 401;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        _logger.LogInformation("WS Connected: Room={roomId}, Uid={uid}", roomId, uid);

        _conns.Add(roomId, uid, ws);

        try
        {
            // Join (Local)
            var (ok, joinErr) = await _waitingRoom.JoinAsync(roomId, uid, name);
            if (!ok) 
            {
                _logger.LogWarning("WS Join Failed: Room={roomId}, Uid={uid}, Err={err}", roomId, uid, joinErr);
                await CloseAsync(ws, "Join Failed: " + joinErr);
                return;
            }

            await _waitingRoom.UpdateMemberTransportAsync(roomId, uid, name, clientVersion, steamId64, -1, 0);

            var (_, room) = await _waitingRoom.GetAsync(roomId);
            if (room == null) // Strange timing
            {
                 await CloseAsync(ws, "Room Disappeared");
                 return;
            }

            _logger.LogInformation("WS Join Success: Room={roomId}, Uid={uid}", roomId, uid);

            // Broadcast Join
            await _conns.BroadcastAsync(roomId, new { type = "MemberJoin", uid, name });
            
            // Send Init
            var clientRoom = new 
            {
                roomId = room.RoomId,
                title = room.Title,
                mapId = room.MapId,
                maxPlayers = room.MaxPlayers,
                ownerUid = room.OwnerUid,
                status = room.Status,
                useP2PRelay = room.UseP2PRelay,
                steamLobbyId = room.SteamLobbyId,
                preferredHostUid = room.PreferredHostUid,
                hostEpoch = room.HostEpoch,
                hostSelectionEpoch = room.HostSelectionEpoch,
                hostSelectionMode = room.HostSelectionMode,
                hostSelectionMetricVersion = room.HostSelectionMetricVersion,
                hostSelectionScore = room.HostSelectionScore,
                hostSelectionUpdatedAtMs = room.HostSelectionUpdatedAtMs,
                hostCandidateOrder = room.HostCandidateOrder,
                hostSelectionCandidates = room.HostSelectionCandidates.Select(x => new
                {
                    uid = x.Uid,
                    isEligible = x.IsEligible,
                    candidateCost = x.CandidateCost,
                    averagePairCost = x.AveragePairCost,
                    worstPairCost = x.WorstPairCost,
                    averagePairRttMs = x.AveragePairRttMs,
                    worstPairRttMs = x.WorstPairRttMs,
                    steamPairCount = x.SteamPairCount,
                    measuredSteamPairCount = x.MeasuredSteamPairCount,
                    proxySteamPairCount = x.ProxySteamPairCount,
                    serverRelayPairCount = x.ServerRelayPairCount,
                    unavailablePairCount = x.UnavailablePairCount,
                    hostCapacityPenalty = x.HostCapacityPenalty,
                    steamReady = x.SteamReady,
                    currentServerRttMs = x.CurrentServerRttMs,
                    currentServerLossPct = x.CurrentServerLossPct,
                    currentServerJitterMs = x.CurrentServerJitterMs,
                    avgFrameMs = x.AvgFrameMs,
                    p95FrameMs = x.P95FrameMs,
                    disqualifiedReasons = x.DisqualifiedReasons
                }).ToList(),
                sourceTownRoomId = room.SourceTownRoomId,
                requiredMemberUids = room.RequiredMemberUids,
                memberUids = room.MemberUids,
                memberReady = room.MemberReady.Select(kv => new { uid = kv.Key, ready = kv.Value }).ToList(),
                memberTransport = room.MemberTransport.Select(x => new
                {
                    uid = x.Uid,
                    name = x.Name,
                    steamId64 = x.SteamId64,
                    clientVersion = x.ClientVersion,
                    hostProbeRttMs = x.HostProbeRttMs,
                    hostProbeReportedAtMs = x.HostProbeReportedAtMs,
                    steamEnabled = x.SteamEnabled,
                    steamInitialized = x.SteamInitialized,
                    steamLobbyJoined = x.SteamLobbyJoined,
                    steamReady = x.SteamReady,
                    currentServerRttMs = x.CurrentServerRttMs,
                    currentServerLossPct = x.CurrentServerLossPct,
                    currentServerJitterMs = x.CurrentServerJitterMs,
                    avgFrameMs = x.AvgFrameMs,
                    p95FrameMs = x.P95FrameMs,
                    sendQueueDepth = x.SendQueueDepth,
                    measuredSteamPairs = x.MeasuredSteamPairs.Select(p => new
                    {
                        peerUid = p.PeerUid,
                        peerSteamId64 = p.PeerSteamId64,
                        rttMs = p.RttMs,
                        connectionQualityLocal = p.ConnectionQualityLocal,
                        connectionQualityRemote = p.ConnectionQualityRemote,
                        connected = p.Connected,
                        reportedAtMs = p.ReportedAtMs,
                        source = p.Source
                    }).ToList(),
                    hostSelectionReportedAtMs = x.HostSelectionReportedAtMs
                }).ToList()
            };
            await _conns.SendToAsync(roomId, uid, new { type = "Init", room = clientRoom });
            await BroadcastHostSelectionAsync(roomId, room);

            var buffer = new byte[1024 * 4];
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessMessageAsync(roomId, uid, msg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WS Error");
        }
        finally
        {
            _logger.LogInformation("WS Disconnected: Room={roomId}, Uid={uid}", roomId, uid);
            _conns.Remove(roomId, uid);
            
            // Leave (Local)
            if (await _waitingRoom.LeaveAsync(roomId, uid))
            {
                await _conns.BroadcastAsync(roomId, new { type = "MemberLeave", uid });
                await BroadcastHostSelectionAsync(roomId);
            }
        }
    }

    private async Task ProcessMessageAsync(string roomId, string uid, string json)
    {
        _logger.LogInformation("WS Msg: Room={roomId}, Uid={uid}, Payload={json}", roomId, uid, json);
        try 
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            if (type == "Ready")
            {
                var val = root.GetProperty("value").GetBoolean();
                // SetReady (Local)
                if (await _waitingRoom.SetReadyAsync(roomId, uid, val))
                {
                    await _conns.BroadcastAsync(roomId, new { type = "MemberUpdate", uid, ready = val });
                    await BroadcastHostSelectionAsync(roomId);
                }
            }
            else if (type == "HostProbePing")
            {
                var nonce = root.TryGetProperty("nonce", out var nonceElement)
                    ? nonceElement.GetString() ?? ""
                    : "";

                await _conns.SendToAsync(roomId, uid, new
                {
                    type = "HostProbePong",
                    nonce,
                    serverTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
            else if (type == "HostProbeReport")
            {
                var probeRttMs = root.TryGetProperty("rttMs", out var rttElement) && rttElement.TryGetInt32(out var parsedRtt)
                    ? parsedRtt
                    : -1;
                var reportedAtMs = root.TryGetProperty("reportedAtMs", out var reportedElement) && reportedElement.TryGetInt64(out var parsedReportedAt)
                    ? parsedReportedAt
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (await _waitingRoom.UpdateMemberTransportAsync(roomId, uid, "", "", "", probeRttMs, reportedAtMs))
                {
                    var (_, room) = await _waitingRoom.GetAsync(roomId);
                    _logger.LogInformation(
                        "[WaitingRoom] Host probe room={RoomId} uid={Uid} rtt={Rtt} reportedAt={ReportedAt} preferredHost={PreferredHost} hostEpoch={HostEpoch}",
                        roomId,
                        uid,
                        probeRttMs,
                        reportedAtMs,
                        room?.PreferredHostUid ?? "",
                        room?.HostSelectionEpoch ?? 0);
                    await BroadcastHostSelectionAsync(roomId, room);
                }
            }
            else if (type == "HostSelectionReport")
            {
                var steamId = root.TryGetProperty("steamId64", out var steamIdElement)
                    ? steamIdElement.GetString() ?? ""
                    : "";
                var steamEnabled = root.TryGetProperty("steamEnabled", out var steamEnabledElement) && steamEnabledElement.GetBoolean();
                var steamInitialized = root.TryGetProperty("steamInitialized", out var steamInitializedElement) && steamInitializedElement.GetBoolean();
                var steamLobbyJoined = root.TryGetProperty("steamLobbyJoined", out var steamLobbyJoinedElement) && steamLobbyJoinedElement.GetBoolean();
                var steamReady = root.TryGetProperty("steamReady", out var steamReadyElement) && steamReadyElement.GetBoolean();
                var currentServerRttMs = root.TryGetProperty("currentServerRttMs", out var currentServerRttElement) && currentServerRttElement.TryGetInt32(out var parsedCurrentServerRtt)
                    ? parsedCurrentServerRtt
                    : -1;
                var currentServerLossPct = root.TryGetProperty("currentServerLossPct", out var currentServerLossElement) && currentServerLossElement.TryGetSingle(out var parsedCurrentServerLoss)
                    ? parsedCurrentServerLoss
                    : 0f;
                var currentServerJitterMs = root.TryGetProperty("currentServerJitterMs", out var currentServerJitterElement) && currentServerJitterElement.TryGetInt32(out var parsedCurrentServerJitter)
                    ? parsedCurrentServerJitter
                    : -1;
                var avgFrameMs = root.TryGetProperty("avgFrameMs", out var avgFrameElement) && avgFrameElement.TryGetSingle(out var parsedAvgFrame)
                    ? parsedAvgFrame
                    : -1f;
                var p95FrameMs = root.TryGetProperty("p95FrameMs", out var p95FrameElement) && p95FrameElement.TryGetSingle(out var parsedP95Frame)
                    ? parsedP95Frame
                    : -1f;
                var sendQueueDepth = root.TryGetProperty("sendQueueDepth", out var sendQueueDepthElement) && sendQueueDepthElement.TryGetInt32(out var parsedSendQueueDepth)
                    ? parsedSendQueueDepth
                    : 0;
                var measuredSteamPairs = root.TryGetProperty("measuredSteamPairs", out var measuredSteamPairsElement) && measuredSteamPairsElement.ValueKind == JsonValueKind.Array
                    ? measuredSteamPairsElement.EnumerateArray()
                        .Select(item => new WaitingRoomMeasuredSteamPairDto
                        {
                            PeerUid = item.TryGetProperty("peerUid", out var peerUidElement) ? peerUidElement.GetString() ?? "" : "",
                            PeerSteamId64 = item.TryGetProperty("peerSteamId64", out var peerSteamIdElement) ? peerSteamIdElement.GetString() ?? "" : "",
                            RttMs = item.TryGetProperty("rttMs", out var pairRttElement) && pairRttElement.TryGetInt32(out var parsedPairRtt)
                                ? parsedPairRtt
                                : -1,
                            ConnectionQualityLocal = item.TryGetProperty("connectionQualityLocal", out var qualityLocalElement) && qualityLocalElement.TryGetSingle(out var parsedQualityLocal)
                                ? parsedQualityLocal
                                : -1f,
                            ConnectionQualityRemote = item.TryGetProperty("connectionQualityRemote", out var qualityRemoteElement) && qualityRemoteElement.TryGetSingle(out var parsedQualityRemote)
                                ? parsedQualityRemote
                                : -1f,
                            Connected = item.TryGetProperty("connected", out var connectedElement) && connectedElement.GetBoolean(),
                            ReportedAtMs = item.TryGetProperty("reportedAtMs", out var pairReportedAtElement) && pairReportedAtElement.TryGetInt64(out var parsedPairReportedAt)
                                ? parsedPairReportedAt
                                : 0L,
                            Source = item.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() ?? "" : ""
                        })
                        .ToList()
                    : new List<WaitingRoomMeasuredSteamPairDto>();
                var reportedAtMs = root.TryGetProperty("reportedAtMs", out var selectionReportedElement) && selectionReportedElement.TryGetInt64(out var parsedSelectionReportedAt)
                    ? parsedSelectionReportedAt
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (await _waitingRoom.UpdateMemberHostSelectionAsync(
                        roomId,
                        uid,
                        steamId,
                        steamEnabled,
                        steamInitialized,
                        steamLobbyJoined,
                        steamReady,
                        currentServerRttMs,
                        currentServerLossPct,
                        currentServerJitterMs,
                        avgFrameMs,
                        p95FrameMs,
                        sendQueueDepth,
                        measuredSteamPairs,
                        reportedAtMs))
                {
                    var (_, room) = await _waitingRoom.GetAsync(roomId);
                    _logger.LogInformation(
                        "[WaitingRoom] Host selection report room={RoomId} uid={Uid} steamReady={SteamReady} serverRtt={ServerRtt} jitter={Jitter} loss={Loss} measuredPairs={MeasuredPairs} preferredHost={PreferredHost} mode={Mode} epoch={Epoch}",
                        roomId,
                        uid,
                        steamReady,
                        currentServerRttMs,
                        currentServerJitterMs,
                        currentServerLossPct,
                        measuredSteamPairs.Count,
                        room?.PreferredHostUid ?? "",
                        room?.HostSelectionMode ?? "",
                        room?.HostSelectionEpoch ?? 0);
                    await BroadcastHostSelectionAsync(roomId, room);
                }
            }
            else if (type == "BindSteamLobby")
            {
                var steamLobbyId = root.TryGetProperty("steamLobbyId", out var lobbyElement)
                    ? lobbyElement.GetString() ?? ""
                    : "";

                if (await _waitingRoom.BindSteamLobbyAsync(roomId, steamLobbyId))
                {
                    await _conns.BroadcastAsync(roomId, new
                    {
                        type = "SteamLobbyBound",
                        steamLobbyId
                    });
                }
            }
            else if (type == "Start")
            {
                try 
                {
                    // 1. Get Room Info for Ready Check (Local)
                    var (ok, room) = await _waitingRoom.GetAsync(roomId);
                    if (!ok || room == null)
                        throw new Exception("Room info not found");

                    if (room.OwnerUid != uid)
                        throw new Exception("Only owner can start");

                    // 2. Ready Check
                    var requiredMemberUids = await ResolveRequiredMemberUidsForStartAsync(room);
                    _logger.LogInformation("Start Check Room={roomId} Owner={owner} Members={members} Required={required} Ready={ready}",
                        roomId,
                        room.OwnerUid,
                        string.Join(",", room.MemberUids),
                        string.Join(",", requiredMemberUids),
                        string.Join(",", room.MemberReady.Select(kv => $"{kv.Key}:{kv.Value}")));

                    var joinedMembers = new HashSet<string>(room.MemberUids, StringComparer.OrdinalIgnoreCase);
                    var missingRequiredMembers = requiredMemberUids
                        .Where(m => !joinedMembers.Contains(m))
                        .ToList();
                    if (missingRequiredMembers.Count > 0)
                    {
                        _logger.LogWarning(
                            "Start Fail: Missing required members. Room={roomId}, Missing={missing}",
                            roomId,
                            string.Join(",", missingRequiredMembers));
                        throw new Exception("파티원이 모두 Game 대기방에 입장해야 시작할 수 있습니다.");
                    }

                    var allReady = requiredMemberUids
                        .Where(m => !string.Equals(m, room.OwnerUid, StringComparison.OrdinalIgnoreCase))
                        .All(m => room.MemberReady.TryGetValue(m, out var isReady) && isReady);

                    if(!allReady) 
                    {
                        _logger.LogWarning("Start Fail: Not all ready. Room={roomId}", roomId);
                        throw new Exception("참가자가 모두 준비해야 시작할 수 있습니다."); // User friendly message
                    }

                    // 3. Allocate GameServer (RPC to CP)
                    // ReserveTtl: 60s
                    string serverId, reserveId;
                    ApiServer.Application.Ports.Models.Endpoint ep;
                    long expireAt;

                    try 
                    {
                        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        (serverId, ep, reserveId, expireAt) = await _cp.AllocateGameServerAsync(uid, "", 60, nowMs, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                         throw new Exception("No game server available: " + ex.Message);
                    }

                    var ownerTransport = room.MemberTransport
                        .FirstOrDefault(x => string.Equals(x.Uid, uid, StringComparison.OrdinalIgnoreCase));
                    var protocolVersion = ownerTransport?.ClientVersion ?? "0.0.0";
                    var matchManifest = await _gameMatch.CreateOrReplaceForWaitingRoomAsync(
                        room,
                        room.UseP2PRelay ? "steam_p2p_host" : "gameserver",
                        protocolVersion,
                        CancellationToken.None);

                    // 4. Issue Tickets & Broadcast
                    // (Ideally, CP should support BulkIssue, but for now loop)
                    foreach(var memberUid in room.MemberUids)
                    {
                        // IssueTicket (RPC)
                        // key: relay prefix + roomId (for validation in GS)
                        var ticketKey = room.UseP2PRelay ? $"p2p:{roomId}" : roomId;
                        try 
                        {
                            var (ticketId, _, _, _, _) = await _cp.IssueTicketAsync(
                                memberUid, 
                                "GAME", 
                                ticketKey, 
                                serverId, // issuedServerId
                                60, 
                                CancellationToken.None
                            );
                            
                            var payload = new 
                            { 
                                type = "GameStart", 
                                endpoint = new { host = ep.Host, port = ep.Port },
                                ticket = ticketId,
                                mapId = room.MapId,
                                maxPlayers = room.MemberUids.Count, // 실제 참여 인원으로 시작
                                useP2PRelay = room.UseP2PRelay,
                                matchManifest
                            };
                            var sent = await _conns.SendToAsync(roomId, memberUid, payload);
                            if (sent)
                            {
                                _logger.LogInformation(
                                    "[WaitingRoom] GameStart sent room={RoomId} uid={Uid} map={MapId} relay={Relay} host={HostUid}",
                                    roomId,
                                    memberUid,
                                    room.MapId,
                                    room.UseP2PRelay,
                                    matchManifest.HostUid);
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "[WaitingRoom] GameStart send skipped - no active websocket room={RoomId} uid={Uid} map={MapId}",
                                    roomId,
                                    memberUid,
                                    room.MapId);
                            }
                        }
                        catch (Exception ticketEx)
                        {
                            _logger.LogError(ticketEx, "Failed to issue ticket for {memberUid}", memberUid);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await _conns.SendToAsync(roomId, uid, new { type = "Error", message = ex.Message });
                }
            }
        }
        catch (Exception ex) 
        {
             _logger.LogError(ex, "Msg Process Fail");
        }
    }

    private async Task<List<string>> ResolveRequiredMemberUidsForStartAsync(WaitingRoomDto room)
    {
        if (room == null)
            return new List<string>();

        if (string.IsNullOrWhiteSpace(room.SourceTownRoomId))
        {
            var configuredMembers = room.RequiredMemberUids.Count > 0
                ? room.RequiredMemberUids
                : room.MemberUids;
            return NormalizeRequiredStartMembers(configuredMembers, room.OwnerUid);
        }

        var townRoom = await _townRooms.GetAsync(room.SourceTownRoomId);
        if (townRoom == null)
            throw new Exception("Town Room 정보를 찾을 수 없습니다.");

        if (!string.Equals(townRoom.OwnerUid, room.OwnerUid, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Town Host와 Game 대기방 Host가 일치하지 않습니다.");

        if (!string.Equals(townRoom.ActiveGameRoomId, room.RoomId, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Town에 공유된 Game 대기방이 아닙니다.");

        return NormalizeRequiredStartMembers(
            townRoom.Participants.Select(x => x.Uid),
            room.OwnerUid);
    }

    private static List<string> NormalizeRequiredStartMembers(IEnumerable<string>? uids, string ownerUid)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(ownerUid) && seen.Add(ownerUid))
            result.Add(ownerUid);

        if (uids == null)
            return result;

        foreach (var uid in uids)
        {
            if (!string.IsNullOrWhiteSpace(uid) && seen.Add(uid))
                result.Add(uid);
        }

        return result;
    }

    private async Task CloseAsync(WebSocket ws, string reason)
    {
        if(ws.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
    }

    private async Task BroadcastHostSelectionAsync(string roomId, WaitingRoomDto? room = null)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return;

        if (room == null)
        {
            var (_, fetchedRoom) = await _waitingRoom.GetAsync(roomId);
            room = fetchedRoom;
        }

        if (room == null)
            return;

        await _conns.BroadcastAsync(roomId, new
        {
            type = "HostCandidateUpdate",
            preferredHostUid = room.PreferredHostUid ?? "",
            hostEpoch = room.HostEpoch,
            hostSelectionEpoch = room.HostSelectionEpoch,
            hostSelectionMode = room.HostSelectionMode ?? "",
            hostSelectionMetricVersion = room.HostSelectionMetricVersion ?? WaitingRoomHostSelectionV1Calculator.MetricVersion,
            hostSelectionScore = room.HostSelectionScore,
            hostSelectionUpdatedAtMs = room.HostSelectionUpdatedAtMs,
            hostCandidateOrder = room.HostCandidateOrder ?? new List<string>(),
            hostSelectionCandidates = (room.HostSelectionCandidates ?? new List<WaitingRoomHostSelectionCandidateDto>())
                .Select(x => new
                {
                    uid = x.Uid,
                    isEligible = x.IsEligible,
                    candidateCost = x.CandidateCost,
                    averagePairCost = x.AveragePairCost,
                    worstPairCost = x.WorstPairCost,
                    averagePairRttMs = x.AveragePairRttMs,
                    worstPairRttMs = x.WorstPairRttMs,
                    steamPairCount = x.SteamPairCount,
                    measuredSteamPairCount = x.MeasuredSteamPairCount,
                    proxySteamPairCount = x.ProxySteamPairCount,
                    serverRelayPairCount = x.ServerRelayPairCount,
                    unavailablePairCount = x.UnavailablePairCount,
                    hostCapacityPenalty = x.HostCapacityPenalty,
                    steamReady = x.SteamReady,
                    currentServerRttMs = x.CurrentServerRttMs,
                    currentServerLossPct = x.CurrentServerLossPct,
                    currentServerJitterMs = x.CurrentServerJitterMs,
                    avgFrameMs = x.AvgFrameMs,
                    p95FrameMs = x.P95FrameMs,
                    disqualifiedReasons = x.DisqualifiedReasons
                }).ToList(),
            memberTransport = (room.MemberTransport ?? new List<WaitingRoomMemberTransportDto>())
                .Select(x => new
                {
                    uid = x.Uid,
                    name = x.Name,
                    steamId64 = x.SteamId64,
                    clientVersion = x.ClientVersion,
                    hostProbeRttMs = x.HostProbeRttMs,
                    hostProbeReportedAtMs = x.HostProbeReportedAtMs,
                    steamEnabled = x.SteamEnabled,
                    steamInitialized = x.SteamInitialized,
                    steamLobbyJoined = x.SteamLobbyJoined,
                    steamReady = x.SteamReady,
                    currentServerRttMs = x.CurrentServerRttMs,
                    currentServerLossPct = x.CurrentServerLossPct,
                    currentServerJitterMs = x.CurrentServerJitterMs,
                    avgFrameMs = x.AvgFrameMs,
                    p95FrameMs = x.P95FrameMs,
                    sendQueueDepth = x.SendQueueDepth,
                    measuredSteamPairs = x.MeasuredSteamPairs.Select(p => new
                    {
                        peerUid = p.PeerUid,
                        peerSteamId64 = p.PeerSteamId64,
                        rttMs = p.RttMs,
                        connectionQualityLocal = p.ConnectionQualityLocal,
                        connectionQualityRemote = p.ConnectionQualityRemote,
                        connected = p.Connected,
                        reportedAtMs = p.ReportedAtMs,
                        source = p.Source
                    }).ToList(),
                    hostSelectionReportedAtMs = x.HostSelectionReportedAtMs
                }).ToList()
        });
    }
}

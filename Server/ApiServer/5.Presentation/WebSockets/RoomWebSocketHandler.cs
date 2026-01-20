using ApiServer.Application.Ports;
using ApiServer.Domain.WaitingRoom;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ApiServer.Presentation.WebSockets;

public sealed class RoomWebSocketHandler
{
    private readonly IControlPlanePort _cp;
    private readonly WaitingRoomService _waitingRoom;
    private readonly ConnectionManager _conns;
    private readonly ILogger<RoomWebSocketHandler> _logger;

    public RoomWebSocketHandler(
        IControlPlanePort cp, 
        WaitingRoomService waitingRoom,
        ConnectionManager conns, 
        ILogger<RoomWebSocketHandler> logger)
    {
        _cp = cp;
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
                memberUids = room.MemberUids,
                memberReady = room.MemberReady.Select(kv => new { uid = kv.Key, ready = kv.Value }).ToList()
            };
            await _conns.SendToAsync(roomId, uid, new { type = "Init", room = clientRoom });

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
                    var allReady = room.MemberUids
                        .Where(m => m != room.OwnerUid)
                        .All(m => room.MemberReady.ContainsKey(m) && room.MemberReady[m]);

                    if(!allReady) 
                        throw new Exception("Not all members are ready");

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

                    // 4. Issue Tickets & Broadcast
                    // (Ideally, CP should support BulkIssue, but for now loop)
                    foreach(var memberUid in room.MemberUids)
                    {
                        // IssueTicket (RPC)
                        // key: roomId (for validation in GS)
                        try 
                        {
                            var (ticketOk, ticket, _, _, _) = await _cp.IssueTicketAsync(
                                memberUid, 
                                "GAME", 
                                roomId, 
                                serverId, // issuedServerId
                                60, 
                                CancellationToken.None
                            );
                            
                            var payload = new 
                            { 
                                type = "GameStart", 
                                endpoint = new { host = ep.Host, port = ep.Port },
                                ticket = ticket
                            };
                            await _conns.SendToAsync(roomId, memberUid, payload);
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

    private async Task CloseAsync(WebSocket ws, string reason)
    {
        if(ws.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
    }
}

using ApiServer.Application.Ports;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ApiServer.Presentation.WebSockets;

public sealed class RoomWebSocketHandler
{
    private readonly IControlPlanePort _cp;
    private readonly ConnectionManager _conns;
    private readonly ILogger<RoomWebSocketHandler> _logger;

    public RoomWebSocketHandler(IControlPlanePort cp, ConnectionManager conns, ILogger<RoomWebSocketHandler> logger)
    {
        _cp = cp;
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

        var uid = context.Items["Uid"]?.ToString() ?? context.Request.Query["uid"].ToString();
        var name = context.Request.Query["name"].ToString();
        var roomId = context.Request.Query["roomId"].ToString();

        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(roomId)) 
        {
            context.Response.StatusCode = 401;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        _conns.Add(roomId, uid, ws);

        try
        {
            // Join
            var (ok, room) = await _cp.JoinWaitingRoomAsync(roomId, uid, name, CancellationToken.None);
            if (!ok) 
            {
                await CloseAsync(ws, "Join Failed");
                return;
            }

            // Broadcast Join
            await _conns.BroadcastAsync(roomId, new { type = "MemberJoin", uid, name });
            // Send Init
            await _conns.SendToAsync(roomId, uid, new { type = "Init", room });

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
            _conns.Remove(roomId, uid);
            // Leave (Check if socket closed normally vs crash?) - Just leave always
            await _cp.LeaveWaitingRoomAsync(roomId, uid, CancellationToken.None);
            await _conns.BroadcastAsync(roomId, new { type = "MemberLeave", uid });
        }
    }

    private async Task ProcessMessageAsync(string roomId, string uid, string json)
    {
        try 
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            if (type == "Ready")
            {
                var val = root.GetProperty("value").GetBoolean();
                if (await _cp.SetMemberReadyAsync(roomId, uid, val, CancellationToken.None))
                {
                    await _conns.BroadcastAsync(roomId, new { type = "MemberUpdate", uid, ready = val });
                }
            }
            else if (type == "Start")
            {
                try 
                {
                    var (srvId, ep, tickets) = await _cp.StartGameSessionAsync(roomId, uid, CancellationToken.None);
                    
                    // Broadcast individual tickets
                    foreach(var (memberUid, ticket) in tickets)
                    {
                        var payload = new 
                        { 
                            type = "GameStart", 
                            endpoint = new { host = ep.Host, port = ep.Port },
                            ticket = ticket
                        };
                        await _conns.SendToAsync(roomId, memberUid, payload);
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

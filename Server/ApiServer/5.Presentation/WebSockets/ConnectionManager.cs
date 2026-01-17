using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ApiServer.Presentation.WebSockets;

public sealed class ConnectionManager
{
    // RoomId -> Uid -> WebSocket
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> _rooms = new();

    public void Add(string roomId, string uid, WebSocket ws)
    {
        var room = _rooms.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, WebSocket>());
        room[uid] = ws;
    }

    public void Remove(string roomId, string uid)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            room.TryRemove(uid, out _);
            if (room.IsEmpty) _rooms.TryRemove(roomId, out _);
        }
    }

    public async Task BroadcastAsync(string roomId, object message)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return;

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        var tasks = new List<Task>();
        foreach (var ws in room.Values)
        {
            if (ws.State == WebSocketState.Open)
            {
                tasks.Add(ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None));
            }
        }
        await Task.WhenAll(tasks);
    }

    public async Task SendToAsync(string roomId, string uid, object message)
    {
        if (_rooms.TryGetValue(roomId, out var room) && room.TryGetValue(uid, out var ws))
        {
            if (ws.State == WebSocketState.Open)
            {
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var bytes = Encoding.UTF8.GetBytes(json);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}

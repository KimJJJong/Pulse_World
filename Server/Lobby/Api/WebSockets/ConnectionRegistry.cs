using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;


namespace Lobby.Api.WebSockets;

public sealed class ConnectionRegistry
{
    // roomId -> userId -> (WebSocket set)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<WebSocket, byte>>> _rooms
        = new();

    public void Add(string roomId, string userId, WebSocket ws)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId) || ws is null) return;
        var users = _rooms.GetOrAdd(roomId, _ => new());
        var sockets = users.GetOrAdd(userId, _ => new());
        sockets.TryAdd(ws, 0);
    }

    public void Remove(string roomId, string userId, WebSocket ws)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId) || ws is null) return;
        if (!_rooms.TryGetValue(roomId, out var users)) return;

        if (users.TryGetValue(userId, out var sockets))
        {
            sockets.TryRemove(ws, out _);
            if (sockets.IsEmpty) users.TryRemove(userId, out _);
        }
        if (users.IsEmpty) _rooms.TryRemove(roomId, out _);
    }

    /// <summary>
    /// 방 전체 브로드캐스트(타입 안정). payload가 string이면 그대로, 아니면 WireJson 직렬화.
    /// </summary>
    public async Task BroadcastAsync<T>(string roomId, T payload, CancellationToken ct = default)
    {
        if (!_rooms.TryGetValue(roomId, out var users) || users.IsEmpty) return;

        var json = payload as string ?? Contracts.Packet.WireJson.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        var tasks = new List<Task>();
        foreach (var kv in users) // kv: Key=userId, Value=socketSet
        {
            var userId = kv.Key;
            foreach (var ws in kv.Value.Keys)
            {
                if (ws.State == WebSocketState.Open)
                    tasks.Add(SendSafeAndPrune(roomId, userId, ws, bytes, ct));
                else
                    Remove(roomId, userId, ws);
            }
        }
        if (tasks.Count > 0) await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 특정 유저(모든 소켓)에 유니캐스트 전송.
    /// </summary>
    public Task SendAsync<T>(string roomId, string userId, T payload, CancellationToken ct = default)
    {
        if (!_rooms.TryGetValue(roomId, out var users)) return Task.CompletedTask;
        if (!users.TryGetValue(userId, out var sockets) || sockets.IsEmpty) return Task.CompletedTask;

        var json = payload as string ?? Contracts.Packet.WireJson.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        var tasks = new List<Task>(sockets.Count);
        foreach (var ws in sockets.Keys)
        {
            if (ws.State == WebSocketState.Open)
                tasks.Add(SendSafeAndPrune(roomId, userId, ws, bytes, ct));
            else
                Remove(roomId, userId, ws);
        }
        return tasks.Count > 0 ? Task.WhenAll(tasks) : Task.CompletedTask;
    }

    /// <summary>
    /// 여러 유저에게 멀티캐스트 전송(선택).
    /// </summary>
    public async Task MulticastAsync<T>(string roomId, IEnumerable<string> userIds, T payload, CancellationToken ct = default)
    {
        if (!_rooms.TryGetValue(roomId, out var users) || users.IsEmpty) return;

        var set = new HashSet<string>(userIds);
        var json = payload as string ?? Contracts.Packet.WireJson.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        var tasks = new List<Task>();
        foreach (var uid in set)
        {
            if (!users.TryGetValue(uid, out var sockets) || sockets.IsEmpty) continue;
            foreach (var ws in sockets.Keys)
            {
                if (ws.State == WebSocketState.Open)
                    tasks.Add(SendSafeAndPrune(roomId, uid, ws, bytes, ct));
                else
                    Remove(roomId, uid, ws);
            }
        }
        if (tasks.Count > 0) await Task.WhenAll(tasks);
    }

    private async Task SendSafeAndPrune(string roomId, string userId, WebSocket ws, byte[] bytes, CancellationToken ct)
    {
        try { await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct); }
        catch { Remove(roomId, userId, ws); } // 실패 소켓 제거
    }

    public int CountInRoom(string roomId)
        => _rooms.TryGetValue(roomId, out var users) ? users.Sum(kv => kv.Value.Count) : 0;
}

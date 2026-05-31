using System.Collections.Concurrent;
using System.Linq;
using Shared;

public static class P2PRelayManager
{
    private static readonly ConcurrentDictionary<string, P2PRelayRoom> _rooms = new();

    public static P2PRelayRoom GetOrCreate(string relayId, string mapId, int maxPlayers)
    {
        if (_rooms.TryGetValue(relayId, out var existing))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=GameP2P world={relayId} created=False map={existing.MapId} max={maxPlayers} total={_rooms.Count}");
            return existing;
        }

        var createdRoom = new P2PRelayRoom(relayId, mapId, maxPlayers);
        if (_rooms.TryAdd(relayId, createdRoom))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_create roomType=GameP2P world={relayId} map={createdRoom.MapId} max={maxPlayers} total={_rooms.Count}");
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=GameP2P world={relayId} created=True map={createdRoom.MapId} max={maxPlayers} total={_rooms.Count}");
            return createdRoom;
        }

        var room = _rooms[relayId];

        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_get roomType=GameP2P world={relayId} created=False raceLost=True map={room.MapId} max={maxPlayers} total={_rooms.Count}");
        return room;
    }

    public static bool TryGet(string relayId, out P2PRelayRoom room)
        => _rooms.TryGetValue(relayId, out room);

    public static void Remove(string relayId)
    {
        if (_rooms.TryRemove(relayId, out var room))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_remove roomType=GameP2P world={relayId} map={room.MapId} remaining={_rooms.Count}");
            return;
        }

        LogManager.Instance.LogWarning(
            "RoomLifecycle",
            $"event=room_remove_skip reason=not_found roomType=GameP2P world={relayId} remaining={_rooms.Count}");
    }

    public static IUpdatable[] GetUpdatablesSnapshot()
        => _rooms.Values.Cast<IUpdatable>().ToArray();
}

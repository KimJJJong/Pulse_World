using System.Collections.Concurrent;
using System.Linq;
using Shared;

public static class TownP2PRelayManager
{
    private static readonly ConcurrentDictionary<string, TownP2PRelayRoom> _rooms = new();

    public static TownP2PRelayRoom GetOrCreate(string relayId, string mapId, int maxPlayers)
    {
        if (_rooms.TryGetValue(relayId, out var existing))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=TownP2P world={relayId} created=False map={existing.MapId} max={maxPlayers} total={_rooms.Count}");
            return existing;
        }

        var createdRoom = new TownP2PRelayRoom(relayId, mapId, maxPlayers);
        if (_rooms.TryAdd(relayId, createdRoom))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_create roomType=TownP2P world={relayId} map={createdRoom.MapId} max={maxPlayers} total={_rooms.Count}");
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=TownP2P world={relayId} created=True map={createdRoom.MapId} max={maxPlayers} total={_rooms.Count}");
            return createdRoom;
        }

        var room = _rooms[relayId];

        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_get roomType=TownP2P world={relayId} created=False raceLost=True map={room.MapId} max={maxPlayers} total={_rooms.Count}");
        return room;
    }

    public static bool TryGet(string relayId, out TownP2PRelayRoom room)
        => _rooms.TryGetValue(relayId, out room);

    public static void Remove(string relayId)
    {
        if (_rooms.TryRemove(relayId, out var room))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_remove roomType=TownP2P world={relayId} map={room.MapId} remaining={_rooms.Count}");
            return;
        }

        LogManager.Instance.LogWarning(
            "RoomLifecycle",
            $"event=room_remove_skip reason=not_found roomType=TownP2P world={relayId} remaining={_rooms.Count}");
    }

    public static IUpdatable[] GetUpdatablesSnapshot()
        => _rooms.Values.Cast<IUpdatable>().ToArray();
}

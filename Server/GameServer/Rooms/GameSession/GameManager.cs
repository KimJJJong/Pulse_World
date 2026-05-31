using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shared;

public static class GameManager
{
    static readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public static GameRoom GetOrCreate(string matchId, string mapId, int maxPlayers)
    {
        if (_rooms.TryGetValue(matchId, out var existing))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=Game world={matchId} created=False map={existing.MapId} max={maxPlayers} total={_rooms.Count}");
            return existing;
        }

        var createdRoom = new GameRoom(matchId, mapId, maxPlayers: maxPlayers);
        if (_rooms.TryAdd(matchId, createdRoom))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_create roomType=Game world={matchId} map={createdRoom.MapId} max={maxPlayers} total={_rooms.Count}");
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=Game world={matchId} created=True map={createdRoom.MapId} max={maxPlayers} total={_rooms.Count}");
            return createdRoom;
        }

        var room = _rooms[matchId];

        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_get roomType=Game world={matchId} created=False raceLost=True map={room.MapId} max={maxPlayers} total={_rooms.Count}");
        return room;
    }

    public static bool TryGet(string matchId, out GameRoom room)
        => _rooms.TryGetValue(matchId, out room);

    public static void Remove(string matchId)
    {
        if (_rooms.TryRemove(matchId, out var room))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_remove roomType=Game world={matchId} map={room.MapId} remaining={_rooms.Count}");
            return;
        }

        LogManager.Instance.LogWarning(
            "RoomLifecycle",
            $"event=room_remove_skip reason=not_found roomType=Game world={matchId} remaining={_rooms.Count}");
    }

    public static IUpdatable[] GetUpdatablesSnapshot()
        => _rooms.Values.Cast<IUpdatable>().ToArray();

}


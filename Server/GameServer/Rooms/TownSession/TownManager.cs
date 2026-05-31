using System.Collections.Concurrent;
using System.Linq;
using Shared;

public static class TownManager
{
    static readonly ConcurrentDictionary<string, TownRoom> _towns = new();

    public static TownRoom GetOrCreate(string townId)
    {
        if (_towns.TryGetValue(townId, out var existing))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=Town world={townId} created=False total={_towns.Count}");
            return existing;
        }

        var createdTown = new TownRoom(townId);
        if (_towns.TryAdd(townId, createdTown))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_create roomType=Town world={townId} total={_towns.Count}");
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=Town world={townId} created=True total={_towns.Count}");
            return createdTown;
        }

        var town = _towns[townId];

        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_get roomType=Town world={townId} created=False raceLost=True total={_towns.Count}");
        return town;
    }

    public static bool TryGet(string townId, out TownRoom town)
        => _towns.TryGetValue(townId, out town);

    public static void Remove(string townId)
    {
        if (_towns.TryRemove(townId, out _))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_remove roomType=Town world={townId} remaining={_towns.Count}");
            return;
        }

        LogManager.Instance.LogWarning(
            "RoomLifecycle",
            $"event=room_remove_skip reason=not_found roomType=Town world={townId} remaining={_towns.Count}");
    }

    public static IUpdatable[] GetUpdatablesSnapshot()
        => _towns.Values.Cast<IUpdatable>().ToArray();
}

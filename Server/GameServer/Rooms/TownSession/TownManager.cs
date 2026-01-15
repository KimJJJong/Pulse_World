using System.Collections.Concurrent;
using System.Linq;

public static class TownManager
{
    static readonly ConcurrentDictionary<string, TownRoom> _towns = new();

    public static TownRoom GetOrCreate(string townId)
        => _towns.GetOrAdd(townId, id => new TownRoom(id));

    public static bool TryGet(string townId, out TownRoom town)
        => _towns.TryGetValue(townId, out town);

    public static void Remove(string townId)
        => _towns.TryRemove(townId, out _);

    public static IUpdatable[] GetUpdatablesSnapshot()
        => _towns.Values.Cast<IUpdatable>().ToArray();
}

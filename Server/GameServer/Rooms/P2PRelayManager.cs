using System.Collections.Concurrent;
using System.Linq;

public static class P2PRelayManager
{
    private static readonly ConcurrentDictionary<string, P2PRelayRoom> _rooms = new();

    public static P2PRelayRoom GetOrCreate(string relayId, string mapId, int maxPlayers)
        => _rooms.GetOrAdd(relayId, id => new P2PRelayRoom(id, mapId, maxPlayers));

    public static bool TryGet(string relayId, out P2PRelayRoom room)
        => _rooms.TryGetValue(relayId, out room);

    public static void Remove(string relayId)
        => _rooms.TryRemove(relayId, out _);

    public static IUpdatable[] GetUpdatablesSnapshot()
        => _rooms.Values.Cast<IUpdatable>().ToArray();
}

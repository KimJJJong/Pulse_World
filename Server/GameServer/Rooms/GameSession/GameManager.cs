using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public static class GameManager
{
    static readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public static GameRoom GetOrCreate(string matchId, string mapId, int maxPlayers)
        => _rooms.GetOrAdd(matchId, id => new GameRoom(id, mapId, maxPlayers: maxPlayers));

    public static bool TryGet(string matchId, out GameRoom room)
        => _rooms.TryGetValue(matchId, out room);

    public static void Remove(string matchId)
        => _rooms.TryRemove(matchId, out _);

    public static IUpdatable[] GetUpdatablesSnapshot()
        => _rooms.Values.Cast<IUpdatable>().ToArray();

}


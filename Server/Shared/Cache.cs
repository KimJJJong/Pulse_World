using System.Collections.Generic;

public class PositionCache
{
    private Dictionary<(int sessionId, int x, int y), (int x, int y)> _clientToServer = new Dictionary<(int sessionId, int x, int y), (int x, int y)>();
    private Dictionary<(int sessionId, int x, int y), (int x, int y)> _serverToClient = new Dictionary<(int sessionId, int x, int y), (int x, int y)>();

    public PositionCache(int[] playerSessionIds)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 12; y++)
            {
                foreach (int sid in playerSessionIds)
                {
                    bool isPlayer0 = sid == playerSessionIds[0];
                    _clientToServer[(sid, x, y)] = isPlayer0 ? (x, y) : (7 - x, 11 - y);
                    _serverToClient[(sid, x, y)] = isPlayer0 ? (x, y) : (7 - x, 11 - y);
                }
            }
        }
    }

    public (int x, int y) ClientToServer(int sessionId, int x, int y)
        => _clientToServer.TryGetValue((sessionId, x, y), out var pos) ? pos : (x, y);

    public (int x, int y) ServerToClient(int sessionId, int x, int y)
        => _serverToClient.TryGetValue((sessionId, x, y), out var pos) ? pos : (x, y);
}

using System;
using System.Collections.Generic;

namespace GameServer.Content.Map;
public sealed class Map2D
{
    private readonly TileKind[,] _tiles;
    private List<(int, int)> _spawnPoints;
    public int Width { get; }
    public int Height { get; }

    public Map2D(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new TileKind[height, width];
        _spawnPoints = new List<(int, int)>();
    }

    public bool InBounds(int x, int y)
        => x >= 0 && y >= 0 && x < Width && y < Height;

    public TileKind Get(int x, int y)
    {
        if (!InBounds(x, y))
            return TileKind.None;
        return _tiles[y, x];
    }

    public void Set(int x, int y, TileKind kind)
    {
        if (!InBounds(x, y)) return;
        _tiles[y, x] = kind;
    }

    public bool IsWalkable(int x, int y)
    {
        var t = Get(x, y);
        return t == TileKind.Floor || t == TileKind.Spawn;
    }
    public void SetSpawnPoint(int x, int y)
    {

        _spawnPoints.Add((x,y));
    }


    public (int, int) GetSpawnPoint(int slot)
    {
        if (_spawnPoints.Count - 1 < slot) return (-1, -1);

        return _spawnPoints[slot];
    }
    
}

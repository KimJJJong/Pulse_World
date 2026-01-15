using System;
using System.Collections.Generic;

namespace GameServer.Content.Map;
public sealed class Map2D
{
    private static readonly Random _rng = new Random();

    private readonly TileKind[,] _tiles;
    private readonly List<(int, int)> _spawnPoints = new();
    private List<(int, int)> _shuffledSpawns = new();
    private int _spawnCursor = 0;
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

    public (int, int) GetSpawnPointRandom()
    {
        if (_spawnPoints.Count == 0)
            return (-1, -1);

        if (_shuffledSpawns.Count == 0 || _spawnCursor >= _shuffledSpawns.Count)
            ShuffleSpawnPoints();

        return _shuffledSpawns[_spawnCursor++];
    }

    private void ShuffleSpawnPoints()
    {
        _shuffledSpawns = new List<(int, int)>(_spawnPoints);
        for (int i = _shuffledSpawns.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_shuffledSpawns[i], _shuffledSpawns[j]) =
                (_shuffledSpawns[j], _shuffledSpawns[i]);
        }
        _spawnCursor = 0;
    }


}

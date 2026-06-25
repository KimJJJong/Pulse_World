using System;
using System.Collections.Generic;

public sealed class GridDistanceField
{
    public const int Unreachable = int.MaxValue;

    private static readonly int[] DirX = { 0, 0, 1, -1 };
    private static readonly int[] DirY = { 1, -1, 0, 0 };

    private int _width;
    private int _height;
    private int[] _distances = Array.Empty<int>();
    private int[] _queue = Array.Empty<int>();

    public GridDistanceField(int width, int height)
    {
        Resize(width, height);
    }

    public int Width => _width;
    public int Height => _height;

    public void Resize(int width, int height)
    {
        _width = Math.Max(0, width);
        _height = Math.Max(0, height);

        int size = _width * _height;
        if (_distances.Length != size)
            _distances = new int[size];
        if (_queue.Length != size)
            _queue = new int[size];
    }

    public void Build(IEnumerable<GridPos> goals, Func<int, int, bool> isWalkable)
    {
        if (isWalkable == null)
            throw new ArgumentNullException(nameof(isWalkable));

        for (int i = 0; i < _distances.Length; i++)
            _distances[i] = Unreachable;

        int head = 0;
        int tail = 0;

        foreach (var goal in goals)
        {
            if (!InBounds(goal.X, goal.Y) || !isWalkable(goal.X, goal.Y))
                continue;

            int idx = Index(goal.X, goal.Y);
            if (_distances[idx] == 0)
                continue;

            _distances[idx] = 0;
            _queue[tail++] = idx;
        }

        while (head < tail)
        {
            int idx = _queue[head++];
            int x = idx % _width;
            int y = idx / _width;
            int nextDistance = _distances[idx] + 1;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + DirX[i];
                int ny = y + DirY[i];
                if (!InBounds(nx, ny) || !isWalkable(nx, ny))
                    continue;

                int nextIdx = Index(nx, ny);
                if (_distances[nextIdx] != Unreachable)
                    continue;

                _distances[nextIdx] = nextDistance;
                _queue[tail++] = nextIdx;
            }
        }
    }

    public int GetDistance(int x, int y)
    {
        if (!InBounds(x, y))
            return Unreachable;

        return _distances[Index(x, y)];
    }

    public bool TryStepToward(
        GridPos from,
        int maxSteps,
        Func<int, int, bool> canEnter,
        out GridPos result)
    {
        if (canEnter == null)
            throw new ArgumentNullException(nameof(canEnter));

        result = from;
        if (!InBounds(from.X, from.Y))
            return false;

        int steps = Math.Max(1, maxSteps);
        GridPos current = from;
        bool moved = false;

        for (int i = 0; i < steps; i++)
        {
            int currentDistance = GetDistance(current.X, current.Y);
            if (!TryChooseNext(current, currentDistance, canEnter, out var next))
                break;

            current = next;
            moved = true;
        }

        result = current;
        return moved;
    }

    private bool TryChooseNext(
        GridPos from,
        int currentDistance,
        Func<int, int, bool> canEnter,
        out GridPos next)
    {
        next = from;

        int bestLowerDistance = Unreachable;
        GridPos bestLower = from;

        int bestFallbackDistance = Unreachable;
        GridPos bestFallback = from;

        for (int i = 0; i < 4; i++)
        {
            int nx = from.X + DirX[i];
            int ny = from.Y + DirY[i];
            if (!InBounds(nx, ny) || !canEnter(nx, ny))
                continue;

            int distance = GetDistance(nx, ny);
            if (distance == Unreachable)
                continue;

            if (distance < currentDistance && distance < bestLowerDistance)
            {
                bestLowerDistance = distance;
                bestLower = new GridPos(nx, ny);
                continue;
            }

            if (currentDistance > 1 && distance < bestFallbackDistance)
            {
                bestFallbackDistance = distance;
                bestFallback = new GridPos(nx, ny);
            }
        }

        if (bestLowerDistance != Unreachable)
        {
            next = bestLower;
            return true;
        }

        if (bestFallbackDistance != Unreachable)
        {
            next = bestFallback;
            return true;
        }

        return false;
    }

    private bool InBounds(int x, int y)
        => x >= 0 && y >= 0 && x < _width && y < _height;

    private int Index(int x, int y)
        => y * _width + x;
}

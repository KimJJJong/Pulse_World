public readonly struct GridPos
{
    public readonly int X;
    public readonly int Y;

    public GridPos(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X},{Y})";
}
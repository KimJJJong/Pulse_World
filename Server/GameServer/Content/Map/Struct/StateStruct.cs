using System;

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

public sealed class MapJson
{
    public string appearancePalette { get; set; } = "";
    public int width { get; set; }
    public int height { get; set; }
    public CellJson[] cells { get; set; } = Array.Empty<CellJson>();
}

public sealed class CellJson
{
    public byte k { get; set; }   // TileKind
    public byte v { get; set; }   // Variant (서버는 의미 없음)
    public byte a { get; set; }   // AppearanceTileKind (서버 로직은 의미 없음)
    public byte av { get; set; }  // Appearance AutoTile Variant
}

public sealed class MapContent
{
    public string MapId { get; init; } = "";
    public string AppearancePalette { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }

    // 서버 로직용 (Kind only)
    public TileKind[] Kind { get; init; } = Array.Empty<TileKind>();

    // 서버는 의미를 몰라도 됨 (그대로 클라에 전달용)
    public byte[] Variant { get; init; } = Array.Empty<byte>();
    public byte[] AppearanceKind { get; init; } = Array.Empty<byte>();
    public byte[] AppearanceVariant { get; init; } = Array.Empty<byte>();
}

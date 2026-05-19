public enum TileKind : byte
{
    None = 0,
    Floor = 1,
    Wall = 2,
    Spawn = 3,
}

public enum AppearanceTileKind : byte
{
    None = 0,
    GrassBorder = 1,
    StonePath = 2,
    BrickBorder = 3,
    WaterEdge = 4,
    WoodDeck = 5,
    Carpet = 6,
    FlowerBed = 7,
    CustomA = 8,
    CustomB = 9,
}

[System.Serializable]
public struct TileCell
{
    public TileKind Kind;
    public byte Variant; // 같은 Kind 안에서 다른 블록 구분
}

[System.Serializable]
public struct AppearanceTileCell
{
    public AppearanceTileKind Kind;
    public byte Variant; // 8방향 Auto Tile 연결 마스크
}

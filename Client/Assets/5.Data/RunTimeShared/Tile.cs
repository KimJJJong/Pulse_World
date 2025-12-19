public enum TileKind : byte
{
    None = 0,
    Floor = 1,
    Wall = 2,
    Spawn = 3,
}

[System.Serializable]
public struct TileCell
{
    public TileKind Kind;
    public byte Variant; // 같은 Kind 안에서 다른 블록 구분
}

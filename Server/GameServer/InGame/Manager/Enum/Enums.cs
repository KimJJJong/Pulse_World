
public enum TileKind : byte
{
    None = 0,  // 맵 바깥/사용 안 함
    Floor = 1,  // 통행 가능
    Wall = 2,  // 통행 불가
    Spawn = 3,  // 스폰용 but 통행 가능
}


public enum MonsterAiState
{
    Idle,
    Chase,
    Attack,
    Retreat
}

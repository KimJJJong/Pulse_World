using System.Collections.Generic;

namespace GameServer.InGame.Manager.Entity;

public enum EntityType : byte
{
    Player = 1,
    Monster = 2,
    Object = 3,
}

public /*sealed*/ class MapEntity
{
    /// <summary>
    /// 0 ~  99   : Player 영역 (슬롯 개수 내)
    //100 ~ 999 : 일반 몬스터
    //1000~1999 : 소환수/펫
    //2000~2999 : 투사체
    //3000~3999 : 오브젝트(트랩, 상자 등)
    /// </summary>
    public int Id { get; }
    public EntityType Type { get; }
    public GridPos Position { get; internal set; }
    public bool IsAlive { get; internal set; } = true;

    // 상태: "HP", "Stun", "Buff_Speed" 등 확장 가능
    private readonly Dictionary<string, object> _states = new();

    public MapEntity(int id, EntityType type, GridPos initialPos)
    {
        Id = id;
        Type = type;
        Position = initialPos;
        IsAlive = true;     // 생성시에는 살아 있어야징
    }

    public T GetState<T>(string key)
    {
        if (_states.TryGetValue(key, out var value) && value is T t)
            return t;
        return default;
    }

    public void SetState<T>(string key, T value)
    {
        _states[key] = value!;
    }

    public bool HasState(string key) => _states.ContainsKey(key);
}

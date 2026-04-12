using System.Collections.Generic;

namespace GameServer.InGame.Manager.Entity;

public enum EntityType : byte
{
    Player = 1,
    Monster = 2,
    Object = 3,
}

public  class MapEntity
{
    /// <summary>
    /// 0 ~  99   : Player 영역 
    //100 ~ 999 : 일반 몬스터
    //1000~1999 : 소환수/펫
    //2000~2999 : 투사체
    //3000~3999 : 오브젝트(트랩, 상자 등)
    /// </summary>
    public int Id { get; }
    public EntityType Type { get; }
    public GridPos Position { get; internal set; }
    public bool IsAlive { get; internal set; } = true;
    public float Rotation { get; set; } = 0.0f; // 시전자 바라보는 방향 (0:북, 90:동, 180:남, 270:서)
    
    // Status Effects
    public long StunEndTick { get; set; } = 0;
    public bool IsStunned(long currentTick) => currentTick < StunEndTick;

    // 상태: "HP", "Stun", ~~
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

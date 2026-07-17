using GameServer.Content.Map;
using GameServer.Content.Map.Interface;
using GameServer.InGame.Manager.Entity;
using System;
using System.Collections.Generic;

public abstract class SessionBase
{
    public int SessionId { get; }

    // 월드 / 맵
    public IGameWorld World { get; protected set; }
    public MapWorld2D World2D { get; protected set; }
    protected readonly Map2D _map;
    public Map2D Map => _map;

    // 외부 시스템
    protected readonly IServerTime _time;
    protected readonly IGameBroadcaster _broadcaster;

    // 룸 구성 요소
    protected readonly List<MapEntity> _players = new();
    
    // public accessor for checking
    public List<MapEntity> Players => _players;

    protected readonly HashSet<int> _actorIds = new(); // slot -> actorId


    protected SessionBase(int sessionId, IServerTime time, IGameBroadcaster broadcaster, Map2D map)
    {
        SessionId = sessionId;
        _time = time;
        _broadcaster = broadcaster;
        _map = map;

        World2D = new MapWorld2D(map, broadcaster);
        World = World2D;
    }

    // =====================================================
    // 공통: 플레이어 정리
    // =====================================================
    public virtual void OnPlayerLeft(int actorId)
    {
        if (actorId < 0)
        {
            Console.WriteLine($"[OnPlayerLeft] actorId < 0 : {actorId}");
            return;
        }

        CleanupActor(actorId);

        var p = _players.Find(x => x.Id == actorId);
        if (p != null) p.IsAlive = false;

        _players.RemoveAll(x => x.Id == actorId);
        _actorIds.Remove(actorId);

        Console.WriteLine($"[GameSession] OnPlayerLeft: Actor {actorId} removed. Remaining Players: {_players.Count}");
    }

    /// <summary>
    /// 세션별 추가 정리 포인트.
    /// 기본은 월드 디스폰만.
    /// </summary>
    protected virtual void CleanupActor(int actorId)
    {
        World2D.Despawn(actorId);
    }

    // =====================================================
    // 공통: 플레이어 초기 스폰/매핑
    // =====================================================
    protected void InitPlayers(IEnumerable<MapEntity> players)
    {
        _players.Clear();
        _actorIds.Clear();

        foreach (var p in players)
        {
            if (!World2D.TrySpawn(p, p.Position))
            {
                Console.WriteLine($"[InitPlayers] Player spawn failed actorId={p.Id}");
                continue;
            }

            _players.Add(p);
            _actorIds.Add(p.Id);

            Console.WriteLine($"[InitPlayers] Player spawned actorId={p.Id}");
        }
    }

    /// <summary>
    /// 재연결 시(reattach) 따로 스폰할 건 없지만,
    /// 혹시 월드에서 사라진 경우 복구할 수 있는 안전망.
    /// </summary>
    public virtual void EnsurePlayerSpawned(int actorId)
    {
        if (actorId < 0) return;
        if (!_actorIds.Contains(actorId)) return;

        if (World2D.ContainsEntity(actorId))
            return;

        var p = _players.Find(x => x.Id == actorId);
        if (p == null) return;

        if (!World2D.TrySpawn(p, p.Position))
        {
            Console.WriteLine($"[EnsurePlayerSpawned] respawn failed actorId={actorId}");
            return;
        }

        Console.WriteLine($"[EnsurePlayerSpawned] respawn OK actorId={actorId}");
    }

    public abstract void SendInitPacketToPlayer(ClientSession s);

    public virtual void Update() { }
}

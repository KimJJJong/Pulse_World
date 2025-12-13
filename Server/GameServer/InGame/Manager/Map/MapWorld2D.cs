namespace GameServer.InGame.Manager.Map;

using global::System;
using global::System.Collections.Generic;
using global::System.Linq;

using GameServer.InGame.Manager.Entity;
using GameServer.InGame.Manager.Map.Interface;
public sealed class MapWorld2D : IGameWorld
{
    private readonly Map2D _map;
    public Map2D Map => _map;
    // 엔티티 전체 목록
    private readonly Dictionary<int, MapEntity> _entities = new();

    // 셀별로 어떤 엔티티가 있는지
    private readonly Dictionary<(int x, int y), HashSet<int>> _entitiesByGrid = new();

    public MapWorld2D(Map2D map)
    {
        _map = map;
    }

    // ==== 엔티티 관리 ====

    public bool TrySpawn(MapEntity entity, GridPos at)
    {
        if (!_map.InBounds(at.X, at.Y))
            return false;

        if (!_map.IsWalkable(at.X, at.Y))
            return false;

        if (_entities.ContainsKey(entity.Id))
            return false;

        entity.Position = at;
        _entities[entity.Id] = entity;

        var key = (at.X, at.Y);
        if (!_entitiesByGrid.TryGetValue(key, out var set))
        {
            set = new HashSet<int>();
            _entitiesByGrid[key] = set;
        }
        set.Add(entity.Id);
        return true;
    }

    public bool Despawn(int entityId)
    {
        if (!_entities.TryGetValue(entityId, out var e))
            return false;

        var pos = e.Position;
        var key = (pos.X, pos.Y);

        if (_entitiesByGrid.TryGetValue(key, out var set))
            set.Remove(entityId);

        _entities.Remove(entityId);
        e.IsAlive = false;
        return true;
    }


    // ==== 위치 조회 ====

    public GridPos GetActorPosition(int actorId)
    {
        if (!_entities.TryGetValue(actorId, out var e))
            throw new KeyNotFoundException($"No entity {actorId}");

        return new GridPos(e.Position.X, e.Position.Y);
    }
   public bool ContainsEntity(int entityId)
       => _entities.ContainsKey(entityId);

   public bool TryGetEntity(int entityId, out MapEntity e)
       => _entities.TryGetValue(entityId, out e);

    public bool TryGetActorPosition(int actorId, out GridPos pos)
    {
        if (_entities.TryGetValue(actorId, out var e))
        {
            pos = new GridPos(e.Position.X, e.Position.Y);
            return true;
        }

        pos = default;
        return false;
    }

    // ==== 이동 처리 ====

    public bool TryMove(int actorId, GridPos target)
    {
        if (!_entities.TryGetValue(actorId, out MapEntity e))
            return false;

        GridPos from = e.Position;

        // 1) 맵 범위 & 통행 가능 체크
        if (!_map.InBounds(target.X, target.Y))
            return false;
        if (!_map.IsWalkable(target.X, target.Y))
            return false;

        // 2) 인접 칸인지 체크 (4방향 기준)
        var dx = Math.Abs(target.X - from.X);
        var dy = Math.Abs(target.Y - from.Y);
        if (dx + dy != 1)
        {
            // Beat당 1칸 이동만 허용 (원하면 수정 가능)
            return false;
        }

        // 3) 점유 상태 체크 (Player/Monster 한 칸 1개만 허용)
        var targetKey = (target.X, target.Y);
        if (_entitiesByGrid.TryGetValue(targetKey, out var occ))
        {
            if (occ.Count > 0)
                return false;
        }

        // 4) 이동 반영
        var fromKey = (from.X, from.Y);
        if (_entitiesByGrid.TryGetValue(fromKey, out var fromSet))
            fromSet.Remove(actorId);

        if (!_entitiesByGrid.TryGetValue(targetKey, out var toSet))
        {
            toSet = new HashSet<int>();
            _entitiesByGrid[targetKey] = toSet;
        }
        toSet.Add(actorId);

        e.Position = target;
        return true;
    }

    // ==== 스킬/오브젝트 상태 예시 ====

    public bool TryUseSkill(int actorId, int targetX, int targetY)
    {
        if (!_entities.TryGetValue(actorId, out var caster))
            return false;

        var center = new GridPos(targetX, targetY);


        // 간단 예: target 주변 1칸 안의 몬스터에게 데미지
        foreach (var e in GetEntitiesInRange(center, radius: 1))
        {
            if (e.Type != EntityType.Monster)
                continue;

            var hp = e.GetState<int>("HP");
            hp -= 10;
            e.SetState("HP", hp);

            //if (hp <= 0)
            //{
            //    Despawn(e.Id);
            //}
        }

        return true;

           }

    // ==== 조회 유틸 ====

    public IEnumerable<MapEntity> GetEntitiesInRange(GridPos center, int radius)
    {
        // 간단: 맨해튼 거리 기반 검색
        foreach (var kv in _entities)
        {
            var e = kv.Value;
            var dx = Math.Abs(e.Position.X - center.X);
            var dy = Math.Abs(e.Position.Y - center.Y);
            if (dx + dy <= radius)
                yield return e;
        }
    }

    public IEnumerable<MapEntity> GetEntitiesAt(GridPos pos)
    {
        var key = (pos.X, pos.Y);
        if(_entitiesByGrid.TryGetValue(key, out var set))
        {
            foreach(var id in set) 
                if(_entities.TryGetValue(id, out var e))
                    yield return e;
        }
    }

}

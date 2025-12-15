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
        if (!_map.InBounds(at.X, at.Y))         return false;
        if (!_map.IsWalkable(at.X, at.Y))       return false;
        if (_entities.ContainsKey(entity.Id))   return false;

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
    private bool TryGetEntityAt(int x, int y, out MapEntity entity)
    {
        var key = (x, y);
        if (_entitiesByGrid.TryGetValue(key, out var set))
        {
            foreach (var id in set)
            {
                if (_entities.TryGetValue(id, out var e) && e.IsAlive)
                {
                    entity = e;
                    return true;
                }
            }
        }

        entity = default!;
        return false;
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
        if (_entitiesByGrid.TryGetValue(key, out var set))
        {
            foreach (var id in set)
                if (_entities.TryGetValue(id, out var e))
                    yield return e;
        }
    }


    // ==== 이동 처리 ====

    public bool TryMove(int actorId, GridPos target)
    {
        if (!_entities.TryGetValue(actorId, out MapEntity e))
            return false;

        if (!e.IsAlive) return false;

        GridPos from = e.Position;

        // 1) 맵 범위 & 통행 가능 체크
        if (!_map.InBounds(target.X, target.Y))     return false;
        if (!_map.IsWalkable(target.X, target.Y))   return false;

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

    public bool TryUseSkill(int actorId, string skillId, int targetX, int targetY)
    {
        if (!_entities.TryGetValue(actorId, out var caster) || !caster.IsAlive)
            return false;

        // 프로토타입: 스킬 정의 없으면 기본값
        var skill = SkillDatabase.GetOrDefault(skillId);

        var center = new GridPos(targetX, targetY);

        var cells = BuildDiamond(center, radius: 1);

        return TryUseSkillArea(actorId, skillId, cells);
    }

    //  Freeze 셀 기반 판정(텔레그래프=판정 보장)
    public bool TryUseSkillArea(int actorId, string skillId, IReadOnlyList<GridPos> cells)
    {
        if (!TryGetEntity(actorId, out var attacker) || !attacker.IsAlive)
            return false;

        // 스킬 정의 조회
        if (!SkillDatabase.TryGet(skillId, out var skill))
        {
            // 운영 기준: 여기서 false가 더 안전함
            // 프로토타입이면 아래처럼 기본값으로 처리 가능
            skill = SkillDatabase.GetOrDefault(skillId);
        }

        var hitTargets = new HashSet<int>();
        bool anyHit = false;

        foreach (var c in cells)
        {
            if (!IsInside(c.X, c.Y))
                continue;

            // 스킬이 "벽에 막힘"이면, 해당 셀이 벽이면 스킵
            if (skill.BlockedByWall && IsBlocked(c.X, c.Y))
                continue;

            // 한 셀에 여러 엔티티가 있을 수 있으니 전부 순회
            foreach (var target in GetEntitiesAt(new GridPos(c.X, c.Y)))
            {
                if (!target.IsAlive) continue;
                if (target.Id == actorId) continue;

                if (!skill.CanHit(attacker, target))
                    continue;

                if (!hitTargets.Add(target.Id))
                    continue;

                ApplySkillEffect(attacker, target, skill);
                anyHit = true;
            }
        }

        return anyHit;
    }
    // ==== 내부 유틸 ====
    private bool IsInside(int x, int y) => _map.InBounds(x, y);

    // 프로토 기준: walkable 아니면 "벽/장애물"로 간주
    private bool IsBlocked(int x, int y) => !_map.IsWalkable(x, y);

    private void ApplySkillEffect(MapEntity attacker, MapEntity target, SkillDef skill)
    {
        int hp = target.GetState<int>("HP");
        hp -= skill.Damage;
        target.SetState("HP", Math.Max(0, hp));

        if (hp <= 0)
        {
            //  죽으면 despawn 처리
            Despawn(target.Id);
        }
    }

    private static List<GridPos> BuildDiamond(GridPos o, int radius)
    {
        radius = radius <= 0 ? 1 : radius;
        var list = new List<GridPos>();
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                if (Math.Abs(dx) + Math.Abs(dy) <= radius)
                    list.Add(new GridPos(o.X + dx, o.Y + dy));
        return list;
    }



}

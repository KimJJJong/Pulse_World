namespace GameServer.Content.Map;

using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.Content.Map.Interface;
using GameServer.InGame.Manager.Entity;

public sealed class MapWorld2D : IGameWorld
{
    private readonly IGameBroadcaster _broadcaster;

    private readonly Map2D _map;
    public Map2D Map => _map;
    // 엔티티 전체 목록
    private readonly Dictionary<int, MapEntity> _entities = new();

    // 셀별로 어떤 엔티티가 있는지
    private readonly Dictionary<(int x, int y), HashSet<int>> _entitiesByGrid = new();

    public MapWorld2D(Map2D map, IGameBroadcaster broadcaster)
    {
        _map = map;
        _broadcaster = broadcaster;
    }

    // ==== 엔티티 관리 ====

    public bool TrySpawn(MapEntity entity, GridPos at)
    {
        if (!_map.InBounds(at.X, at.Y))
        {
            Console.WriteLine($"[ TrySpawn ] _map.InBounds(at.X, at.Y) ");
            return false;
        }
        if (!_map.IsWalkable(at.X, at.Y))
        {
            Console.WriteLine($"[ TrySpawn ] _map.IsWalkable(at.X, at.Y) ");

            return false;
        }
        if (_entities.ContainsKey(entity.Id))
        {
            Console.WriteLine($"[ TrySpawn ] _entities.ContainsKey(entity.Id) ");
            return false;
        }

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

        // grid 정리(있으면)
        var pos = e.Position;
        var key = (pos.X, pos.Y);

        if (_entitiesByGrid.TryGetValue(key, out var set))
        {
            set.Remove(entityId);
            if (set.Count == 0)
                _entitiesByGrid.Remove(key);
        }

        // 월드에서 제거
        _entities.Remove(entityId);
        Console.WriteLine("말소!!!!");
        //  중요: 이미 죽었든 말든 “항상” despawn 전파
        _broadcaster.Broadcast(new SC_EntityDespawn
        {
            BeatIndex = 0,      // Town에서 beat 넣고 싶으면 여기 말고 Session에서 보내는 구조로 바꾸는게 더 깔끔
            EntityId = entityId,
        });

        // 상태 정리(객체 참조는 남아있을 수 있으니)
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

    private const bool DBG_MOVE_FAIL = true;
    private const bool DBG_MOVE_OK = false; // 필요할 때만 true

    private void MoveLog(string msg)
    {
        if (!DBG_MOVE_FAIL && !DBG_MOVE_OK) return;
        Console.WriteLine(msg);
    }

    public bool TryMove(int actorId, GridPos target)
    {
        if (!_entities.TryGetValue(actorId, out MapEntity e))
        {
            if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL NoEntity actor={actorId} target=({target.X},{target.Y})");
            return false;
        }

        if (!e.IsAlive)
        {
            if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL Dead actor={actorId} pos=({e.Position.X},{e.Position.Y}) target=({target.X},{target.Y})");
            return false;
        }

        GridPos from = e.Position;

        // 1) 범위
        if (!_map.InBounds(target.X, target.Y))
        {
            if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL OOB actor={actorId} from=({from.X},{from.Y}) target=({target.X},{target.Y}) map=({_map.Width}x{_map.Height})");
            return false;
        }

        // 2) 통행 가능
        if (!_map.IsWalkable(target.X, target.Y))
        {
            if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL Blocked actor={actorId} from=({from.X},{from.Y}) target=({target.X},{target.Y}) tile={_map.Get(target.X, target.Y)}");
            return false;
        }

        // 3) 인접(4방)
        var dx = Math.Abs(target.X - from.X);
        var dy = Math.Abs(target.Y - from.Y);
        //if (dx + dy != 1)
        //{
        //    if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL NotAdjacent actor={actorId} from=({from.X},{from.Y}) target=({target.X},{target.Y}) dx={dx} dy={dy}");
        //    return false;
        //}

        // 4) 점유
        var targetKey = (target.X, target.Y);
        if (_entitiesByGrid.TryGetValue(targetKey, out var occ) && occ.Count > 0)
        {
            // 누가 막는지도 출력 (1칸에 1개만이라면 First만 찍어도 충분)
            int blocker = occ.First();
            //if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL Occupied actor={actorId} from=({from.X},{from.Y}) target=({target.X},{target.Y}) by={blocker} occCount={occ.Count}");
            return false;
        }

        // 5) 반영
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

        if (DBG_MOVE_OK)
            MoveLog($"[MOVE] OK actor={actorId} {from.X},{from.Y}->{target.X},{target.Y}");

        return true;
    }

    // ==== Attack ====
    public bool TryUseAttack(int actorId, /*나중에 직업별 일반공격 셋팅?*/int tartX, int tartY, List<HpUpdate> hpUpdates)
    {
        if (!_entities.TryGetValue(actorId, out var caster) || !caster.IsAlive)
            return false;


        bool anyHit = false;
        foreach(var target in GetEntitiesAt(new GridPos(tartX, tartY)))
        {
            if (!target.IsAlive) continue;
            if (target.Id == actorId) continue;

            //if (!skill.CanHit(attacker, target))  // 일반 공격 혹은 공격 모듈화 필요
            //    continue;

            //if (!hitTargets.Add(target.Id))
            //    continue;
            SkillDef tmpDamage = new SkillDef();
            tmpDamage.Damage = 10;
            ApplySkillEffect(caster, target, tmpDamage, hpUpdates);
            anyHit = true;
        }
        return anyHit;
    }


    // ==== 스킬/오브젝트 상태  ====

    public bool TryUseSkill(int actorId, string skillId, int targetX, int targetY, List<HpUpdate> hpUpdate)
    {
        if (!_entities.TryGetValue(actorId, out var caster) || !caster.IsAlive)
            return false;

        // 프로토타입: 스킬 정의 없으면 기본값
        var skill = SkillDatabase.GetOrDefault(skillId);

        var center = new GridPos(targetX, targetY);

        var cells = BuildDiamond(center, radius: 1);

        return TryUseSkillArea(actorId, skillId, cells, hpUpdate);
    }

    //  Freeze 셀 기반 판정(텔레그래프=판정 보장)
    public bool TryUseSkillArea(int actorId, string skillId, IReadOnlyList<GridPos> cells, List<HpUpdate> hpUpdates)
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

                ApplySkillEffect(attacker, target, skill, hpUpdates);
                anyHit = true;
            }
        }

        return anyHit;
    }
    // ==== 내부 유틸 ====
    private bool IsInside(int x, int y) => _map.InBounds(x, y);

    // 프로토 기준: walkable 아니면 "벽/장애물"로 간주
    private bool IsBlocked(int x, int y) => !_map.IsWalkable(x, y);

    private void ApplySkillEffect(MapEntity attacker, MapEntity target, SkillDef skill, List<HpUpdate> hpUpdates)
    {
        int before = target.GetState<int>("HP");
        int after = Math.Max(0, before - skill.Damage);

        if (after == before)
            return;

        target.SetState("HP", after);

        hpUpdates.Add(new HpUpdate(target.Id, after));

        Console.WriteLine($"[ApplySkillEffect] Attacker:{attacker.Id} Target:{target.Id} HP {before}->{after}");

        if (after <= 0)
        {
            // 죽으면 despawn
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

public readonly struct HpUpdate
{
    public readonly int EntityId;
    public readonly int NewHp;

    public HpUpdate(int entityId, int newHp)
    {
        EntityId = entityId;
        NewHp = newHp;
    }
}
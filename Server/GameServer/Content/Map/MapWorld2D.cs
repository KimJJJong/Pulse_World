namespace GameServer.Content.Map;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using GameServer.Content.Map.Interface;
using GameServer.InGame.Manager.Entity;
using GameServer.Content.Skill; // [NEW]
using GameShared.Data; // [NEW]

public sealed class MapWorld2D : IGameWorld
{
    private readonly IGameBroadcaster _broadcaster;
    public event Action<int> OnEntityDead;

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
        bool removedFromGrid = false;

        if (_entitiesByGrid.TryGetValue(key, out var set))
        {
            if (set.Remove(entityId))
            {
                removedFromGrid = true;
                if (set.Count == 0)
                    _entitiesByGrid.Remove(key);
            }
        }

        // Fast remove failed -> Scan all (Safety Fallback)
        if (!removedFromGrid)
        {
            Console.WriteLine($"[Despawn] Fast remove failed for {entityId} at ({pos.X},{pos.Y}). Scanning grid...");
            var keysToRemove = new List<(int, int)>();
            foreach (var kv in _entitiesByGrid)
            {
                if (kv.Value.Remove(entityId))
                {
                    Console.WriteLine($"[Despawn] Found & Removed {entityId} at ({kv.Key.x},{kv.Key.y})");
                    if (kv.Value.Count == 0) keysToRemove.Add(kv.Key);
                }
            }
            foreach (var k in keysToRemove) _entitiesByGrid.Remove(k);
        }

        // 월드에서 제거
        _entities.Remove(entityId);
        Console.WriteLine($"[Despawn] Entity {entityId} Removed. (Alive? {e.IsAlive})");

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

        // 3) 인접(4방) - Optional

        // 4) 점유 (Robust Logic)
        var targetKey = (target.X, target.Y);
        if (_entitiesByGrid.TryGetValue(targetKey, out var occ) && occ.Count > 0)
        {
            // 실제 존재하는 Entity인지, 살아있는지 확인 (Ghost Cleanup)
            bool actualCollision = false;
            int blockerId = -1;
            List<int> ghosts = null;

            foreach (var occupantId in occ)
            {
                if (_entities.TryGetValue(occupantId, out var occupant) && occupant.IsAlive)
                {
                    actualCollision = true;
                    blockerId = occupantId;
                    break; 
                }
                else
                {
                    // Ghost detected (not in _entities or !IsAlive)
                    if (ghosts == null) ghosts = new List<int>();
                    ghosts.Add(occupantId);
                }
            }

            // Lazy Cleanup
            if (ghosts != null)
            {
                foreach (var g in ghosts)
                {
                    occ.Remove(g);
                    Console.WriteLine($"[TryMove] Cleaned up Ghost {g} at ({target.X},{target.Y})");
                }
                if (occ.Count == 0) _entitiesByGrid.Remove(targetKey);
            }

            if (actualCollision)
            {
                //if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL Occupied actor={actorId} target=({target.X},{target.Y}) by={blockerId}");
                return false;
            }
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

        // [Refactor] Legacy SkillDef 제거 -> 직접 데미지 로직 사용
        // Basic Attack = Damage 10, Range 1 (Diamond)
        
        var center = new GridPos(tartX, tartY);
        // 타겟 위치에 있는 적들 식별
        foreach(var target in GetEntitiesAt(center))
        {
            if (!target.IsAlive) continue;
            if (target.Id == actorId) continue;

            // Apply Damage 10
            ApplyCustomDamage(caster, target, 10, hpUpdates);
        }
        
        return true; 
    }


    // ==== 스킬/오브젝트 상태  ====

    public bool TryUseSkill(int actorId, string skillId, int targetX, int targetY, List<HpUpdate> hpUpdate)
    {
        if (!_entities.TryGetValue(actorId, out var caster) || !caster.IsAlive)
            return false;

        // [Refactor] Use NewSkillDatabase
        if (!NewSkillDatabase.TryGet(skillId, out var skill))
        {
            Console.WriteLine($"[TryUseSkill] Skill not found: {skillId}");
            return false;
        }

        // Note: NewSkill uses Timeline (SkillRunner). 
        // Instant usage via TryUseSkill is not fully supported without a Runner.
        // For now, we assume this method is only called for simple/legacy cases or debugging.
        // If we want to support it, we should instantiate a SkillRunner here, 
        // BUT SkillRunner needs dependencies (actions, frozen, etc.) which MapWorld2D lacks.
        // 
        // Suggestion: Caller (BeatActionManager) should use SkillRunner directly.
        // Returning true to prevent crash, but log warning.
        
        Console.WriteLine($"[TryUseSkill] Warning: Caller should use SkillRunner for {skillId}");
        return true;
    }

    // Helper to apply raw damage (NewSkill System)
    public bool TryUseCustomSkill(int actorId, int damage, List<GridPos> cells, List<HpUpdate> hpUpdates)
    {
        if (!TryGetEntity(actorId, out var attacker) || !attacker.IsAlive)
            return false;

        bool anyHit = false;

        foreach (var c in cells)
        {
            if (!IsInside(c.X, c.Y)) continue;
            // if (IsBlocked(c.X, c.Y)) continue; // Custom skill might ignore walls or have its own flags

            foreach (var target in GetEntitiesAt(new GridPos(c.X, c.Y)))
            {
                if (!target.IsAlive) continue;
                if (target.Id == actorId) continue;

                // TODO: Friendly Fire Check (if needed)

                // Apply Damage
                int before = target.GetState<int>("HP");
                int after = Math.Max(0, before - damage);
                
                if (before != after)
                {
                    target.SetState("HP", after);
                    hpUpdates.Add(new HpUpdate(target.Id, after));
                    Console.WriteLine($"[TryUseCustomSkill] Attacker:{attacker.Id} Hit:{target.Id} DMG:{damage} HP:{before}->{after}");

                    if (after <= 0)
                    {
                        OnEntityDead?.Invoke(target.Id);
                        Despawn(target.Id);
                    }
                    anyHit = true;
                }
            }
        }
        return anyHit;
    }

    public bool TryUseSkillArea(int actorId, string skillId, IReadOnlyList<GridPos> cells, List<HpUpdate> hpUpdates)
    {
        if (!TryGetEntity(actorId, out var attacker) || !attacker.IsAlive)
            return false;

        // [Refactor] Use NewSkillDatabase
        if (!NewSkillDatabase.TryGet(skillId, out var skill))
        {
             // Log?
             return false;
        }

        // NewSkillDef doesn't have simple "Damage" field in root.
        // It has Actions -> DamageAction.
        // We need to find the damage amount if we want to apply it here.
        // Logic: Iterate tracks, find first DamageAction?
        
        int damage = 0;
        foreach(var t in skill.Tracks) {
            foreach(var e in t.Events) {
                if(e.Action is DamageAction da) {
                    damage = da.Amount;
                    break;
                }
            }
            if(damage > 0) break;
        }

        var hitTargets = new HashSet<int>();
        bool anyHit = false;

        foreach (var c in cells)
        {
            if (!IsInside(c.X, c.Y))
                continue;

            // BlockedByWall is not directly on NewSkillDef. 
            // Assume false or check generic flags if added.

            foreach (var target in GetEntitiesAt(new GridPos(c.X, c.Y)))
            {
                if (!target.IsAlive) continue;
                if (target.Id == actorId) continue;

                if (!hitTargets.Add(target.Id))
                    continue;

                ApplyCustomDamage(attacker, target, damage, hpUpdates);
                anyHit = true;
            }
        }

        return anyHit;
    }

    public bool TryUseCustomSkill(int actorId, int damage, IReadOnlyList<GridPos> cells, List<HpUpdate> hpUpdates)
    {
        if (!_entities.TryGetValue(actorId, out var attacker)) return false; 
        // Or if actorId is invalid, we might still process? usually need attacker info for hit check?
        // Pattern damage usually hits Players. 
        // If attacker is null (e.g. environment), we might skip alliance check?
        // Let's assume attacker exists.

        bool anyHit = false;
        HashSet<int> hitTargets = new();

        foreach (var c in cells)
        {
            if (!IsInside(c.X, c.Y)) continue;

            foreach (var target in GetEntitiesAt(new GridPos(c.X, c.Y)))
            {
                if (!target.IsAlive) continue;
                if (target.Id == actorId) continue; // Self-hit check
                
                // Alliance Check? 
                // CustomSkill is usually Monster -> Player.
                if (attacker != null)
                {
                    // Simple check: Monster shouldn't hit Monster, Player shouldn't hit Player
                    if (attacker.Type == target.Type) continue; 
                }

                if (!hitTargets.Add(target.Id)) continue;

                ApplyCustomDamage(attacker, target, damage, hpUpdates);
                anyHit = true;
            }
        }
        return anyHit;
    }

    // ==== 내부 유틸 ====
    private bool IsInside(int x, int y) => _map.InBounds(x, y);

    // 프로토 기준: walkable 아니면 "벽/장애물"로 간주
    private bool IsBlocked(int x, int y) => !_map.IsWalkable(x, y);

    // [REMOVED] ApplySkillEffect (Legacy)


    private void ApplyCustomDamage(MapEntity attacker, MapEntity target, int damage, List<HpUpdate> hpUpdates)
    {
        int before = target.GetState<int>("HP");
        int after = Math.Max(0, before - damage);

        if (after == before)
            return;

        target.SetState("HP", after);

        hpUpdates.Add(new HpUpdate(target.Id, after));

        Console.WriteLine($"[ApplyDamage] Attacker:{attacker?.Id??-1} Target:{target.Id} HP {before}->{after}");

        if (after <= 0)
        {
            // 죽으면 event -> despawn
            OnEntityDead?.Invoke(target.Id);
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
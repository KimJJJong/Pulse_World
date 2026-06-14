namespace GameServer.Content.Map;

using System;
using System.Collections.Generic;
using GameServer.Content.Map.Interface;
using GameServer.InGame.Manager.Entity;

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
        if (_entities.ContainsKey(entity.Id))
        {
            Console.WriteLine($"[ TrySpawn ] _entities.ContainsKey(entity.Id) ");
            return false;
        }

        var cells = BuildFootprintCells(entity, at);
        foreach (var cell in cells)
        {
            if (!_map.InBounds(cell.X, cell.Y))
            {
                Console.WriteLine($"[ TrySpawn ] _map.InBounds({cell.X}, {cell.Y}) ");
                return false;
            }

            if (!_map.IsWalkable(cell.X, cell.Y))
            {
                Console.WriteLine($"[ TrySpawn ] _map.IsWalkable({cell.X}, {cell.Y}) ");
                return false;
            }

            if (HasLiveOccupant(cell.X, cell.Y, entity.Id))
            {
                Console.WriteLine($"[ TrySpawn ] occupied ({cell.X}, {cell.Y}) ");
                return false;
            }
        }

        entity.Position = at;
        _entities[entity.Id] = entity;

        foreach (var cell in cells)
        {
            var key = (cell.X, cell.Y);
            if (!_entitiesByGrid.TryGetValue(key, out var set))
            {
                set = new HashSet<int>();
                _entitiesByGrid[key] = set;
            }
            set.Add(entity.Id);
        }
        return true;
    }

    public bool Despawn(int entityId)
    {
        if (!_entities.TryGetValue(entityId, out var e))
            return false;

        bool removedFromGrid = false;

        foreach (var cell in BuildFootprintCells(e, e.Position))
        {
            var key = (cell.X, cell.Y);
            if (_entitiesByGrid.TryGetValue(key, out var set) && set.Remove(entityId))
            {
                removedFromGrid = true;
                if (set.Count == 0) _entitiesByGrid.Remove(key);
            }
        }

        // Fast remove failed -> Scan all (Safety Fallback)
        if (!removedFromGrid)
        {
            Console.WriteLine($"[Despawn] Fast remove failed for {entityId} at ({e.Position.X},{e.Position.Y}). Scanning grid...");
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

    private bool PreviewDash(GridPos from, int dirX, int dirY, int distance, out GridPos landedPos)
    {
        landedPos = from;

        GridPos best = from;
        for (int step = 1; step <= distance; step++)
        {
            int nx = from.X + dirX * step;
            int ny = from.Y + dirY * step;

            if (!_map.InBounds(nx, ny) || !_map.IsWalkable(nx, ny))
                break;

            best = new GridPos(nx, ny);
        }

        if (best.X == from.X && best.Y == from.Y)
            return false;

        landedPos = best;
        return true;
    }

    private bool PreviewBlink(GridPos from, int dirX, int dirY, int distance, out GridPos landedPos)
    {
        int tx = from.X + dirX * distance;
        int ty = from.Y + dirY * distance;

        landedPos = from;
        if (!_map.InBounds(tx, ty) || !_map.IsWalkable(tx, ty))
            return false;

        landedPos = new GridPos(tx, ty);
        return true;
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
    private const bool DBG_MOVE_OK = false;

    private void MoveLog(string msg)
    {
        if (!DBG_MOVE_FAIL && !DBG_MOVE_OK) return;
        Console.WriteLine(msg);
    }

    /// <summary>
    /// Dash: dirX/dirY 방향으로 distance 칸 이동.
    /// 도중에 벽/맵내 수없으면 바로 앞 칸(Walkable이었던 마지막 지점)까지만 이동.
    /// </summary>
    public bool TryDash(int actorId, int dirX, int dirY, int distance, out GridPos landedPos)
    {
        landedPos = default;
        if (!_entities.TryGetValue(actorId, out var e) || !e.IsAlive) return false;

        GridPos from = e.Position;
        if (!PreviewDash(from, dirX, dirY, distance, out var preview))
        {
            Console.WriteLine($"[Dash] Failed: Actor={actorId} dir=({dirX},{dirY}) dist={distance} — no room");
            return false;
        }

        // 목표지점으로 이동 (TryMove 개철 기존 Occupancy 체크 가능)
        bool moved = TryMove(actorId, preview);
        landedPos = moved ? preview : from;
        Console.WriteLine($"[Dash] Actor={actorId} {from.X},{from.Y}->{landedPos.X},{landedPos.Y} moved={moved}");
        return moved;
    }

    /// <summary>
    /// Blink: dirX/dirY 방향으로 distance 칸 지점이 Walkable이면 순간이동.
    /// 목표지점이 몉 / 차지 중이면 실패 (StopOnObstacle=false 시).
    /// </summary>
    public bool TryBlink(int actorId, int dirX, int dirY, int distance, out GridPos landedPos)
    {
        landedPos = default;
        if (!_entities.TryGetValue(actorId, out var e) || !e.IsAlive) return false;

        GridPos from = e.Position;
        if (!PreviewBlink(from, dirX, dirY, distance, out var target))
        {
            Console.WriteLine($"[Blink] Failed: Actor={actorId} target=({from.X + dirX * distance},{from.Y + dirY * distance}) — blocked or OOB");
            return false;
        }

        bool moved = TryMove(actorId, target);
        landedPos = moved ? target : from;
        Console.WriteLine($"[Blink] Actor={actorId} {from.X},{from.Y}->{landedPos.X},{landedPos.Y} moved={moved}");
        return moved;
    }

    public bool TryPreviewDash(GridPos from, int dirX, int dirY, int distance, out GridPos landedPos)
        => PreviewDash(from, dirX, dirY, distance, out landedPos);

    public bool TryPreviewBlink(GridPos from, int dirX, int dirY, int distance, out GridPos landedPos)
        => PreviewBlink(from, dirX, dirY, distance, out landedPos);

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

        var targetCells = BuildFootprintCells(e, target);
        foreach (var cell in targetCells)
        {
            if (!_map.InBounds(cell.X, cell.Y))
            {
                if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL OOB actor={actorId} from=({from.X},{from.Y}) target=({target.X},{target.Y}) footprint=({cell.X},{cell.Y}) map=({_map.Width}x{_map.Height})");
                return false;
            }

            if (!_map.IsWalkable(cell.X, cell.Y))
            {
                if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL Blocked actor={actorId} from=({from.X},{from.Y}) target=({target.X},{target.Y}) footprint=({cell.X},{cell.Y}) tile={_map.Get(cell.X, cell.Y)}");
                return false;
            }

            var targetKey = (cell.X, cell.Y);
            if (_entitiesByGrid.TryGetValue(targetKey, out var occ) && occ.Count > 0)
            {
                bool actualCollision = false;
                int blockerId = -1;
                List<int> ghosts = null;

                foreach (var occupantId in occ)
                {
                    if (occupantId == actorId)
                        continue;

                    if (_entities.TryGetValue(occupantId, out var occupant) && occupant.IsAlive)
                    {
                        actualCollision = true;
                        blockerId = occupantId;
                        break;
                    }

                    if (ghosts == null) ghosts = new List<int>();
                    ghosts.Add(occupantId);
                }

                if (ghosts != null)
                {
                    foreach (var g in ghosts)
                    {
                        occ.Remove(g);
                        Console.WriteLine($"[TryMove] Cleaned up Ghost {g} at ({cell.X},{cell.Y})");
                    }
                    if (occ.Count == 0) _entitiesByGrid.Remove(targetKey);
                }

                if (actualCollision)
                {
                    if (DBG_MOVE_FAIL) MoveLog($"[MOVE] FAIL Occupied actor={actorId} target=({cell.X},{cell.Y}) by={blockerId}");
                    return false;
                }
            }
        }

        // 5) 반영
        foreach (var cell in BuildFootprintCells(e, from))
        {
            var fromKey = (cell.X, cell.Y);
            if (_entitiesByGrid.TryGetValue(fromKey, out var fromSet))
            {
                fromSet.Remove(actorId);
                if (fromSet.Count == 0) _entitiesByGrid.Remove(fromKey);
            }
        }

        foreach (var cell in targetCells)
        {
            var targetKey = (cell.X, cell.Y);
            if (!_entitiesByGrid.TryGetValue(targetKey, out var toSet))
            {
                toSet = new HashSet<int>();
                _entitiesByGrid[targetKey] = toSet;
            }
            toSet.Add(actorId);
        }

        e.Position = target;

        if (DBG_MOVE_OK)
            MoveLog($"[MOVE] OK actor={actorId} {from.X},{from.Y}->{target.X},{target.Y}");

        return true;
    }

    // ==== 스킬/오브젝트 상태 ====
    // Skill 해석은 SkillRunner가 담당하고, 월드는 확정된 DamageAction 결과만 반영한다.
    public bool TryUseCustomSkill(int actorId, long currentTick, FrozenAttackRegistry.FrozenAttack frozen, List<HpUpdate> hpUpdates)
    {
        if (!TryGetEntity(actorId, out var attacker) || !attacker.IsAlive)
            return false;

        bool anyHit = false;

        foreach (var c in frozen.Cells)
        {
            if (!IsInside(c.X, c.Y)) continue;
            // if (IsBlocked(c.X, c.Y)) continue; // Custom skill might ignore walls or have its own flags

            foreach (var target in GetEntitiesAt(new GridPos(c.X, c.Y)))
            {
                if (target == null || !target.IsAlive) continue;
                if (attacker.Id == target.Id) continue;

                // Faction Logic: DamageAction의 HitPlayers/HitMonsters 플래그 적용
                bool isTargetPlayer = target.Type == EntityType.Player || target.Id < 100;
                bool isTargetMonster = target.Type == EntityType.Monster || (target.Id >= 100 && target.Id < 1000);

                if (isTargetPlayer && !frozen.HitPlayers) continue;
                if (isTargetMonster && !frozen.HitMonsters) continue;

                // Apply Stun First

                // Apply Stun First
                if (frozen.StunDurationTicks > 0)
                {
                    target.StunEndTick = currentTick + frozen.StunDurationTicks;
                    Console.WriteLine($"[CustomSkill] Target:{target.Id} Stunned until {target.StunEndTick}");
                }

                // Apply Damage
                int baseDamage = Math.Max(0, frozen.CustomDamage ?? 0);
                int attackerAtk = Math.Max(0, attacker.GetState<int>("ATK"));
                int targetDef = Math.Max(0, target.GetState<int>("DEF"));
                int damage = Math.Max(1, baseDamage + attackerAtk - targetDef);
                int before = target.GetState<int>("HP");
                int after = Math.Max(0, before - damage);
                
                if (before != after)
                {
                    target.SetState("HP", after);
                    hpUpdates.Add(new HpUpdate(target.Id, after));
                    
                    // [Sync_Log] 서버 시점의 데미지 발생 타이밍 (틱 및 환산 비트)
                    long serverBeat = currentTick / 480;
                    Console.WriteLine($"[Damage_Sync] Tick:{currentTick} (approx Beat:{serverBeat}) Attacker:{attacker.Id} Hit:{target.Id} DMG:{damage} HP:{before}->{after}");

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

    // ==== 내부 유틸 ====
    private bool IsInside(int x, int y) => _map.InBounds(x, y);

    private static List<GridPos> BuildFootprintCells(MapEntity entity, GridPos origin)
    {
        int sizeX = Math.Max(1, entity.GetState<int>("SizeX"));
        int sizeY = Math.Max(1, entity.GetState<int>("SizeY"));
        var cells = new List<GridPos>(sizeX * sizeY);
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
                cells.Add(new GridPos(origin.X + x, origin.Y + y));
        }

        return cells;
    }

    private bool HasLiveOccupant(int x, int y, int ignoreEntityId)
    {
        var key = (x, y);
        if (!_entitiesByGrid.TryGetValue(key, out var set))
            return false;

        foreach (int entityId in set)
        {
            if (entityId == ignoreEntityId)
                continue;

            if (_entities.TryGetValue(entityId, out var entity) && entity.IsAlive)
                return true;
        }

        return false;
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

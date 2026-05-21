using GameServer.Content.Map;
using GameServer.Content.Map.Interface;
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using System;
using System.Collections.Generic;

public sealed class PatternRunner
{
    private readonly IGameWorld _world;
    private readonly BeatActionManager _actions;
    private readonly TelegraphScheduler _telegraph;
    private readonly MonsterPatternSet _patterns;
    private readonly FrozenAttackRegistry _frozen;
    private readonly Map2D _map2d;

    private readonly Dictionary<int, RuntimeState> _rt = new();
    private readonly Random _rng = new();

    public PatternRunner(IGameWorld world, BeatActionManager actions, TelegraphScheduler telegraph, MonsterPatternSet patterns, FrozenAttackRegistry frozen, Map2D map2D)
    {
        _world = world;
        _actions = actions;
        _telegraph = telegraph;
        _patterns = patterns;
        _frozen = frozen;
        _map2d = map2D;
    }

    public void InitMonster(int monsterId)
    {
        if (!_rt.ContainsKey(monsterId))
            _rt[monsterId] = new RuntimeState();
    }

    public void RemoveMonster(int monsterId) => _rt.Remove(monsterId);

    public void Run(long beatIndex, MapEntity monster, string monsterType, IList<MapEntity> players)
    {
        if (players.Count == 0) return;
        if (!_rt.TryGetValue(monster.Id, out var st))
        {
            Console.WriteLine($"[PatternRunner] no runtime state for monsterId={monster.Id}");
            return;
        }

        // LockedUntilBeat 정의:
        // - "이 비트 미만에서는 패턴 선택/스케줄링을 금지" (beatIndex < LockedUntilBeat이면 return)
        // - 즉 LockedUntilBeat == 현재 beat 이면 '이제 패턴 실행 가능' 상태

        if (beatIndex >= st.LockedUntilBeat)
        {
            bool isStunned = monster.IsStunned(beatIndex * 480);
            if (!isStunned)
            {
                MonsterPatternDef def = _patterns.GetMonster(monsterType);
                if (def == null)
                    def = _patterns.GetMonster("Default");

                if (def != null)
                {
                    ApplyPhaseTransitions(def, monster, st, beatIndex);

                    PhaseDef phase = def.GetPhase(st.PhaseId) ?? def.GetPhase(def.DefaultPhase);
                    if (phase != null && phase.Selectors.Count > 0)
                    {
                        List<SelectorDef> candidates = new List<SelectorDef>(8);
                        foreach (var sel in phase.Selectors)
                        {
                            if (IsInCooldown(st, sel.Id, beatIndex)) continue;
                            if (!EvaluateWhen(sel.When, monster, players)) continue;
                            candidates.Add(sel);
                        }

                        if (candidates.Count > 0)
                        {
                            SelectorDef picked = WeightedPick(candidates);

                            long locked = ScheduleTimeline(beatIndex, monster, players, picked);
                            st.LockedUntilBeat = Math.Max(st.LockedUntilBeat, locked);

                            if (picked.CooldownBeats > 0)
                                st.Cooldowns[picked.Id] = beatIndex + picked.CooldownBeats;
                        }
                    }
                }
            }
        }
    }

    public void UpdateTick(long currentTick)
    {
        foreach (var kvp in _rt)
        {
            var st = kvp.Value;
            var monsterId = kvp.Key;

            bool isStunned = _world.TryGetEntity(monsterId, out var entity) && entity.IsStunned(currentTick);

            if (isStunned)
            {
                if (st.ActiveSkills.Count > 0)
                {
                    Console.WriteLine($"[PatternRunner] Monster {monsterId} Stunned! Canceling {st.ActiveSkills.Count} active skills.");
                    st.ActiveSkills.Clear();
                    _actions.BroadcastCancelAction(monsterId);
                    _telegraph.RemoveByCaster(monsterId);
                    _frozen.RemoveByActor(monsterId);
                }
                continue;
            }

            for (int i = st.ActiveSkills.Count - 1; i >= 0; i--)
            {
                var skill = st.ActiveSkills[i];
                skill.UpdateTick(currentTick);
                if (!skill.IsRunning)
                    st.ActiveSkills.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// ScheduleTimeline 반환값(last):
    /// 이번에 예약한 액션들 중 "가장 마지막 executeBeat"를 반환한다.
    /// 이 값을 LockedUntilBeat로 사용해 패턴이 끝나기 전 다른 패턴이 겹쳐 예약되지 않게 막는다.
    /// </summary>
    private long ScheduleTimeline(long baseBeat, MapEntity m, IList<MapEntity> players, SelectorDef sel)
    {
        long last = baseBeat;
        bool needsPadding = false;

        GridPos plannedPos = m.Position;
        foreach (var act in sel.Timeline)
        {
            long executeBeat = baseBeat + act.AtBeatOffset;
            last = Math.Max(last, executeBeat);

            switch (act.Type)
            {
                case ActionType.Wait:
                    last = Math.Max(last, executeBeat + 1);
                    break;

                case ActionType.MoveStepToward:
                    {
                        last = Math.Max(last, executeBeat + 1);
                        MapEntity target = FindTarget(m, players, act.Target);
                        if (target == null) break;

                        var nextPos = GetPathPosition(plannedPos, target.Position, act.MoveDistance);
                        if (!_map2d.IsWalkable(nextPos.X, nextPos.Y))
                            nextPos = plannedPos;

                        var cmd = new PlayerActionCmd
                        {
                            ActorId = m.Id,
                            Kind = ActionKind.Move,
                            TargetCell = nextPos,
                            Rotation = CalculateRotation(plannedPos, nextPos, m.Rotation),
                            ClientSendTimeMs = 0,
                            ServerReceiveTimeMs = 0
                        };
                        _actions.ScheduleServerCommand(executeBeat, cmd);
                        plannedPos = nextPos;
                        m.Rotation = cmd.Rotation; // Immediate update for next actions in timeline
                        break;
                    }

                case ActionType.Attack:
                    ProcessSkillAction(m, act, executeBeat, ref last);
                    break;

                case ActionType.CastSkill:
                    ProcessSkillAction(m, act, executeBeat, ref last);
                    break;

                case ActionType.Move:
                    {
                        last = Math.Max(last, executeBeat + 1);

                        // ─────────────────────────────────────────────────────────
                        // [방향 기반 이동] MoveDirection != None 이면 방향 우선 적용.
                        // 절대 위치 예약이 아닌 "현재 위치 기준 방향 + 1칸씩" 처리이므로
                        // 순간이동 · 대각선이동 · 2칸 점프가 발생하지 않는다.
                        // MoveDistance > 1 이면 한 Beat 내에서 step별로 벽 체크하며 이동.
                        // 매 Beat 1칸씩 이동하게 하려면 MoveDistance=1 + AtBeatOffset 증분 사용.
                        // ─────────────────────────────────────────────────────────
                        GridPos targetPos = act.MoveDirection != MoveDirection.None
                            ? ResolveMoveDirection(plannedPos, m, players, act)
                            : ResolveMoveStrategy(plannedPos, m, players, act);

                        // 최종 위치 걷기 가능 여부 보정 (ResolveMoveDirection 내부에서도 체크하지만 안전망)
                        if (!_map2d.IsWalkable(targetPos.X, targetPos.Y))
                            targetPos = plannedPos;

                        var cmd = new PlayerActionCmd
                        {
                            ActorId = m.Id,
                            Kind = ActionKind.Move,
                            TargetCell = targetPos,
                            Rotation = CalculateRotation(plannedPos, targetPos, m.Rotation),
                            ClientSendTimeMs = 0,
                            ServerReceiveTimeMs = 0
                        };
                        _actions.ScheduleServerCommand(executeBeat, cmd);
                        plannedPos = targetPos;
                        m.Rotation = cmd.Rotation;
                        break;
                    }
            }
        }

        return needsPadding ? (last + 1) : last;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  [방향 기반 이동] ResolveMoveDirection
    //  MoveDistance칸을 1칸씩 순차 처리한다.
    //  각 step마다 벽 체크 → 막히면 그 자리에서 정지(안전한 위치 보장).
    //  TowardTarget / AwayFromTarget은 step마다 타겟 위치를 재계산한다.
    // ─────────────────────────────────────────────────────────────────────────
    private GridPos ResolveMoveDirection(GridPos from, MapEntity m, IList<MapEntity> players, ActionDef act)
    {
        GridPos pos = from;
        int steps = Math.Max(1, act.MoveDistance);

        for (int step = 0; step < steps; step++)
        {
            GridPos next;
            switch (act.MoveDirection)
            {
                case MoveDirection.Up:
                    next = new GridPos(pos.X, pos.Y + 1);
                    break;
                case MoveDirection.Down:
                    next = new GridPos(pos.X, pos.Y - 1);
                    break;
                case MoveDirection.Left:
                    next = new GridPos(pos.X - 1, pos.Y);
                    break;
                case MoveDirection.Right:
                    next = new GridPos(pos.X + 1, pos.Y);
                    break;
                case MoveDirection.TowardTarget:
                    {
                        var target = FindTarget(m, players, act.Target);
                        next = target != null ? StepTowards(pos, target.Position) : pos;
                        break;
                    }
                case MoveDirection.AwayFromTarget:
                    {
                        var target = FindTarget(m, players, act.Target);
                        next = target != null ? StepAway(pos, target.Position) : pos;
                        break;
                    }
                default:
                    next = pos;
                    break;
            }

            // 벽이면 해당 step에서 정지 (이후 step도 진행 불가)
            if (!_map2d.IsWalkable(next.X, next.Y))
            {
                Console.WriteLine($"[PatternRunner.ResolveMoveDirection] Blocked at step {step+1}/{steps}, pos=({next.X},{next.Y}). Stopping.");
                break;
            }
            pos = next;
        }

        return pos;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  헬퍼: 타겟 반대 방향으로 1칸 이동 (Manhattan 우선)
    // ─────────────────────────────────────────────────────────────────────────
    private GridPos StepAway(GridPos from, GridPos threat)
    {
        int dx = from.X - threat.X;
        int dy = from.Y - threat.Y;

        // 같은 위치면 오른쪽으로 도망
        if (dx == 0 && dy == 0)
            return new GridPos(from.X + 1, from.Y);

        // X거리가 Y거리 이상이면 X축으로 도망 (StepTowards와 동일한 축 우선 기준)
        if (Math.Abs(dx) >= Math.Abs(dy))
            return new GridPos(from.X + Math.Sign(dx), from.Y);
        else
            return new GridPos(from.X, from.Y + Math.Sign(dy));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  기존 Strategy 방식 (Legacy Fallback — MoveDirection == None 일 때)
    // ─────────────────────────────────────────────────────────────────────────
    private GridPos ResolveMoveStrategy(GridPos currentPos, MapEntity m, IList<MapEntity> players, ActionDef act)
    {
        int dist = act.MoveDistance;
        if (dist <= 0) return currentPos;

        switch (act.MoveStrategy)
        {
            case MoveStrategy.Random:
                {
                    int[] dx = { 0, 0, 1, -1 };
                    int[] dy = { 1, -1, 0, 0 };
                    List<GridPos> valids = new();
                    for (int i = 0; i < 4; i++)
                    {
                        var cand = new GridPos(currentPos.X + dx[i] * dist, currentPos.Y + dy[i] * dist);
                        if (_map2d.IsWalkable(cand.X, cand.Y)) valids.Add(cand);
                    }
                    if (valids.Count > 0) return valids[_rng.Next(valids.Count)];
                    return currentPos;
                }
            case MoveStrategy.Flee:
                {
                    var target = FindTarget(m, players, act.Target);
                    if (target == null) return currentPos;
                    int dx = currentPos.X - target.Position.X;
                    int dy = currentPos.Y - target.Position.Y;
                    int sx = 0, sy = 0;
                    if (Math.Abs(dx) >= Math.Abs(dy)) sx = Math.Sign(dx);
                    else sy = Math.Sign(dy);
                    if (sx == 0 && sy == 0) sx = 1;
                    return new GridPos(currentPos.X + sx * dist, currentPos.Y + sy * dist);
                }
            case MoveStrategy.Forward:
                {
                    var target = FindClosestPlayer(m, players, out _);
                    if (target == null) return currentPos;
                    return GetPathPosition(currentPos, target.Position, dist);
                }
            case MoveStrategy.Backward:
                {
                    var target = FindClosestPlayer(m, players, out _);
                    if (target == null) return currentPos;
                    int dx = currentPos.X - target.Position.X;
                    int dy = currentPos.Y - target.Position.Y;
                    int sx = 0, sy = 0;
                    if (Math.Abs(dx) >= Math.Abs(dy)) sx = Math.Sign(dx);
                    else sy = Math.Sign(dy);
                    if (sx == 0 && sy == 0) sx = -1;
                    return new GridPos(currentPos.X + sx * dist, currentPos.Y + sy * dist);
                }
            default:
                return currentPos;
        }
    }

    private bool EvaluateWhen(WhenGroup when, MapEntity m, IList<MapEntity> players)
    {
        foreach (var c in when.All)
            if (!EvalOne(c, m, players)) return false;
        return true;
    }

    private bool EvalOne(ConditionDef c, MapEntity m, IList<MapEntity> players)
    {
        var target = FindClosestPlayer(m, players, out int dist);
        if (target == null) return false;

        return c.Type switch
        {
            ConditionType.DistanceToClosestPlayerLE => dist <= c.Value,
            ConditionType.DistanceToClosestPlayerGT => dist > c.Value,
            _ => true
        };
    }

    private MapEntity FindTarget(MapEntity self, IList<MapEntity> players, TargetDef target)
    {
        MapEntity best = null;
        int bestDist = int.MaxValue;

        List<MapEntity> candidates = new();
        foreach (var p in players)
        {
            if (target.RequireAlive && !p.IsAlive) continue;
            int dist = Math.Abs(p.Position.X - self.Position.X)
                     + Math.Abs(p.Position.Y - self.Position.Y);
            if (dist > target.MaxRange) continue;
            candidates.Add(p);
        }

        if (candidates.Count == 0) return null;

        switch (target.Type)
        {
            case TargetType.ClosestPlayer:
                foreach (var p in candidates)
                {
                    int d = Math.Abs(p.Position.X - self.Position.X)
                          + Math.Abs(p.Position.Y - self.Position.Y);
                    if (d < bestDist) { bestDist = d; best = p; }
                }
                return best;

            case TargetType.LowestHpPlayer:
                int minHp = int.MaxValue;
                foreach (var p in candidates)
                {
                    int hp = p.GetState<int>("HP");
                    if (hp < minHp) { minHp = hp; best = p; }
                }
                return best;

            case TargetType.RandomPlayer:
                return candidates[_rng.Next(candidates.Count)];

            default:
                return candidates[0];
        }
    }

    private GridPos ResolveOriginPoint(MapEntity self, MapEntity target, AreaDef area)
    {
        return area.OriginType switch
        {
            TelegraphOriginType.Self   => self.Position,
            TelegraphOriginType.Target => target.Position,
            TelegraphOriginType.Point  => new GridPos(area.OriginX, area.OriginY),
            _ => self.Position
        };
    }

    private SC_BeatTelegraphs.Telegraphs BuildTelegraphEntry(
        int casterId, byte styleId, int durationTicks, List<GridPos> frozenCells)
    {
        var tg = new SC_BeatTelegraphs.Telegraphs
        {
            CasterId     = casterId,
            StyleId      = styleId,
            DurationTicks = durationTicks,
            Shape        = (byte)TelegraphShape.Cells,
            OriginType   = (byte)TelegraphOriginType.Point,
            OriginX      = 0,
            OriginY      = 0,
            ParamA       = 0,
            ParamB       = 0
        };
        tg.cellss.Clear();
        foreach (var c in frozenCells)
            tg.cellss.Add(new SC_BeatTelegraphs.Telegraphs.Cells { X = c.X, Y = c.Y });
        return tg;
    }

    private bool IsInCooldown(RuntimeState st, string selectorId, long nowBeat)
        => st.Cooldowns.TryGetValue(selectorId, out var nextBeat) && nowBeat < nextBeat;

    private SelectorDef WeightedPick(List<SelectorDef> list)
    {
        int total = 0;
        foreach (var s in list) total += Math.Max(1, s.Weight);
        int r = _rng.Next(0, total);
        foreach (var s in list)
        {
            r -= Math.Max(1, s.Weight);
            if (r < 0) return s;
        }
        return list[0];
    }

    private MapEntity? FindClosestPlayer(MapEntity m, IList<MapEntity> players, out int dist)
    {
        MapEntity? best = null;
        dist = int.MaxValue;
        foreach (var p in players)
        {
            if (!p.IsAlive) continue;
            int d = Math.Abs(p.Position.X - m.Position.X) + Math.Abs(p.Position.Y - m.Position.Y);
            if (d < dist) { dist = d; best = p; }
        }
        return best;
    }

    private GridPos GetPathPosition(GridPos from, GridPos to, int steps)
    {
        GridPos curr = from;
        for (int i = 0; i < steps; i++)
        {
            if (curr.X == to.X && curr.Y == to.Y) break;
            curr = StepTowards(curr, to);
        }
        return curr;
    }

    private GridPos StepTowards(GridPos from, GridPos to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        // X거리가 Y거리보다 크면 X축으로 1칸 — 대각선 이동 원천 불가
        if (Math.Abs(dx) > Math.Abs(dy))
            return new GridPos(from.X + Math.Sign(dx), from.Y);
        else if (dy != 0)
            return new GridPos(from.X, from.Y + Math.Sign(dy));
        else
            return from;
    }

    private void ApplyPhaseTransitions(MonsterPatternDef def, MapEntity monster, RuntimeState st, long beatIndex)
    {
        if (def.Transitions == null || def.Transitions.Count == 0) return;

        foreach (var tr in def.Transitions)
        {
            if (tr.FromPhaseId != st.PhaseId) continue;

            bool passed = tr.Type switch
            {
                PhaseTransitionType.HpPercentLE =>
                    monster.GetState<int>("HP") * 100 <= monster.GetState<int>("MaxHP") * tr.Value,
                PhaseTransitionType.TimeSinceSpawnBeatsGE =>
                    beatIndex - monster.GetState<long>("SpawnBeat") >= tr.Value,
                _ => false
            };

            if (!passed) continue;

            st.PhaseId = tr.ToPhaseId;
            st.LockedUntilBeat = -1;
            st.Cooldowns.Clear();
            return; // 한 beat에 하나만 전환
        }
    }

    private void ProcessSkillAction(MapEntity m, ActionDef act, long executeBeat, ref long last)
    {
        if (!GameServer.Content.Skill.NewSkillDatabase.TryGet(act.SkillId, out var skillDef))
            return;

        // 스킬 시전 시 타겟이 있다면 해당 방향으로 회전 업데이트 (옵션)
        // 여기서는 일단 현재 몬스터의 Rotation을 그대로 사용하거나, 
        // 필요 시 타겟 방향으로 미리 돌려주는 로직을 넣을 수 있음.
        
        var runner = new GameServer.Content.Skill.SkillRunner(m.Id, _world, _actions, _frozen, _telegraph);
        runner.StartSkillTick(skillDef, executeBeat * 480, m.Rotation);

        if (_rt.TryGetValue(m.Id, out var rt))
            rt.ActiveSkills.Add(runner);

        long endBeat = executeBeat + Math.Max(1, (skillDef.TotalDurationTicks + 479) / 480);
        last = Math.Max(last, endBeat);
    }

    private float CalculateRotation(GridPos from, GridPos to, float current)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        if (dx == 0 && dy == 0) return current;

        // 0:북(Y+), 90:동(X+), 180:남(Y-), 270:서(X-)
        if (dy > 0) return 0f;   // Up
        if (dy < 0) return 180f; // Down
        if (dx > 0) return 90f;  // Right
        if (dx < 0) return 270f; // Left

        return current;
    }

    private sealed class RuntimeState
    {
        public string PhaseId = "P1";
        public long LockedUntilBeat = -1;
        public Dictionary<string, long> Cooldowns = new();
        public List<GameServer.Content.Skill.SkillRunner> ActiveSkills = new();
    }
}

using GameServer.Content.Map;
using GameServer.Content.Map.Interface;
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using System;
using System.Collections.Generic;
using System.Linq;

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
        
        // [Refactor] Pattern Selection Logic comes FIRST
        // This allows newly added skills to be updated in the SAME beat loop below.
        
        if (beatIndex >= st.LockedUntilBeat)
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

        // 2. Active Skills Update (매 비트 실행)
        // Now includes any skills just added above.
        for (int i = st.ActiveSkills.Count - 1; i >= 0; i--)
        {
            var skill = st.ActiveSkills[i];
            skill.Update((int)beatIndex);
            if (!skill.IsRunning)
            {
                st.ActiveSkills.RemoveAt(i);
            }
        }
    }
    /// <summary>
    /// // ScheduleTimeline 반환값(last):
    // - 이번에 예약한 액션들 중 "가장 마지막 executeBeat"를 반환한다.
    // - 이 값을 LockedUntilBeat로 사용해 패턴이 끝나기 전 다른 패턴이 겹쳐 예약되지 않게 막는다.
    /// </summary>
    /// <param name="baseBeat"></param>
    /// <param name="m"></param>
    /// <param name="players"></param>
    /// <param name="sel"></param>
    /// <returns></returns>
    private long ScheduleTimeline(long baseBeat, MapEntity m, IList<MapEntity> players, SelectorDef sel)
    {
        long last = baseBeat;
        bool needsPadding = false;

        //Console.WriteLine($"[ScheduleTimeLine] TimeLineCount :{sel.Timeline.Count} =========================");

        GridPos plannedPos = m.Position;
        foreach (var act in sel.Timeline)
        {
            long executeBeat = baseBeat + act.AtBeatOffset;

            //if (executeBeat <= baseBeat) executeBeat = baseBeat + 1;

            last = Math.Max(last, executeBeat);

            switch (act.Type)
            {
                case ActionType.Wait:
                    last = Math.Max(last, executeBeat + 1); // Consumes 1 beat
                    break;

                case ActionType.MoveStepToward:
                    {
                        last = Math.Max(last, executeBeat + 1); // Consumes 1 beat
                        MapEntity target = FindTarget(m, players, act.Target);
                        if (target == null)
                        {
                            //Console.WriteLine("[ScheduleTimeline : MoveStepToward ] (target == null)");
                            break;
                        }
                        var nextPos = GetPathPosition(plannedPos, target.Position, act.MoveDistance);

                        if(! _map2d.IsWalkable(nextPos.X, nextPos.Y) )
                        {
                            // If target is blocked, maybe try 1 step? Or just stay.
                            // For now, let's try to find a walkable spot on the path if the end is blocked?
                            // Or simplistically:
                            nextPos = plannedPos;
                        }
                        var cmd = new PlayerActionCmd
                        {
                            ActorId = m.Id,
                            Kind = ActionKind.Move,
                            TargetCell = nextPos,
                            ClientSendTimeMs = 0,
                            ServerReceiveTimeMs = 0
                        };
                        _actions.ScheduleServerCommand(executeBeat, cmd);
                        //Console.WriteLine($"[ScheduleTimeLine] BeatIndex :{executeBeat} || Action : {ActionKind.Move} ||Pos :{nextPos}");
                        plannedPos = nextPos;

                        break;
                    }
                case ActionType.Attack:
                    // Attack is just a wrapper for Skill in the new system.
                    ProcessSkillAction(m, act, executeBeat, ref last);
                    break;

                case ActionType.CastSkill:
                    ProcessSkillAction(m, act, executeBeat, ref last);
                    break;

                case ActionType.Move:
                    {
                        last = Math.Max(last, executeBeat + 1); // Consumes 1 beat
                        // Strategy Move
                        GridPos targetPos = ResolveMoveStrategy(plannedPos, m, players, act);
                        
                        // Basic validation
                        if (!_map2d.IsWalkable(targetPos.X, targetPos.Y))
                        {
                             targetPos = plannedPos; 
                        }

                        var cmd = new PlayerActionCmd
                        {
                            ActorId = m.Id,
                            Kind = ActionKind.Move,
                            TargetCell = targetPos,
                            ClientSendTimeMs = 0,
                            ServerReceiveTimeMs = 0
                        };
                        _actions.ScheduleServerCommand(executeBeat, cmd);
                        plannedPos = targetPos;
                        break;
                    }
            }
        }
        //Console.WriteLine($"[ScheduleTimeLine] LockCount :{last}=========================");

        return needsPadding ? (last + 1) : last;
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
    // [REMOVED] Legacy SkillDef Helper Methods (Dead Code)
    /*
     * ComputeCellsFromSkill, ResolveStep4, BuildRectOriented, etc. 
     * were removed because they depended on the deleted SkillDef class 
     * and were not used in the new PatternRunner logic.
     */


    // TargetDef 확장 포인트:
    // - 현재 MVP는 ClosestPlayer만 지원한다.
    // - 추후 LowestHp / Random / Aggro / 특정 Slot 등으로 확장할 수 있다.
    // - 중요한 점: "패턴 타임라인 내에서 타겟을 고정할지(락) / 매 액션마다 재선정할지" 정책도 필요.

    //private MapEntity? FindTarget(MapEntity m, IList<MapEntity> players, TargetDef? t)
    //{
    //    // MVP: ClosestPlayer만
    //    return FindClosestPlayer(m, players, out _);
    //}
    private MapEntity FindTarget( MapEntity self, IList<MapEntity> players, TargetDef target)
    {
        MapEntity best = null;
        int bestDist = int.MaxValue;

        // 1) 후보 필터링
        List<MapEntity> candidates = new();
        foreach (var p in players)
        {
            if (target.RequireAlive && !p.IsAlive)
                continue;

            int dist = Math.Abs(p.Position.X - self.Position.X)
                     + Math.Abs(p.Position.Y - self.Position.Y);

            if (dist > target.MaxRange)
                continue;

            candidates.Add(p);
        }

        if (candidates.Count == 0)
            return null;

        // 2) 선택
        switch (target.Type)
        {
            case TargetType.ClosestPlayer:
                foreach (var p in candidates)
                {
                    int d = Math.Abs(p.Position.X - self.Position.X)
                          + Math.Abs(p.Position.Y - self.Position.Y);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = p;
                    }
                }
                return best;

            case TargetType.LowestHpPlayer:
                int minHp = int.MaxValue;
                foreach (var p in candidates)
                {
                    int hp = p.GetState<int>("HP");
                    if (hp < minHp)
                    {
                        minHp = hp;
                        best = p;
                    }
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
            TelegraphOriginType.Self => self.Position,
            TelegraphOriginType.Target => target.Position,
            TelegraphOriginType.Point => new GridPos(area.OriginX, area.OriginY),
            _ => self.Position
        };
    }

    private SC_BeatTelegraphs.Telegraphs BuildTelegraphEntry(
        int casterId,
        byte styleId,
        int durationBeats,
        List<GridPos> frozenCells)
    {
        // 패킷에 TargetId가 없으니 OriginType은 Point로 확정해서 보내는 걸 추천
        var tg = new SC_BeatTelegraphs.Telegraphs
        {
            CasterId = casterId,
            StyleId = styleId,
            DurationBeats = durationBeats,

            Shape = (byte)TelegraphShape.Cells,
            // 텔레그래프 패킷에 TargetId(OriginEntityId)가 없기 때문에
            // OriginType=Target로 보내면 클라이언트가 "어느 타겟인지"를 재현할 방법이 없다.
            // 따라서 서버에서 origin 좌표를 확정해서 OriginType=Point로 강제한다.
            // (추후 TargetId 필드가 패킷에 추가되면 Target 기반으로도 가능)

            OriginType = (byte)TelegraphOriginType.Point,
            OriginX = 0,
            OriginY = 0,

            ParamA = 0,
            ParamB = 0
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

    // New helper: Moves 'steps' count towards 'to'
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

        if (Math.Abs(dx) > Math.Abs(dy))
            return new GridPos(from.X + Math.Sign(dx), from.Y);
        else if (dy != 0)
            return new GridPos(from.X, from.Y + Math.Sign(dy));
        else
            return from;
    }
    private void ApplyPhaseTransitions( MonsterPatternDef def, MapEntity monster, RuntimeState st, long beatIndex )
    {
        if (def.Transitions == null || def.Transitions.Count == 0)
            return;

        foreach (var tr in def.Transitions)
        {
            if (tr.FromPhaseId != st.PhaseId)
                continue;

            bool passed = tr.Type switch
            {
                PhaseTransitionType.HpPercentLE =>
                    monster.GetState<int>("HP") * 100
                        <= monster.GetState<int>("MaxHP") * tr.Value,

                PhaseTransitionType.TimeSinceSpawnBeatsGE =>
                    beatIndex - monster.GetState<long>("SpawnBeat") >= tr.Value,

                _ => false
            };

            if (!passed)
                continue;

            // Phase 전환
            st.PhaseId = tr.ToPhaseId;

            // 전환 순간 기존 lock / cooldown 정리 (안 하면 패턴 꼬임)
            st.LockedUntilBeat = -1;
            st.Cooldowns.Clear();

            return; // 한 beat에 하나만 전환
        }
    }

    private GridPos ResolveMoveStrategy(GridPos currentPos, MapEntity m, IList<MapEntity> players, ActionDef act)
    {
        int dist = act.MoveDistance;
        if (dist <= 0) return currentPos;

        switch (act.MoveStrategy)
        {
            case MoveStrategy.Random:
                {
                    // Simple random neighbor loop multiplied by dist? 
                    // Or simple jump? Basic random implementation:
                    int[] dx = { 0, 0, 1, -1 };
                    int[] dy = { 1, -1, 0, 0 };
                    
                    // Try to find a valid spot 'dist' away in one of 4 directions
                    List<GridPos> valids = new();
                    
                    for(int i=0; i<4; i++)
                    {
                        var cand = new GridPos(currentPos.X + dx[i]*dist, currentPos.Y + dy[i]*dist);
                        if (_map2d.IsWalkable(cand.X, cand.Y)) valids.Add(cand);
                    }
                    if (valids.Count > 0) return valids[_rng.Next(valids.Count)];
                    return currentPos;
                }
            case MoveStrategy.Flee:
                {
                    var target = FindTarget(m, players, act.Target);
                    if (target == null) return currentPos; // No target to flee from
                    
                    // Calculate vector away from target
                    int dx = currentPos.X - target.Position.X;
                    int dy = currentPos.Y - target.Position.Y;
                    
                    int sx = 0, sy = 0;
                    if (Math.Abs(dx) >= Math.Abs(dy)) sx = Math.Sign(dx);
                    else sy = Math.Sign(dy);
                    
                    if (sx == 0 && sy == 0) sx = 1; // Emergency move right if overlapped

                    // Move 'dist' steps in that direction
                    return new GridPos(currentPos.X + sx * dist, currentPos.Y + sy * dist);
                }
            case MoveStrategy.Forward:
                 {
                    // Forward = Toward Target (simplified)
                    // Move 'dist' steps towards target
                    var target = FindClosestPlayer(m, players, out _);
                    if (target == null) return currentPos;
                    return GetPathPosition(currentPos, target.Position, dist);
                 }
            case MoveStrategy.Backward:
                 {
                    // Backward = Away from Target (same as Flee but assumes closest player? logic)
                    // Reusing Flee logic but forcing closest player if no target specified?
                    
                    var target = FindClosestPlayer(m, players, out _);
                    if (target == null) return currentPos;

                    // Same flee logic as above
                    int dx = currentPos.X - target.Position.X;
                    int dy = currentPos.Y - target.Position.Y;
                    
                    int sx = 0, sy = 0;
                    if (Math.Abs(dx) >= Math.Abs(dy)) sx = Math.Sign(dx);
                    else sy = Math.Sign(dy);
                    
                    if (sx == 0 && sy == 0) sx = -1; // Different fallback than flee?

                    return new GridPos(currentPos.X + sx * dist, currentPos.Y + sy * dist);
                 }
            default:
                return currentPos;
        }
    }

    private void ProcessSkillAction(MapEntity m, ActionDef act, long executeBeat, ref long last)
    {
        if (!GameServer.Content.Skill.NewSkillDatabase.TryGet(act.SkillId, out var skillDef))
        {
            //Console.WriteLine($"[PatternRunner] Skill not found: {act.SkillId}");
            return;
        }

        var runner = new GameServer.Content.Skill.SkillRunner(m.Id, _world, _actions, _frozen, _telegraph);
        runner.StartSkill(skillDef, (int)executeBeat);

        if (_rt.TryGetValue(m.Id, out var rt))
        {
            rt.ActiveSkills.Add(runner);
        }

        long endBeat = executeBeat + skillDef.TotalDurationBeats;
        last = Math.Max(last, endBeat);
    }

    private sealed class RuntimeState
    {
        public string PhaseId = "P1";

        public long LockedUntilBeat = -1;
        public Dictionary<string, long> Cooldowns = new();
        
        // New Skill System
        public List<GameServer.Content.Skill.SkillRunner> ActiveSkills = new();
    }
}

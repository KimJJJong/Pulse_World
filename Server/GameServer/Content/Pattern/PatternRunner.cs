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
        if (beatIndex < st.LockedUntilBeat) return;

        MonsterPatternDef def = _patterns.GetMonster(monsterType);
        if (def == null) // Fallback to Default if not found
             def = _patterns.GetMonster("Default");

        if (def == null)
        {
            // Still null? Stop.
            return;
        }
        ApplyPhaseTransitions(def, monster, st, beatIndex);


        if (def == null)
        {
            Console.WriteLine($"[Run] MonsterPatterDef == null");
            return;
        }

        PhaseDef phase = def.GetPhase(st.PhaseId) ?? def.GetPhase(def.DefaultPhase);
        if (phase == null || phase.Selectors.Count == 0)
        {
            Console.WriteLine($"[Run] phase == null || phase.Selectors.Count == 0");
            return;
        }
        List<SelectorDef> candidates = new List<SelectorDef>(8);
        foreach (var sel in phase.Selectors)
        {
            if (IsInCooldown(st, sel.Id, beatIndex)) continue;
            if (!EvaluateWhen(sel.When, monster, players)) continue;
            candidates.Add(sel);
        }
        if (candidates.Count == 0) return;

        SelectorDef picked = WeightedPick(candidates);

        long locked = ScheduleTimeline(beatIndex, monster, players, picked);
        st.LockedUntilBeat = Math.Max(st.LockedUntilBeat, locked);

        if (picked.CooldownBeats > 0)
            st.Cooldowns[picked.Id] = beatIndex + picked.CooldownBeats;
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
                    break;

                case ActionType.MoveStepToward:
                    {
                        MapEntity target = FindTarget(m, players, act.Target);
                        if (target == null)
                        {
                            //Console.WriteLine("[ScheduleTimeline : MoveStepToward ] (target == null)");
                            break;
                        }
                        var nextPos = StepTowards(plannedPos, target.Position);

                        if(! _map2d.IsWalkable(nextPos.X, nextPos.Y) )
                        {
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
                // 중요(동기화/신뢰성):
                // 텔레그래프(예고)와 실제 공격 판정은 반드시 동일한 Area 정의를 사용해야 한다.
                // 현재는 originPoint만 고정하고 실제 판정은 _world.TryUseSkill() 내부 규칙에 의존한다.
                // 운영 단계에서는 아래 둘 중 하나로 통일 필요:
                //  1) 서버에서 areaCells를 미리 계산해 Freeze하고, executeBeat에 그 cells로 판정한다.
                //  2) TryUseSkill가 항상 "originPoint + (skill/area정의)"만으로 판정하게 만들어
                //     월드 상태(타겟 이동/방향 등)에 의해 area가 달라지지 않게 한다.

                case ActionType.Attack:
                    {

                        MapEntity target = FindTarget(m, players, act.Target);
                        if (target == null)
                        {
                            // Console.WriteLine("[ScheduleTimeline : Attack ] (target == null)");
                            break;
                        }

                        var originPoint = ResolveOriginPoint(m, target, act.Area);

                        if (!SkillDatabase.TryGet(act.SkillId, out var skill))
                        {
                            Console.WriteLine($"[Attack] Skill not found: {act.SkillId}");
                            break;
                        }

                        //  SkillDef 기준 Freeze
                        var frozenCells = ComputeCellsFromSkill(skill, plannedPos/*originPoint*/, self: m, target: target, areaHint: act.Area);
                        _frozen.Put(m.Id, executeBeat, act.SkillId, frozenCells);

                        if (act.TelegraphBeats > 0)
                            needsPadding = true;

                        // 텔레그래프 예약 (Shape=Cells로 frozenCells 그대로)
                        int teleBeats = Math.Max(0, act.TelegraphBeats);
                        if (teleBeats > 0)
                        {
                            long teleBeat = executeBeat - teleBeats;
                            if (teleBeat >= baseBeat)
                            {
                                var entry = BuildTelegraphEntry(
                                    casterId: m.Id,
                                    styleId: act.TelegraphStyleId,
                                    durationBeats: teleBeats,
                                    frozenCells: frozenCells
                                );
                                //Console.WriteLine($"[ScheduleTimeLine] TeleIndex :{teleBeat} ");
                                _telegraph.Schedule(teleBeat, entry);
                            }
                        }

                        // 실제 공격 예약
                        var cmd = new PlayerActionCmd
                        {
                            ActorId = m.Id,
                            Kind = ActionKind.Skill,
                            SkillId = act.SkillId,
                            TargetCell = originPoint,
                            ClientSendTimeMs = 0,
                            ServerReceiveTimeMs = 0
                        };
                        //Console.WriteLine($"[ScheduleTimeLine] DamageBeatIndex :{executeBeat} || Action : {ActionKind.Skill}");
                        _actions.ScheduleServerCommand(executeBeat, cmd);
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
    #region Shape
    private List<GridPos> ComputeCellsFromSkill(
        SkillDef skill,
        GridPos origin,
        MapEntity self,
        MapEntity target,
        AreaDef areaHint)
    {
        return skill.Shape switch
        {
            SkillAoeShape.Cells =>
                new List<GridPos>(areaHint.Cells), // (운영에서 금지하려면 여기서 throw/return empty)

            SkillAoeShape.Diamond =>
                BuildDiamond(origin, radius: skill.ParamA),

            SkillAoeShape.Rect =>
                BuildRectOriented(origin, width: skill.ParamA, height: skill.ParamB,
                                  self: self, target: target, skill: skill),

            SkillAoeShape.Line =>
                BuildLineOriented(origin, length: skill.ParamA,
                                  self: self, target: target, skill: skill),

            _ =>
                BuildDiamond(origin, 1)
        };
    }



    private static List<GridPos> BuildRectOriented(
    GridPos origin,
    int width,
    int height,
    MapEntity self,
    MapEntity target,
    SkillDef skill)
    {
        width = width <= 0 ? 1 : width;
        height = height <= 0 ? 1 : height;

        var (sx, sy) = ResolveStep4(self, target, skill);

        // 진행방향에 수직인 좌우 방향(perp)
        int px = -sy;
        int py = sx;

        int halfW = width / 2;

        var list = new List<GridPos>(width * height);

        // i=0..height-1 : 진행방향으로 뻗는 길이
        for (int i = 0; i < height; i++)
        {
            int baseX = origin.X + sx * i;
            int baseY = origin.Y + sy * i;

            // 좌우 폭
            for (int w = -halfW; w <= halfW; w++)
            {
                int x = baseX + px * w;
                int y = baseY + py * w;
                list.Add(new GridPos(x, y));
            }
        }

        return list;
    }


    private static List<GridPos> BuildDiamond(GridPos o, int radius)
    {
        radius = radius <= 0 ? 1 : radius;
        var list = new List<GridPos>();
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                if (System.Math.Abs(dx) + System.Math.Abs(dy) <= radius)
                    list.Add(new GridPos(o.X + dx, o.Y + dy));
        return list;
    }

    private static List<GridPos> BuildLineOriented(
    GridPos origin,
    int length,
    MapEntity self,
    MapEntity target,
    SkillDef skill)
    {
        length = length <= 0 ? 1 : length;

        var (sx, sy) = ResolveStep4(self, target, skill);

        var list = new List<GridPos>(length);
        int x = origin.X, y = origin.Y;

        for (int i = 0; i < length; i++)
        {
            list.Add(new GridPos(x, y));
            x += sx;
            y += sy;
        }

        return list;
    }


    #endregion

    /// <summary>
    /// 방향 계산 Utill
    /// </summary>
    /// <param name="self"></param>
    /// <param name="target"></param>
    /// <param name="skill"></param>
    /// <returns></returns>
    private static (int stepX, int stepY) ResolveStep4(
    MapEntity self,
    MapEntity target,
    SkillDef skill)
    {
        switch (skill.DirType)
        {
            case SkillDirType.Fixed:
                return skill.FixedDir switch
                {
                    FixedDir.Up => (0, 1),
                    FixedDir.Right => (1, 0),
                    FixedDir.Down => (0, -1),
                    FixedDir.Left => (-1, 0),
                    _ => (0, 1)
                };

            case SkillDirType.SelfToTarget:
            default:
                {
                    int dx = target.Position.X - self.Position.X;
                    int dy = target.Position.Y - self.Position.Y;

                    int stepX = 0, stepY = 0;
                    if (Math.Abs(dx) >= Math.Abs(dy)) stepX = Math.Sign(dx);
                    else stepY = Math.Sign(dy);

                    // 같은 칸이면 기본 Up
                    if (stepX == 0 && stepY == 0) stepY = 1;
                    return (stepX, stepY);
                }
        }
    }


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

    private sealed class RuntimeState
    {
        public string PhaseId = "P1";

        public long LockedUntilBeat = -1;
        public Dictionary<string, long> Cooldowns = new();
    }
}

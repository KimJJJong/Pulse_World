#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class PatternValidation
{
    public static bool TryAutoFixAndValidate(
        MonsterPatternSet set,
        SkillsAsset skillsAsset,
        out string report,
        out bool hasErrors)
    {
        var sb = new StringBuilder();
        hasErrors = false;

        if (set == null)
        {
            report = "MonsterPatternSet is null";
            hasErrors = true;
            return false;
        }

        if (set.Monsters == null)
            set.Monsters = new List<MonsterPatternDef>();

        var skillIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (skillsAsset != null && skillsAsset.Data?.Skills != null)
        {
            foreach (var s in skillsAsset.Data.Skills)
            {
                var id = (s.SkillId ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(id))
                    skillIds.Add(id);
            }
        }

        // MonsterType Trim + empty 제거
        foreach (var m in set.Monsters)
            m.MonsterType = (m.MonsterType ?? "").Trim();

        int beforeMon = set.Monsters.Count;
        set.Monsters.RemoveAll(m => string.IsNullOrWhiteSpace(m.MonsterType));
        int removed = beforeMon - set.Monsters.Count;
        if (removed > 0) sb.AppendLine($"[AutoFix] Removed {removed} monsters with empty MonsterType.");

        // MonsterType 중복 처리(운영상 중복은 거의 에러)
        var monUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in set.Monsters)
        {
            if (monUsed.Add(m.MonsterType)) continue;
            sb.AppendLine($"[Error] Duplicate MonsterType: '{m.MonsterType}'");
            hasErrors = true;
        }

        foreach (var mon in set.Monsters)
        {
            // 기본 리스트 보정
            mon.DefaultPhase = (mon.DefaultPhase ?? "P1").Trim();
            mon.Phases ??= new List<PhaseDef>();
            mon.Transitions ??= new List<PhaseTransitionDef>();

            // PhaseId Trim + empty 제거
            foreach (var ph in mon.Phases)
            {
                ph.Id = (ph.Id ?? "").Trim();
                ph.Selectors ??= new List<SelectorDef>();
            }

            int beforePhase = mon.Phases.Count;
            mon.Phases.RemoveAll(p => string.IsNullOrWhiteSpace(p.Id));
            int removedPhase = beforePhase - mon.Phases.Count;
            if (removedPhase > 0)
                sb.AppendLine($"[AutoFix] Monster '{mon.MonsterType}': removed {removedPhase} phases with empty Id.");

            // PhaseId 중복 검사
            var phaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ph in mon.Phases)
            {
                if (!phaseIds.Add(ph.Id))
                {
                    sb.AppendLine($"[Error] Monster '{mon.MonsterType}': duplicate PhaseId '{ph.Id}'");
                    hasErrors = true;
                }
            }

            // DefaultPhase 존재 검사
            if (!phaseIds.Contains(mon.DefaultPhase))
            {
                sb.AppendLine($"[Error] Monster '{mon.MonsterType}': DefaultPhase '{mon.DefaultPhase}' not found in phases.");
                hasErrors = true;
            }

            // Selector + Timeline 검사
            foreach (var ph in mon.Phases)
            {
                foreach (var sel in ph.Selectors)
                {
                    sel.Id = (sel.Id ?? "").Trim();
                    sel.When ??= new WhenGroup();
                    sel.When.All ??= new List<ConditionDef>();
                    sel.Timeline ??= new List<ActionDef>();

                    if (sel.Weight < 1)
                    {
                        sb.AppendLine($"[AutoFix] Monster '{mon.MonsterType}' Phase '{ph.Id}' Selector '{sel.Id}': Weight < 1 -> 1");
                        sel.Weight = 1;
                    }
                    if (sel.CooldownBeats < 0)
                    {
                        sb.AppendLine($"[AutoFix] Monster '{mon.MonsterType}' Selector '{sel.Id}': CooldownBeats < 0 -> 0");
                        sel.CooldownBeats = 0;
                    }

                    foreach (var act in sel.Timeline)
                    {
                        act.SkillId = (act.SkillId ?? "").Trim();
                        act.Target ??= new TargetDef();
                        act.Area ??= new AreaDef();
                        act.Area.Cells ??= new List<GridPos>();

                        if (act.AtBeatOffset < 0)
                        {
                            sb.AppendLine($"[Error] Monster '{mon.MonsterType}' Selector '{sel.Id}': Action AtBeatOffset < 0");
                            hasErrors = true;
                        }

                        if (act.Type == ActionType.Attack)
                        {
                            if (string.IsNullOrWhiteSpace(act.SkillId))
                            {
                                sb.AppendLine($"[Error] Monster '{mon.MonsterType}' Selector '{sel.Id}': Attack action missing SkillId");
                                hasErrors = true;
                            }
                            else if (skillIds.Count > 0 && !skillIds.Contains(act.SkillId))
                            {
                                sb.AppendLine($"[Error] Monster '{mon.MonsterType}' Selector '{sel.Id}': SkillId '{act.SkillId}' not found in SkillsAsset");
                                hasErrors = true;
                            }

                            if (act.TelegraphBeats < 0)
                            {
                                sb.AppendLine($"[AutoFix] Monster '{mon.MonsterType}' Selector '{sel.Id}': TelegraphBeats < 0 -> 0");
                                act.TelegraphBeats = 0;
                            }

                            // teleBeat이 baseBeat보다 과거로 가는 케이스 경고(런타임에서 막지만 에디터에서 경고)
                            if (act.TelegraphBeats > act.AtBeatOffset)
                            {
                                sb.AppendLine($"[Warn] Monster '{mon.MonsterType}' Selector '{sel.Id}': TelegraphBeats({act.TelegraphBeats}) > AtBeatOffset({act.AtBeatOffset}). teleBeat may fall before baseBeat.");
                            }
                        }
                    }
                }
            }

            // Transition 검사 (From/To 존재)
            foreach (var tr in mon.Transitions)
            {
                tr.FromPhaseId = (tr.FromPhaseId ?? "").Trim();
                tr.ToPhaseId = (tr.ToPhaseId ?? "").Trim();

                if (!phaseIds.Contains(tr.FromPhaseId))
                {
                    sb.AppendLine($"[Error] Monster '{mon.MonsterType}': Transition From '{tr.FromPhaseId}' not found.");
                    hasErrors = true;
                }
                if (!phaseIds.Contains(tr.ToPhaseId))
                {
                    sb.AppendLine($"[Error] Monster '{mon.MonsterType}': Transition To '{tr.ToPhaseId}' not found.");
                    hasErrors = true;
                }
            }
        }

        if (sb.Length == 0)
            sb.AppendLine("No issues found.");

        report = sb.ToString();
        return true;
    }
}
#endif

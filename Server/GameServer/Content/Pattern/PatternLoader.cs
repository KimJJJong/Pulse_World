using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class PatternLoader
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        IncludeFields = true // [FIX] Added to support field-based DTOs
    };

    public sealed class PatternLoadReport
    {
        public int FilesScanned;
        public int MonstersLoaded;
        public int PhasesLoaded;
        public int SelectorsLoaded;
        public int ActionsLoaded;

        public int Warnings;
        public int Errors;

        public List<string> MonsterTypes = new();
        public List<string> WarningLines = new();
        public List<string> ErrorLines = new();
    }

    /// <summary>
    /// patternsDir 아래의 모든 *.json을 읽어서 MonsterPatternSet으로 병합한다.
    /// - 파일 루트가 { "Monsters": [...] } 인 포맷(MonsterPatternSet) 지원
    /// - 파일 루트가 { "MonsterType": "..."} 인 포맷(MonsterPatternDef)도 지원
    /// </summary>
    public static MonsterPatternSet LoadFromDirectory(string patternsDir, out PatternLoadReport report)
    {
        if (!Directory.Exists(patternsDir))
            throw new DirectoryNotFoundException($"Pattern directory not found: {patternsDir}");

        report = new PatternLoadReport();

        var byType = new Dictionary<string, MonsterPatternDef>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(patternsDir, "*.json", SearchOption.AllDirectories))
        {
            report.FilesScanned++;
            var json = File.ReadAllText(file);

            try
            {
                if (LooksLikeSet(json))
                {
                    var set = JsonSerializer.Deserialize<MonsterPatternSet>(json, Opt)
                              ?? throw new Exception("Deserialize MonsterPatternSet returned null");

                    if (set.Monsters == null || set.Monsters.Count == 0)
                        continue;

                    foreach (var def in set.Monsters)
                        AddOne(def, file, byType, report);
                }
                else
                {
                    var def = JsonSerializer.Deserialize<MonsterPatternDef>(json, Opt)
                              ?? throw new Exception("Deserialize MonsterPatternDef returned null");

                    AddOne(def, file, byType, report);
                }
            }
            catch (Exception e)
            {
                report.Errors++;
                report.ErrorLines.Add($"[ERROR] {file} : {e.Message}");
            }
        }

        var result = new MonsterPatternSet();
        result.Monsters.AddRange(byType.Values);

        report.MonsterTypes.Sort(StringComparer.OrdinalIgnoreCase);

        return result;
    }

    public static void PrintReport(PatternLoadReport r)
    {
        Console.WriteLine("[PatternLoader] ===== Load Summary =====");
        Console.WriteLine($"  FilesScanned     : {r.FilesScanned}");
        Console.WriteLine($"  MonstersLoaded   : {r.MonstersLoaded}");
        Console.WriteLine($"  PhasesLoaded     : {r.PhasesLoaded}");
        Console.WriteLine($"  SelectorsLoaded  : {r.SelectorsLoaded}");
        Console.WriteLine($"  ActionsLoaded    : {r.ActionsLoaded}");
        Console.WriteLine($"  Warnings         : {r.Warnings}");
        Console.WriteLine($"  Errors           : {r.Errors}");

        int show = Math.Min(30, r.MonsterTypes.Count);
        Console.WriteLine($"  MonsterTypes({r.MonsterTypes.Count}) : " +
                          (show == 0 ? "(none)" : string.Join(", ", r.MonsterTypes.GetRange(0, show))) +
                          (r.MonsterTypes.Count > show ? " ..." : ""));
        Console.WriteLine("[PatternLoader] ========================");

        // 경고/에러는 너무 길어질 수 있으니 마지막에 일부만 출력
        if (r.WarningLines.Count > 0)
        {
            Console.WriteLine("[PatternLoader] ---- Warnings (top 20) ----");
            for (int i = 0; i < Math.Min(20, r.WarningLines.Count); i++)
                Console.WriteLine(r.WarningLines[i]);
        }

        if (r.ErrorLines.Count > 0)
        {
            Console.WriteLine("[PatternLoader] ---- Errors (top 20) ----");
            for (int i = 0; i < Math.Min(20, r.ErrorLines.Count); i++)
                Console.WriteLine(r.ErrorLines[i]);
        }
    }

    /// <summary>
    /// 로드된 MonsterPatternSet에 대해 기본 무결성 검사.
    /// - MonsterType/PhaseId/SelectorId 중복
    /// - Timeline의 Attack인데 SkillId가 비었거나 존재하지 않음
    /// - TelegraphBeats > AtBeatOffset 같은 설계 실수 경고
    /// </summary>
    public static void Validate(
        MonsterPatternSet set,
        Func<string, bool> isValidSkillId,
        PatternLoadReport report)
    {
        if (set.Monsters == null)
            return;

        // MonsterType 중복은 Load 단계에서 막지만, 혹시 모를 외부 구성 대비
        var monsterTypeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in set.Monsters)
        {
            if (m == null) continue;

            if (string.IsNullOrWhiteSpace(m.MonsterType))
            {
                Warn(report, $"[Monster] MonsterType is empty (in memory)");
                continue;
            }

            if (!monsterTypeSet.Add(m.MonsterType))
                Error(report, $"[Monster:{m.MonsterType}] Duplicate MonsterType found (merged)");

            // PhaseId 중복
            var phaseIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ph in m.Phases ?? new List<PhaseDef>())
            {
                if (ph == null) continue;
                if (string.IsNullOrWhiteSpace(ph.Id))
                {
                    Warn(report, $"[Monster:{m.MonsterType}] Phase has empty Id");
                    continue;
                }

                if (!phaseIdSet.Add(ph.Id))
                    Error(report, $"[Monster:{m.MonsterType}] Duplicate PhaseId '{ph.Id}'");

                // SelectorId 중복(phase 내)
                var selectorIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var sel in ph.Selectors ?? new List<SelectorDef>())
                {
                    if (sel == null) continue;

                    if (string.IsNullOrWhiteSpace(sel.Id))
                    {
                        Warn(report, $"[Monster:{m.MonsterType}][Phase:{ph.Id}] Selector has empty Id");
                        continue;
                    }

                    if (!selectorIdSet.Add(sel.Id))
                        Error(report, $"[Monster:{m.MonsterType}][Phase:{ph.Id}] Duplicate SelectorId '{sel.Id}'");

                    // Timeline 검사
                    foreach (var act in sel.Timeline ?? new List<ActionDef>())
                    {
                        if (act == null) continue;

                        // TelegraphBeats 설계 경고
                        if (act.TelegraphBeats > 0 && act.AtBeatOffset >= 0 && act.TelegraphBeats > act.AtBeatOffset)
                        {
                            Warn(report,
                                $"[Monster:{m.MonsterType}][{ph.Id}/{sel.Id}] TelegraphBeats({act.TelegraphBeats}) > AtBeatOffset({act.AtBeatOffset}) → telegraph may not appear");
                        }

                        // Attack인데 SkillId 누락/미존재
                        if (act.Type == ActionType.Attack)
                        {
                            if (string.IsNullOrWhiteSpace(act.SkillId))
                            {
                                Error(report,
                                    $"[Monster:{m.MonsterType}][{ph.Id}/{sel.Id}] Attack action missing SkillId (AtOffset={act.AtBeatOffset})");
                            }
                            else if (!isValidSkillId(act.SkillId))
                            {
                                Error(report,
                                    $"[Monster:{m.MonsterType}][{ph.Id}/{sel.Id}] SkillId '{act.SkillId}' not found in SkillDatabase");
                            }
                        }
                    }
                }
            }
        }
    }

    private static bool LooksLikeSet(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("Monsters", out _);
        }
        catch
        {
            return false;
        }
    }

    private static void AddOne(
        MonsterPatternDef def,
        string file,
        Dictionary<string, MonsterPatternDef> byType,
        PatternLoadReport report)
    {
        if (def == null)
            throw new Exception("MonsterPatternDef is null");

        if (string.IsNullOrWhiteSpace(def.MonsterType))
            throw new Exception($"MonsterType missing in {file}");

        // 기본 방어
        def.DefaultPhase ??= "P1";
        def.Phases ??= new List<PhaseDef>();

        // 네 코드에 Transitions 필드가 있다면 유지(없으면 이 줄 지워도 됨)
        def.Transitions ??= new List<PhaseTransitionDef>();

        if (byType.ContainsKey(def.MonsterType))
            throw new Exception($"Duplicate MonsterType '{def.MonsterType}' found while merging: {file}");

        byType.Add(def.MonsterType, def);

        report.MonstersLoaded++;
        report.MonsterTypes.Add(def.MonsterType);

        // 통계 집계
        foreach (var p in def.Phases)
        {
            report.PhasesLoaded++;
            foreach (var s in p.Selectors ?? new List<SelectorDef>())
            {
                report.SelectorsLoaded++;
                report.ActionsLoaded += (s.Timeline?.Count ?? 0);
            }
        }
    }

    private static void Warn(PatternLoadReport r, string msg)
    {
        r.Warnings++;
        r.WarningLines.Add("[WARN] " + msg);
    }

    private static void Error(PatternLoadReport r, string msg)
    {
        r.Errors++;
        r.ErrorLines.Add("[ERROR] " + msg);
    }
}

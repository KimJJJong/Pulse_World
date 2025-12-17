using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class SkillLoader
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public sealed class SkillLoadReport
    {
        public int FilesScanned;
        public int SkillsLoaded;

        public int Warnings;
        public int Errors;

        public List<string> SkillIds = new();
        public List<string> WarningLines = new();
        public List<string> ErrorLines = new();
    }

    /// <summary>
    /// skillsDir 아래의 모든 *.json을 읽어서 SkillSet으로 병합한다.
    /// 파일 포맷은 기본적으로 { "Skills": [ ... ] } 를 기대한다.
    /// </summary>
    public static SkillSet LoadFromDirectory(string skillsDir, out SkillLoadReport report)
    {
        if (!Directory.Exists(skillsDir))
            throw new DirectoryNotFoundException($"Skill directory not found: {skillsDir}");

        report = new SkillLoadReport();

        var byId = new Dictionary<string, SkillDef>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(skillsDir, "*.json", SearchOption.AllDirectories))
        {
            report.FilesScanned++;

            try
            {
                var json = File.ReadAllText(file);
                var set = JsonSerializer.Deserialize<SkillSet>(json, Opt)
                          ?? throw new Exception("Deserialize SkillSet returned null");

                if (set.Skills == null || set.Skills.Count == 0)
                    continue;

                foreach (var s in set.Skills)
                    AddOne(s, file, byId, report);
            }
            catch (Exception e)
            {
                report.Errors++;
                report.ErrorLines.Add($"[ERROR] {file} : {e.Message}");
            }
        }

        var result = new SkillSet();
        result.Skills.AddRange(byId.Values);

        report.SkillIds.Sort(StringComparer.OrdinalIgnoreCase);
        report.SkillsLoaded = result.Skills.Count;

        return result;
    }

    /// <summary>
    /// SkillSet에 대해 기본 무결성 검사.
    /// - Shape/Param 유효성(필요한 param이 빠졌는지)
    /// - Damage/Cooldown 범위 등
    /// </summary>
    public static void Validate(SkillSet set, SkillLoadReport report)
    {
        if (set.Skills == null) return;

        foreach (var s in set.Skills)
        {
            if (s == null) continue;

            // 1) SkillId
            if (string.IsNullOrWhiteSpace(s.SkillId))
            {
                Error(report, "[Skill] SkillId is empty (in memory)");
                continue;
            }

            // 2) 기본 수치 범위(너 프로젝트 룰에 맞춰 조정 가능)
            if (s.CooldownBeats < 0)
                Error(report, $"[Skill:{s.SkillId}] CooldownBeats < 0 ({s.CooldownBeats})");

            if (s.Damage < 0)
                Warn(report, $"[Skill:{s.SkillId}] Damage < 0 ({s.Damage}) → 의도된 값인지 확인");

            // 3) Shape/Param 검사 (Rect/Line 등은 파라미터 필요)
            // ※ 아래 enum/필드명은 네 SkillDef 정의에 맞춰 둔 가정임
            //    (Shape: TelegraphShape와 동일한 값 사용한다고 했던 흐름 기준)
            switch ((TelegraphShape)s.Shape)
            {
                case TelegraphShape.Cells:
                    // Cells형은 보통 별도 리스트가 필요하지만, SkillDef에서 cells를 들고 있지 않으면 사용 안하는 게 맞음
                    // Warn 정도만
                    Warn(report, $"[Skill:{s.SkillId}] Shape=Cells는 SkillDef만으로 영역을 들고 있지 않으면 사용 불가 (현재는 Pattern frozenCells 방식 권장)");
                    break;

                case TelegraphShape.Diamond:
                    if (s.ParamA <= 0)
                        Error(report, $"[Skill:{s.SkillId}] Diamond requires ParamA(radius) > 0");
                    break;

                case TelegraphShape.Rect:
                    if (s.ParamA <= 0 || s.ParamB <= 0)
                        Error(report, $"[Skill:{s.SkillId}] Rect requires ParamA(width)>0 and ParamB(height)>0");
                    break;

                case TelegraphShape.Line:
                    if (s.ParamA <= 0)
                        Error(report, $"[Skill:{s.SkillId}] Line requires ParamA(length) > 0");
                    // DirType가 필요한지 여부는 네 Skill 판정 룰에 따라 다름
                    // 보통 라인은 방향 필요하니까 경고
                    if (s.DirType == 0)
                        Warn(report, $"[Skill:{s.SkillId}] Line usually needs direction(DirType). 현재 DirType=0이면 self->target 방향으로 고정 처리인지 확인");
                    break;

                default:
                    Warn(report, $"[Skill:{s.SkillId}] Unknown Shape value: {s.Shape}");
                    break;
            }
        }
    }

    public static void PrintReport(SkillLoadReport r)
    {
        Console.WriteLine("[SkillLoader] ===== Load Summary =====");
        Console.WriteLine($"  FilesScanned  : {r.FilesScanned}");
        Console.WriteLine($"  SkillsLoaded  : {r.SkillsLoaded}");
        Console.WriteLine($"  Warnings      : {r.Warnings}");
        Console.WriteLine($"  Errors        : {r.Errors}");

        int show = Math.Min(30, r.SkillIds.Count);
        Console.WriteLine($"  SkillIds({r.SkillIds.Count}) : " +
                          (show == 0 ? "(none)" : string.Join(", ", r.SkillIds.GetRange(0, show))) +
                          (r.SkillIds.Count > show ? " ..." : ""));
        Console.WriteLine("[SkillLoader] ========================");

        if (r.WarningLines.Count > 0)
        {
            Console.WriteLine("[SkillLoader] ---- Warnings (top 20) ----");
            for (int i = 0; i < Math.Min(20, r.WarningLines.Count); i++)
                Console.WriteLine(r.WarningLines[i]);
        }

        if (r.ErrorLines.Count > 0)
        {
            Console.WriteLine("[SkillLoader] ---- Errors (top 20) ----");
            for (int i = 0; i < Math.Min(20, r.ErrorLines.Count); i++)
                Console.WriteLine(r.ErrorLines[i]);
        }
    }

    private static void AddOne(
        SkillDef s,
        string file,
        Dictionary<string, SkillDef> byId,
        SkillLoadReport report)
    {
        if (s == null)
            throw new Exception("SkillDef is null");

        if (string.IsNullOrWhiteSpace(s.SkillId))
            throw new Exception($"SkillId missing in {file}");

        if (byId.ContainsKey(s.SkillId))
            throw new Exception($"Duplicate SkillId '{s.SkillId}' found while merging: {file}");

        byId.Add(s.SkillId, s);
        report.SkillIds.Add(s.SkillId);
    }

    private static void Warn(SkillLoadReport r, string msg)
    {
        r.Warnings++;
        r.WarningLines.Add("[WARN] " + msg);
    }

    private static void Error(SkillLoadReport r, string msg)
    {
        r.Errors++;
        r.ErrorLines.Add("[ERROR] " + msg);
    }
}

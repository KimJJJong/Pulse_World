using System;
using System.Collections.Generic;
using System.IO;

public static class ContentStore
{
    private static bool _initialized;

    public static SkillSet? Skills { get; private set; }
    public static MonsterPatternSet? Patterns { get; private set; }
    public static Dictionary<string, MapContent>? Maps { get; private set; }

    /// <summary>
    /// ContentStore는 프로세스 당 1회만 초기화된다.
    /// role(Town/Game)에 따라 필요한 콘텐츠만 로드하도록 호출부에서 선택한다.
    ///
    /// Rules:
    /// - Patterns는 Skills에 의존하므로 Skills를 로드하지 않으면 Patterns 로드 불가.
    /// - Maps는 단독 로드 가능.
    /// </summary>
    public static void Init(
        string? skillsDir,
        string? patternsDir,
        string? mapsDir)
    {
        if (_initialized)
            throw new InvalidOperationException("ContentStore already initialized.");

        // ---------- Guard: 의존성 ----------
        // Patterns 로드하려면 Skills 필요
        if (!string.IsNullOrWhiteSpace(patternsDir) && string.IsNullOrWhiteSpace(skillsDir))
            throw new InvalidOperationException("Patterns require Skills. Provide skillsDir when patternsDir is set.");

        // ---------- Skills ----------
        if (!string.IsNullOrWhiteSpace(skillsDir))
        {
            EnsureDirExists(skillsDir, "Skills");

            Skills = SkillLoader.LoadFromDirectory(skillsDir, out var sReport);
            SkillLoader.Validate(Skills, sReport);
            SkillLoader.PrintReport(sReport);

            if (sReport.Errors > 0)
                throw new Exception("Skill content invalid");

            SkillDatabase.LoadFrom(Skills);
        }

        // ---------- Patterns ----------
        if (!string.IsNullOrWhiteSpace(patternsDir))
        {
            EnsureDirExists(patternsDir, "Patterns");

            Patterns = PatternLoader.LoadFromDirectory(patternsDir, out var pReport);

            // Skills를 로드했기 때문에 SkillDatabase는 유효해야 함
            PatternLoader.Validate(
                Patterns,
                skillId => SkillDatabase.TryGet(skillId, out _),
                pReport
            );

            PatternLoader.PrintReport(pReport);
            if (pReport.Errors > 0)
                throw new Exception("Pattern content invalid");
        }

        // ---------- Maps ----------
        if (!string.IsNullOrWhiteSpace(mapsDir))
        {
            EnsureDirExists(mapsDir, "Maps");

            Maps = MapLoader.LoadFromDirectory(mapsDir, out var mReport);
            ContentReportPrinter.Print(mReport);

            if (mReport.Errors > 0)
                throw new Exception("Map content invalid");

            MapDatabase.LoadFrom(Maps);
        }

        _initialized = true;
        Console.WriteLine("[ContentStore] Initialized OK");
    }

    private static void EnsureDirExists(string dir, string kind)
    {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Content directory not found ({kind}): {dir}");
    }
}

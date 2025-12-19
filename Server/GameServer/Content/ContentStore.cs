using System;
using System.Collections.Generic;

public static class ContentStore
{
    private static bool _initialized;

    public static SkillSet Skills { get; private set; } = null!;
    public static MonsterPatternSet Patterns { get; private set; } = null!;
    public static Dictionary<string, MapContent> Maps { get; private set; } = null!;

    public static void Init(
        string skillsDir,
        string patternsDir,
        string mapsDir)
    {
        if (_initialized)
            throw new InvalidOperationException("ContentStore already initialized.");

        // ===== Skills =====
        Skills = SkillLoader.LoadFromDirectory(skillsDir, out var sReport);
        SkillLoader.Validate(Skills, sReport);
        SkillLoader.PrintReport(sReport);
        if (sReport.Errors > 0)
            throw new Exception("Skill content invalid");

        SkillDatabase.LoadFrom(Skills);

        // ===== Patterns =====
        Patterns = PatternLoader.LoadFromDirectory(patternsDir, out var pReport);
        PatternLoader.Validate(
            Patterns,
            skillId => SkillDatabase.TryGet(skillId, out _),
            pReport
        );
        PatternLoader.PrintReport(pReport);
        if (pReport.Errors > 0)
            throw new Exception("Pattern content invalid");

        // ===== Maps =====
        Maps = MapLoader.LoadFromDirectory(mapsDir, out var mReport);
        ContentReportPrinter.Print(mReport);
        if (mReport.Errors > 0)
            throw new Exception("Map content invalid");

        MapDatabase.LoadFrom(Maps);

        _initialized = true;
        Console.WriteLine("[ContentStore] Initialized OK");
    }
}

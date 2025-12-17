using System;
using System.IO;

public static class ContentStore
{
    private static bool _initialized;

    public static SkillSet Skills { get; private set; } = null!;
    public static MonsterPatternSet Patterns { get; private set; } = null!;

    public static void Init(string skillsDir, string patternsDir)
    {
        if (_initialized)
            throw new InvalidOperationException("ContentStore already initialized.");

        // ===== Skills =====
        Skills = SkillLoader.LoadFromDirectory(skillsDir, out var sReport);
        SkillLoader.Validate(Skills, sReport);
        SkillLoader.PrintReport(sReport);

        if (sReport.Errors > 0)
            throw new Exception($"Skill content has {sReport.Errors} errors. Fix skills json.");

        SkillDatabase.LoadFrom(Skills);

        // ===== Patterns =====
        Patterns = PatternLoader.LoadFromDirectory(patternsDir, out var pReport);

        PatternLoader.Validate(
            Patterns,
            isValidSkillId: (skillId) => SkillDatabase.TryGet(skillId, out _),
            report: pReport
        );

        PatternLoader.PrintReport(pReport);

        if (pReport.Errors > 0)
            throw new Exception($"Pattern content has {pReport.Errors} errors. Fix patterns json.");

        _initialized = true;
        Console.WriteLine("[ContentStore] ContentStore initialized OK.");
    }

    public static void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Call ContentStore.Init() before using content.");
    }
}

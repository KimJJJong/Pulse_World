using System;

public static class ServerContentBootstrap
{
    public static ContentRegistry Load(string skillsPath, string patternsPath, string mapsDir)
    {
        // ===== Skills =====
        var skills = JsonLoader.LoadOrThrow<SkillSet>(skillsPath);
        SkillDatabase.LoadFrom(skills);

        // ===== Patterns =====
        var patterns = JsonLoader.LoadOrThrow<MonsterPatternSet>(patternsPath);
        if (patterns.Monsters == null || patterns.Monsters.Count == 0)
            Console.WriteLine("[Content] patterns loaded but Monsters is empty");

        // (선택) 패턴이 스킬 ID 참조한다면 여기서 검증
        // PatternLoader.Validate(patterns, skillId => SkillDatabase.TryGet(skillId, out _), report);

        // ===== Maps =====
        var maps = MapLoader.LoadFromDirectory(mapsDir, out var mReport);
        ContentReportPrinter.Print(mReport);

        if (mReport.Errors > 0)
            throw new Exception($"Map content has {mReport.Errors} errors. Fix maps json.");

        // (선택) MapDatabase도 정적으로 쓰고 싶으면 여기서 주입
        MapDatabase.LoadFrom(maps);

        return new ContentRegistry(skills, patterns, maps);
    }
}

using System;

public static class ServerContentBootstrap
{
    public static ContentRegistry Load(string skillsPath, string patternsPath)
    {
        var skills = JsonLoader.LoadOrThrow<SkillSet>(skillsPath);
        SkillDatabase.LoadFrom(skills); // 기존 네 SkillDatabase.LoadFrom 사용

        var patterns = JsonLoader.LoadOrThrow<MonsterPatternSet>(patternsPath);

        // 최소 검증(원하면 더 강하게)
        if (patterns.Monsters == null || patterns.Monsters.Count == 0)
            Console.WriteLine("[Content] patterns loaded but Monsters is empty");

        return new ContentRegistry(skills, patterns);
    }
}

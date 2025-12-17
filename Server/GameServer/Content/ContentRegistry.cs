public sealed class ContentRegistry
{
    public SkillSet Skills { get; }
    public MonsterPatternSet Patterns { get; }

    public ContentRegistry(SkillSet skills, MonsterPatternSet patterns)
    {
        Skills = skills;
        Patterns = patterns;
    }
}

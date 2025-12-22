using System.Collections.Generic;

public sealed class SkillSet
{
    public List<SkillDef> Skills { get; set; } = new();
}

public static class SkillDatabase
{
    private static readonly Dictionary<string, SkillDef> _map = new();

    public static void LoadFrom(SkillSet set)
    {
        _map.Clear();
        foreach (var s in set.Skills)
            _map[s.SkillId] = s;
    }
    public static bool TryGet(string skillId, out SkillDef def) => _map.TryGetValue(skillId, out def);

    //  프로토타입용: 없으면 기본 스킬 생성(원치 않으면 false로 처리해도 됨)
    public static SkillDef GetOrDefault(string skillId)
    {
        if (_map.TryGetValue(skillId, out var def)) return def;
        def = new SkillDef { SkillId = skillId/*, Damage = 10*/, BlockedByWall = false };
        _map[skillId] = def;
        return def;
    }
}
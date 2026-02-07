using System;
using System.Collections.Generic;
using GameShared.Data;

public sealed class ContentRegistry
{
    public List<NewSkillDef> Skills { get; }
    public MonsterPatternSet Patterns { get; }

    // MapId -> MapContent
    public IReadOnlyDictionary<string, MapContent> Maps { get; }

    public ContentRegistry(
        List<NewSkillDef> skills,
        MonsterPatternSet patterns,
        Dictionary<string, MapContent> maps)
    {
        Skills = skills;
        Patterns = patterns;
        Maps = maps;
    }

    public MapContent GetMap(string mapId)
        => Maps.TryGetValue(mapId, out var m)
            ? m
            : throw new Exception($"Map not found: {mapId}");
}

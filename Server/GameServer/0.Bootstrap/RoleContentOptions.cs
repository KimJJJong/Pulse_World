namespace Server.Bootstrap;

public sealed class RoleContentOptions
{
    // Content 루트. 예: D:\Git\...\GameServer\Content
    public string? BaseDir { get; init; }

    // 어떤 컨텐츠를 로딩할지
    public bool LoadSkills { get; init; } = true;
    public bool LoadPatterns { get; init; } = true;
    public bool LoadMaps { get; init; } = true;
    public bool LoadStages { get; init; } = true; // [NEW]

    // 하위 폴더 상대 경로(원하면 커스텀 가능)
    public string SkillsRelDir { get; init; } = @"Skill\Json";
    public string PatternsRelDir { get; init; } = @"Pattern\Json";
    public string MapsRelDir { get; init; } = @"Map\Json";
    public string StagesRelDir { get; init; } = @"Stage\Json"; // [NEW]
}
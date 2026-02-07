using GameServer.InGame.Director.Data;
using GameServer.Content.Skill;
using GameShared.Data; // [NEW] Added for NewSkillDef
using System;
using System.Collections.Generic;
using System.IO;

public static class ContentStore
{
    private static bool _initialized;

    // [MODIFIED] Legacy SkillSet -> List<NewSkillDef>
    public static List<NewSkillDef>? Skills { get; private set; }
    public static MonsterPatternSet? Patterns { get; private set; }
    public static Dictionary<string, MapContent>? Maps { get; private set; }
    public static Dictionary<string, GameServer.InGame.Director.Data.StageScenario>? Stages { get; private set; }

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
        string? mapsDir,
        string? stagesDir)
    {
        if (_initialized)
            throw new InvalidOperationException("ContentStore already initialized.");

        // ---------- Guard: 의존성 ----------
        // Patterns 로드하려면 Skills 필요
        if (!string.IsNullOrWhiteSpace(patternsDir) && string.IsNullOrWhiteSpace(skillsDir))
            throw new InvalidOperationException("Patterns require Skills. Provide skillsDir when patternsDir is set.");

        // ---------- Skills (New System) ----------
        if (!string.IsNullOrWhiteSpace(skillsDir))
        {
            EnsureDirExists(skillsDir, "Skills");

            // [MODIFIED] Use NewSkillLoader
            Skills = NewSkillLoader.LoadFromDirectory(skillsDir, out var sReport);
            
            // Log Summary (User Requested Format)
            if (sReport.Errors > 0)
            {
                 Console.WriteLine($"[NewSkillLoader] Errors: {sReport.Errors}");
                 foreach(var err in sReport.ErrorLines) Console.WriteLine(err);
            }

            Console.WriteLine("[SkillLoader] ===== Load Summary =====");
            Console.WriteLine($"  FilesScanned  : {sReport.FilesScanned}");
            Console.WriteLine($"  SkillsLoaded  : {sReport.SkillsLoaded}");
            Console.WriteLine($"  Warnings      : {sReport.Warnings}");
            Console.WriteLine($"  Errors        : {sReport.Errors}");
            
            if (sReport.LoadedSkillIds.Count > 0)
            {
                // Join skill IDs
                string skillList = string.Join(", ", sReport.LoadedSkillIds);
                Console.WriteLine($"  SkillIds({sReport.SkillsLoaded}) : {skillList}");
            }
            else
            {
                Console.WriteLine("  SkillIds(0) : None");
            }
            Console.WriteLine("[SkillLoader] ========================");

            if (sReport.Errors > 0)
                throw new Exception("Skill content invalid");

            // [MODIFIED] Use NewSkillDatabase
            NewSkillDatabase.LoadFrom(Skills);
        }

        // ---------- Patterns ----------
        if (!string.IsNullOrWhiteSpace(patternsDir))
        {
            EnsureDirExists(patternsDir, "Patterns");

            Patterns = PatternLoader.LoadFromDirectory(patternsDir, out var pReport);

            // [MODIFIED] Use NewSkillDatabase for validation
            PatternLoader.Validate(
                Patterns,
                // Check if skill exists in NewSkillDatabase
                skillId => NewSkillDatabase.TryGet(skillId, out _),
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

        // ---------- Stages ----------
        if (!string.IsNullOrWhiteSpace(stagesDir))
        {
            EnsureDirExists(stagesDir, "Stages");

            Stages = StageLoader.LoadFromDirectory(stagesDir);
            
            // Register to DataManager
            Console.WriteLine($"[ContentStore] Loading Stages from: {stagesDir}");
            foreach(var s in Stages.Values)
            {
                StageDataManager.Register(s);
                Console.WriteLine($"   > [LOADED] MapId: {s.MapId.PadRight(15)} | BPM: {s.RhythmSettings?.Bpm} | Spawns: {s.InitialSpawns?.Count} | Events: {s.Events?.Count}");
            }
            Console.WriteLine($"[ContentStore] Total {Stages.Count} Stages loaded.");
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

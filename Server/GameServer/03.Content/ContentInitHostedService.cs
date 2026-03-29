using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using Server.Runtime;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Content.Item;

namespace Server.Content;

public sealed class ContentInitHostedService : IHostedService
{
    private readonly ILogger<ContentInitHostedService> _log;
    private readonly IRoleModuleResolver _roleResolver;
    private readonly ServerOptions _opt;
    private readonly ItemTemplateManager _itemTemplateManager;

    public ContentInitHostedService(
        ILogger<ContentInitHostedService> log,
        IRoleModuleResolver roleResolver,
        IOptions<ServerOptions> opt,
        ItemTemplateManager itemTemplateManager)
    {
        _log = log;
        _roleResolver = roleResolver;
        _opt = opt.Value;
        _itemTemplateManager = itemTemplateManager;
    }

    public Task StartAsync(CancellationToken ct)
    {

        var role = _roleResolver.Resolve();
        if (!role.NeedsContentInit)
            return Task.CompletedTask;

        var rc = role.Name switch
        {
            "Game" => _opt.Content.Game,
            "Town" => _opt.Content.Town,
            _ => throw new InvalidOperationException($"Unknown role: {role.Name}")
        };

        var baseDir = rc.BaseDir ?? _opt.Content.DefaultBaseDir;
        if (string.IsNullOrWhiteSpace(baseDir))
            throw new InvalidOperationException($"Content baseDir is not set (role={role.Name})");

        string? skills = rc.LoadSkills ? Path.Combine(baseDir, rc.SkillsRelDir) : null;
        string? patterns = rc.LoadPatterns ? Path.Combine(baseDir, rc.PatternsRelDir) : null;
        string? maps = rc.LoadMaps ? Path.Combine(baseDir, rc.MapsRelDir) : null;
        string? stages = rc.LoadStages ? Path.Combine(baseDir, rc.StagesRelDir) : null; // [NEW]
        string? sounds = rc.LoadSounds ? Path.Combine(baseDir, rc.SoundsRelDir) : null; // [NEW]

        // 존재 검사
        EnsureDir(role.Name, "skills", skills);
        EnsureDir(role.Name, "patterns", patterns);
        EnsureDir(role.Name, "maps", maps);
        EnsureDir(role.Name, "stages", stages); 
        EnsureDir(role.Name, "sounds", sounds);

        ContentStore.Init(skills, patterns, maps, stages, sounds);

        _itemTemplateManager.Load(); // [NEW] Items

        _log.LogInformation(
            "Content initialized (role={Role}, baseDir={BaseDir}, skills={Skills}, patterns={Patterns}, maps={Maps}, stages={Stages}, items=Loaded)",
            role.Name, baseDir, skills ?? "-", patterns ?? "-", maps ?? "-", stages ?? "-");
        return Task.CompletedTask;
    }

    static void EnsureDir(string role, string kind, string? dir)
    {
        if (dir == null) return;
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Content dir not found (role={role}, kind={kind}, dir={dir})");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

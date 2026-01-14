using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using Server.Runtime;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Content;

public sealed class ContentInitHostedService : IHostedService
{
    private readonly ILogger<ContentInitHostedService> _log;
    private readonly IRoleModuleResolver _roleResolver;
    private readonly ServerOptions _opt;

    public ContentInitHostedService(
        ILogger<ContentInitHostedService> log,
        IRoleModuleResolver roleResolver,
        IOptions<ServerOptions> opt)
    {
        _log = log;
        _roleResolver = roleResolver;
        _opt = opt.Value;
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

        // 존재 검사(원하면 경고만 찍고 계속 진행하도록 바꿔도 됨)
        EnsureDir(role.Name, "skills", skills);
        EnsureDir(role.Name, "patterns", patterns);
        EnsureDir(role.Name, "maps", maps);


        ContentStore.Init(skills, patterns, maps);

        _log.LogInformation(
            "Content initialized (role={Role}, baseDir={BaseDir}, skills={Skills}, patterns={Patterns}, maps={Maps})",
            role.Name, baseDir, skills ?? "-", patterns ?? "-", maps ?? "-");
        TownManager.GetOrCreate("Town_01");
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

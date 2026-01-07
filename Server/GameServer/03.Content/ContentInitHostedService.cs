using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using Server.Runtime;
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

        // TODO: config로 baseDir 빼기 
        var baseDir = "D:\\Git\\Server\\RhythmRPG\\RhythmRPG\\Server\\GameServer\\Content";

        ContentStore.Init(
            skillsDir: Path.Combine(baseDir, "Skill", "Json"),
            patternsDir: Path.Combine(baseDir, "Pattern", "Json"),
            mapsDir: Path.Combine(baseDir, "Map", "Json")
        );

        _log.LogInformation("Content initialized (role={Role})", role.Name);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

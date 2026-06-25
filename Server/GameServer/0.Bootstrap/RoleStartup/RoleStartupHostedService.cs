using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class RoleStartupHostedService : IHostedService
{
    private readonly ILogger<RoleStartupHostedService> _log;
    private readonly IRoleModuleResolver _roleResolver;
    private readonly IEnumerable<IRoleStartup> _startups;
    private IRoleStartup? _selected;

    public RoleStartupHostedService(
        ILogger<RoleStartupHostedService> log,
        IRoleModuleResolver roleResolver,
        IEnumerable<IRoleStartup> startups)
    {
        _log = log;
        _roleResolver = roleResolver;
        _startups = startups;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var role = _roleResolver.Resolve();

        _selected = _startups.FirstOrDefault(x => x.RoleName == role.Name);
        if (_selected == null)
            throw new InvalidOperationException($"No IRoleStartup registered for role={role.Name}");

        _log.LogInformation("Role startup begin (role={Role}, startup={Startup})", role.Name, _selected.GetType().Name);
        await _selected.StartAsync(ct);
        _log.LogInformation("Role startup done (role={Role})", role.Name);
    }

    public Task StopAsync(CancellationToken ct)
        => _selected?.StopAsync(ct) ?? Task.CompletedTask;
}
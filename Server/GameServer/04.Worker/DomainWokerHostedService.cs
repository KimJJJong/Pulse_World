using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using Server.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Workers;

public sealed class DomainWorkerHostedService : IHostedService
{
    private readonly ILogger<DomainWorkerHostedService> _log;
    private readonly RoleContext _ctx;
    private readonly IEnumerable<IUpdatableSnapshotProvider> _providers;

    private DomainWorker? _worker;

    public DomainWorkerHostedService(
        ILogger<DomainWorkerHostedService> log,
        RoleContext ctx,
        IEnumerable<IUpdatableSnapshotProvider> providers)
    {
        _log = log;
        _ctx = ctx;
        _providers = providers;
    }

    public Task StartAsync(CancellationToken ct)
    {
        var p = _providers.FirstOrDefault(x => x.RoleName.Equals(_ctx.RoleName, StringComparison.OrdinalIgnoreCase));
        if (p == null)
            throw new InvalidOperationException($"No snapshot provider for role '{_ctx.RoleName}'");

        int tickMs = _ctx.Options.Role.TickMs > 0 ? _ctx.Options.Role.TickMs : p.DefaultTickMs;

        _worker = new DomainWorker(
            name: $"{_ctx.RoleName}Worker",
            snapshotGetter: p.GetSnapshotGetter(),
            tickMs: tickMs);

        _worker.Start();
        _log.LogInformation("[WORKER] started role={Role} tickMs={Tick}", _ctx.RoleName, tickMs);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _worker?.Dispose();
        return Task.CompletedTask;
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using System.Threading;
using System.Threading.Tasks;

public sealed class TownStartup : IRoleStartup
{
    public string RoleName => "Town";

    private readonly ILogger<TownStartup> _log;
    //private readonly TownManager _towns;
    private readonly ServerOptions _opt;

    public TownStartup(
        ILogger<TownStartup> log,
        //TownManager towns,
        IOptions<ServerOptions> opt)
    {
        _log = log;
        //_towns = towns;
        _opt = opt.Value;
    }

    public Task StartAsync(CancellationToken ct)
    {
        var id = _opt.Role.Name;
        var town = TownManager.GetOrCreate(id);

        _log.LogInformation("Town world created (townId={TownId})", town.TownId);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
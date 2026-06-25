using System.Threading;
using System.Threading.Tasks;

public interface IRoleStartup
{
    string RoleName { get; } // "Town" or "Game"
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
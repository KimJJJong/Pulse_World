using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public sealed class GameStartup : IRoleStartup
{
    public string RoleName => "Game";

    private readonly ILogger<GameStartup> _log;

    public GameStartup(ILogger<GameStartup> log)
        => _log = log;

    public Task StartAsync(CancellationToken ct)
    {
        // 여기는 "구독/타이머/워커" 같은 인프라만.
        // Room은 요청(Handshake/Join) 시점에 생성.
        _log.LogInformation("Game startup done (no rooms pre-created)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
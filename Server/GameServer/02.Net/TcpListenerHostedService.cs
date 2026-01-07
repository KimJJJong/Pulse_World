using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Bootstrap;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Net;

public sealed class TcpListenerHostedService : IHostedService
{
    private readonly ILogger<TcpListenerHostedService> _log;
    private readonly ServerOptions _opt;

    private readonly ServerCore.Listener _listener = new();

    public TcpListenerHostedService(ILogger<TcpListenerHostedService> log, IOptions<ServerOptions> opt)
    {
        _log = log;
        _opt = opt.Value;
    }

    public Task StartAsync(CancellationToken ct)
    {
        var ep = BuildEndPoint(_opt.Bind.Host, _opt.Bind.Port);

        _listener.Init(ep, () => SessionManager.Instance.Generate<ClientSession>());

        _log.LogInformation("Listening on {Bind} (public {PublicHost}:{PublicPort}) id={Id}",
            ep, _opt.Public.Host, _opt.Public.Port, _opt.ServerId);

        SessionSweeper.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        // Listener에 Stop/Dispose가 있으면 여기서 호출
        return Task.CompletedTask;
    }

    static IPEndPoint BuildEndPoint(string host, int port)
    {
        if (host == "0.0.0.0" || string.IsNullOrWhiteSpace(host))
            return new IPEndPoint(IPAddress.Any, port);

        if (IPAddress.TryParse(host, out var ip))
            return new IPEndPoint(ip, port);

        var ips = Dns.GetHostAddresses(host);
        var ip4 = ips.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? ips.First();
        return new IPEndPoint(ip4, port);
    }
}

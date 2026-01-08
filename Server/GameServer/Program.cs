using Microsoft.Extensions.Hosting;
using Server.Bootstrap;
using StackExchange.Redis;
using System.Threading.Tasks;

namespace Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        using IHost host = ServerHost.Build(args);
        await host.RunAsync();

    }
}

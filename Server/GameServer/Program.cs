// ------------------------------
// GameServer Program.cs (fixed)
// ------------------------------
using Microsoft.Extensions.Configuration;
using Server.withWebServer.Security;
using ServerCore;
using Shared;
using StackExchange.Redis;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Util;

namespace Server;

static class Program
{
    static Listener _clientListener = new Listener();

    static DomainWorker _gameWorker;
    static DomainWorker _townWoker;


    static string _gsId = "gs1";
    static string _publicHost = "127.0.0.1";
    static int _publicPort = 13221;

    static string _bindHost = "0.0.0.0";
    static int _bindPort = 13221;

    static int _tickRate = 30;
    static int _cap = 2;
    static int _hbSec = 2;

    static async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        var key = $"gs:{_gsId}";
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var used = await RedisAuth.DB.HashGetAsync(key, "used");
                if (used.IsNull) await RedisAuth.DB.HashSetAsync(key, new HashEntry[] { new("used", 0) });

                await RedisAuth.DB.HashSetAsync(key, new HashEntry[]
                {
                    new("host", _publicHost),
                    new("port", _publicPort.ToString()),
                    new("tickRate", _tickRate.ToString()),
                    new("cap", _cap.ToString()),
                    new("ver", "1"),
                    new("updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
                });

                await RedisAuth.DB.KeyExpireAsync(key, TimeSpan.FromSeconds(_hbSec * 3));
                await RedisAuth.DB.SetAddAsync("gs:alive", _gsId);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError("Heartbeat", ex.ToString());
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_hbSec), ct); }
            catch { }
        }
    }

    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += (_, __) =>
        {
            try { AppRef.Cts.Cancel(); } catch { }
            try { LogManager.Instance.Shutdown(); } catch { }
        };

        Console.CancelKeyPress += (_, e) => { e.Cancel = true; AppRef.Cts.Cancel(); };

        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        AppRef.Cfg = cfg;

        StartupEcho.Dump("GameServer", cfg);
        Console.WriteLine("=== CONFIG KEYS ===");
        foreach (var kv in cfg.AsEnumerable())
            Console.WriteLine($"{kv.Key} = {kv.Value}");
        Console.WriteLine("===================");



        _gsId = cfg["GameServer:Id"] ?? _gsId;
        _bindHost = cfg["GameServer:BindHost"] ?? _bindHost;
        _bindPort = ParseInt(cfg["GameServer:Port"], _bindPort);
        _publicHost = cfg["GameServer:PublicHost"]
                      ?? GuessLocalIPv4()
                      ?? _publicHost;
        _publicPort = _bindPort;
        _tickRate = ParseInt(cfg["GameServer:TickRate"], _tickRate);
        _cap = ParseInt(cfg["GameServer:Cap"], _cap);
        _hbSec = ParseInt(cfg["GameServer:HeartbeatSec"], _hbSec);

        // Redis 설정 섹션 수정

        var redisConn = cfg["ConnectionStrings:Redis"]
                        ?? "127.0.0.1:6379,abortConnect=False";

        try
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(redisConn);
            Console.WriteLine("=================================================");
            RedisAuth.InitAsync(redisConn, _gsId).GetAwaiter().GetResult();
            RedisAuth.ExpectedGsKey = AppRef.GSId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[REDIS] init failed: {ex.Message}");
            throw;
        }

        Console.WriteLine($"[GS] MyId={AppRef.GSId} TickRate={AppRef.TickRate}");

        //  JWT 
        var jwt = new JwtService(cfg);
        AppRef.Jwt = jwt;

        // Heartbeat 시작
        _ = HeartbeatLoopAsync(AppRef.Cts.Token);

        // Listener
        var endPoint = BuildBindEndPoint(_bindHost, _bindPort);

        try
        {
            _clientListener.Init(endPoint, () => SessionManager.Instance.Generate<ClientSession>());
            LogManager.Instance.LogInfo("Program",
                $"[Game Server Start] bind={endPoint} public={_publicHost}:{_publicPort} id={_gsId}");
            Console.WriteLine($"ClientSession Listening on {endPoint} (public {_publicHost}:{_publicPort})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LISTENER] bind failed: {ex.Message}");
            throw;
        }

        SessionSweeper.Start();

        /*        while (!AppRef.Cts.IsCancellationRequested)
                {
                    JobTimer.Instance.Flush();
                    Thread.Sleep(1);
                }*/
        var baseDir = "D:\\Git\\Server\\RhythmRPG\\RhythmRPG\\Server\\GameServer\\Content";//AppContext.BaseDirectory;  : in Publish, need to Change rootDir
        ContentStore.Init(
            skillsDir: Path.Combine(baseDir, "Skill", "Json"),
            patternsDir: Path.Combine(baseDir, "Pattern", "Json"),
            mapsDir: Path.Combine(baseDir,"Map","Json")
        );


        _gameWorker = new DomainWorker(
            name: "GameWoker",
            snapshotGetter: GameManager.GetUpdatablesSnapshot,
            tickMs: 15);

        _townWoker = new DomainWorker(
            name: "TownWoker",
            snapshotGetter: TownManager.GetUpdatablesSnapshot,
            tickMs: 100);

        _gameWorker.Start();
        _townWoker.Start();
        Console.WriteLine("Server started. Press ENTER to exit...");
        Console.ReadLine();
        
        _townWoker?.Dispose();
        _gameWorker?.Dispose();

        try { Task.Delay(200).Wait(); } catch { }
    }

    // UTIL
    static int ParseInt(string s, int fallback)
        => int.TryParse(s, out var v) ? v : fallback;

    static string GuessLocalIPv4()
    {
        try
        {
            var host = Dns.GetHostName();
            var ipHost = Dns.GetHostEntry(host);
            return ipHost.AddressList.FirstOrDefault(a =>
                a.AddressFamily == AddressFamily.InterNetwork)?.ToString();
        }
        catch { return null; }
    }

    static IPEndPoint BuildBindEndPoint(string host, int port)
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

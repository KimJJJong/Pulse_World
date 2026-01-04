// ------------------------------
// Program.cs (Role split: Town/Game)
// ------------------------------
using ControlPlane.Grpc.V1;
using Microsoft.Extensions.Configuration;
using Server.withWebServer.Security;
using ServerCore;
using Shared;
using Shared.ControlPlane;
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
    // ---- Role ----
    enum ServerRole { Town, Game }

    // ---- Runtime objects ----
    static readonly Listener _clientListener = new Listener();
    static DomainWorker _gameWorker;
    static DomainWorker _townWorker;

    static ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient? _cp;
    static ControlPlaneClientOptions _cpOpt;
    public static ControlPlaneClient CP;


    // ---- Config-derived ----
    static ServerRole _role = ServerRole.Game;

    static string _serverId = "";
    static string _publicHost = "";
    static int _publicPort = 0;

    static string _bindHost = "";
    static int _bindPort = 0;

    static int _tickRate = 0;
    static int _cap = 0;
    static int _hbSec = 0;

    static void Main(string[] args)
    {
        HookProcessExit();

        // 1) Config
        var cfg = BuildConfig();
        AppRef.Cfg = cfg;

        // 2) Parse Role/Args overlay
        LoadServerConfig(cfg, args);

        // 3) Init subsystems (Role에 따라 다르게)
        // RedisAuth는 "기존 유지" 목적이므로, 일단 Game만 Init하도록 분기
        if (_role == ServerRole.Game)
            InitRedis(cfg);

        InitJwt(cfg);
        InitControlPlane(cfg);

        // 4) CP register + heartbeat (Role별로 Type 다르게)
        RegisterToControlPlaneAsync().GetAwaiter().GetResult();
        StartControlPlaneHeartbeat();

        // 5) Listener (Role별 포트로 bind)
        StartListener();
        SessionSweeper.Start();

        // 6) Content / Workers (Role별로 필요한 것만)
        // Content는 Game에서만 필요하면 Game에서만 Init (Town은 필요없으면 안 함)
        if (_role == ServerRole.Game)
            InitContentStore(cfg);

        StartWorkersByRole();

        // 7) Run
        Console.WriteLine($"[{_role}] Server started. Press ENTER to exit...");
        Console.ReadLine();

        Shutdown();
    }

    // ------------------------------
    // Lifecycle
    // ------------------------------
    static void HookProcessExit()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, __) =>
        {
            try { AppRef.Cts.Cancel(); } catch { }
            try { LogManager.Instance.Shutdown(); } catch { }
        };

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AppRef.Cts.Cancel();
        };
    }

    static IConfiguration BuildConfig()
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        StartupEcho.Dump("Server", cfg);
        return cfg;
    }

    // args 우선순위:
    // --role Town|Game
    // --serverId xxx
    // --port 13211
    // --bindHost 0.0.0.0
    // --publicHost 1.2.3.4
    static void LoadServerConfig(IConfiguration cfg, string[] args)
    {
        // 1) 기본은 config
        _serverId = cfg["GameServer:Id"] ?? _serverId;
        _bindHost = cfg["GameServer:BindHost"] ?? _bindHost;
        _bindPort = ParseInt(cfg["GameServer:Port"], _bindPort);

        _publicHost = cfg["GameServer:PublicHost"] ?? GuessLocalIPv4() ?? _publicHost;
        _publicPort = _bindPort;

        _tickRate = ParseInt(cfg["GameServer:TickRate"], _tickRate);
        _cap = ParseInt(cfg["GameServer:Cap"], _cap);
        _hbSec = ParseInt(cfg["GameServer:HeartbeatSec"], _hbSec);

        // 2) args로 덮어쓰기
        _role = ParseRole(args, defaultRole: ServerRole.Game);

        var argServerId = GetArgValue(args, "--serverId");
        if (!string.IsNullOrWhiteSpace(argServerId))
            _serverId = argServerId;

        var argPort = GetArgValue(args, "--port");
        if (int.TryParse(argPort, out var p))
        {
            _bindPort = p;
            _publicPort = p;
        }

        var argBindHost = GetArgValue(args, "--bindHost");
        if (!string.IsNullOrWhiteSpace(argBindHost))
            _bindHost = argBindHost;

        var argPublicHost = GetArgValue(args, "--publicHost");
        if (!string.IsNullOrWhiteSpace(argPublicHost))
            _publicHost = argPublicHost;

        // 3) Echo
        Console.WriteLine($"[BOOT] role={_role} id={_serverId} bind={_bindHost}:{_bindPort} public={_publicHost}:{_publicPort} tickRate={_tickRate} cap={_cap}");
    }

    static void InitRedis(IConfiguration cfg)
    {
        var redisConn = cfg["ConnectionStrings:Redis"] ?? "127.0.0.1:6379,abortConnect=False";
        try
        {
            Console.WriteLine($"[REDIS] {redisConn}");
            RedisAuth.InitAsync(redisConn, _serverId).GetAwaiter().GetResult();

            // ExpectedGsKey 형태 일관화 (gs:{id})
            RedisAuth.ExpectedGsKey = $"gs:{_serverId}";

            RedisAuth.DB.Ping();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[REDIS] init failed: {ex.Message}");
            throw;
        }
    }

    static void InitJwt(IConfiguration cfg)
    {
        AppRef.Jwt = new JwtService(cfg);
    }

    static void InitControlPlane(IConfiguration cfg)
    {
        _cpOpt = new ControlPlaneClientOptions
        {
            Address = cfg["ControlPlane:Address"] ?? "http://127.0.0.1:50051",
            Secret = cfg["ControlPlane:Secret"] ?? "CHANGE_ME",
        };

        var invoker = GrpcInvokerFactory.CreateControlPlaneInvoker(_cpOpt);
        _cp = new ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient(invoker);

        CP = new ControlPlaneClient(_cp, _role.ToString());
    }

    static void StartListener()
    {
        var endPoint = BuildBindEndPoint(_bindHost, _bindPort);

        try
        {
            _clientListener.Init(endPoint, () => SessionManager.Instance.Generate<ClientSession>());
            LogManager.Instance.LogInfo("Program",
                $"[{_role} Server Start] bind={endPoint} public={_publicHost}:{_publicPort} id={_serverId}");

            Console.WriteLine($"[{_role}] Listening on {endPoint} (public {_publicHost}:{_publicPort})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LISTENER] bind failed: {ex.Message}");
            throw;
        }
    }

    static void InitContentStore(IConfiguration cfg)
    {
        // TODO: publish시 경로 문제 -> config로 빼기 추천
        var baseDir = "D:\\Git\\Server\\RhythmRPG\\RhythmRPG\\Server\\GameServer\\Content";

        ContentStore.Init(
            skillsDir: Path.Combine(baseDir, "Skill", "Json"),
            patternsDir: Path.Combine(baseDir, "Pattern", "Json"),
            mapsDir: Path.Combine(baseDir, "Map", "Json")
        );
    }

    static void StartWorkersByRole()
    {
        //  Role에 따라 필요한 worker만 켠다.
        if (_role == ServerRole.Game)
        {
            _gameWorker = new DomainWorker(
                name: "GameWorker",
                snapshotGetter: GameManager.GetUpdatablesSnapshot,
                tickMs: 15);
            _gameWorker.Start();
            Console.WriteLine("[WORKER] GameWorker started");
        }
        else if (_role == ServerRole.Town)
        {
            _townWorker = new DomainWorker(
                name: "TownWorker",
                snapshotGetter: TownManager.GetUpdatablesSnapshot,
                tickMs: 100);
            _townWorker.Start();
            Console.WriteLine("[WORKER] TownWorker started");
        }
        else
        {
            throw new Exception("none Worker");
        }
    }
    static void Shutdown()
    {
        _townWorker?.Dispose();
        _gameWorker?.Dispose();
        try { Task.Delay(200).Wait(); } catch { }
    }

    // ------------------------------
    // ControlPlane
    // ------------------------------
    static ServerType RoleToServerType(ServerRole role)
        => role == ServerRole.Town ? ServerType.Town : ServerType.Game;

    static async Task RegisterToControlPlaneAsync()
    {
        if (_cp == null) throw new InvalidOperationException("CP not initialized.");

        ServerType type = RoleToServerType(_role);

        var req = new RegisterServerRequest
        {
            ServerId = _serverId,
            Type = type,
            Endpoint = new ServerEndpoint { Host = _publicHost, Port = _publicPort },
            Capacity = _cap,
            Region = "local",
            BuildVersion = "1"
        };

        var resp = await _cp.RegisterServerAsync(req);
        if (!resp.Ok)
            throw new Exception($"[CP] Register failed: {resp.Error?.Code} {resp.Error?.Message}");

        Console.WriteLine($"[CP] Registered ok now={resp.ServerNowMs} type={type}");
    }

    static void StartControlPlaneHeartbeat()
    {
        _ = ControlPlaneHeartbeatLoopAsync(AppRef.Cts.Token);
        Console.WriteLine($"[CP] Heartbeat started id={_serverId} type={RoleToServerType(_role)}");
    }

    static async Task ControlPlaneHeartbeatLoopAsync(CancellationToken ct)
    {
        if (_cp == null) throw new InvalidOperationException("CP not initialized.");

        int hbSec = ParseInt(AppRef.Cfg["ControlPlane:HeartbeatSec"], _hbSec);
        var type = RoleToServerType(_role);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var resp = await _cp.HeartbeatAsync(new HeartbeatRequest
                {
                    ServerId = _serverId,
                    Type = type,
                    Load = 0,
                    CurrentSessions = SessionManager.Instance.Count
                });

                if (!resp.Ok)
                {
                    LogManager.Instance.LogError("CPHeartbeat",
                        $"fail code={resp.Error?.Code} msg={resp.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError("CPHeartbeat", ex.ToString());
            }

            try { await Task.Delay(TimeSpan.FromSeconds(hbSec), ct); }
            catch { }
        }
    }


    // VerifyTicket 일반화 (Town/Game 공용)
    public static (bool ok, string uid) VerifyTicket(string tid, TicketTarget expectedTarget)
    {
        if (_cp == null) throw new InvalidOperationException("CP not initialized.");

        var resp = _cp.VerifyTicket(new VerifyTicketRequest
        {
            TicketId = tid,
            ExpectedTarget = expectedTarget,
            VerifierServerId = _serverId
        });

        if (!resp.Ok)
            return (false, "");

        return (true, resp.Uid);
    }

    // ------------------------------
    // Args parsing
    // ------------------------------
    static ServerRole ParseRole(string[] args, ServerRole defaultRole)
    {
        // --role Town / --role=Town
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--role", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (Enum.TryParse<ServerRole>(args[i + 1], true, out var r)) return r;
            }
            else if (args[i].StartsWith("--role=", StringComparison.OrdinalIgnoreCase))
            {
                var v = args[i].Substring("--role=".Length);
                if (Enum.TryParse<ServerRole>(v, true, out var r)) return r;
            }
        }
        return defaultRole;
    }

    static string? GetArgValue(string[] args, string key)
    {
        // --key value / --key=value
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return (i + 1 < args.Length) ? args[i + 1] : null;

            var prefix = key + "=";
            if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return args[i].Substring(prefix.Length);
        }
        return null;
    }

    // ------------------------------
    // Utils
    // ------------------------------
    static int ParseInt(string s, int fallback)
        => int.TryParse(s, out var v) ? v : fallback;

    static string? GuessLocalIPv4()
    {
        try
        {
            var host = Dns.GetHostName();
            var ipHost = Dns.GetHostEntry(host);
            return ipHost.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString();
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

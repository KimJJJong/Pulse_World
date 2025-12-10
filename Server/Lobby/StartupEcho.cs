using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

static class StartupEcho
{
    public static void Dump(string appName, IConfiguration cfg)
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var baseDir = AppContext.BaseDirectory;

        // ───────────── App 기본값 ─────────────
        string redisRaw = cfg["App:Redis"] ?? "(missing)";
        string redisMasked = MaskRedis(redisRaw);

        string protoVer = cfg["App:Versioning:ProtoVer"] ?? "(n/a)";
        string minClient = cfg["App:Versioning:MinClientVersion"] ?? "(n/a)";
        string headerName = cfg["App:Versioning:HeaderName"] ?? "(n/a)";

        // ───────────── RateLimit ─────────────
        string createRoom = cfg["App:RateLimit:CreateRoomPerMin"] ?? "(n/a)";
        string joinRoom = cfg["App:RateLimit:JoinPerMin"] ?? "(n/a)";
        string readyToggle = cfg["App:RateLimit:ReadyTogglePerSec"] ?? "(n/a)";
        string globalRps = cfg["App:RateLimit:GlobalRpsPerIp"] ?? "(n/a)";

        // ───────────── GameServer ─────────────
        string gsMode = cfg["App:GameServer:Mode"] ?? "(Static)";
        string gsId = cfg["App:GameServer:Static:Id"] ?? "(n/a)";
        string gsHost = cfg["App:GameServer:Static:PublicHost"] ?? "(n/a)";
        string gsPort = cfg["App:GameServer:Static:Port"] ?? "(n/a)";
        string gsTick = cfg["App:GameServer:Static:TickRate"] ?? "(n/a)";

        // ───────────── Auth / Ticket ─────────────
        string alg = cfg["Auth:Ticket:Algorithm"] ?? "RS256";
        string issuer = cfg["Auth:Ticket:Issuer"] ?? "(n/a)";
        string aud = cfg["Auth:Ticket:Audience"] ?? "(n/a)";
        string priv = cfg["Auth:Ticket:PrivateKeyPemPath"] ?? "";
        string pub = cfg["Auth:Ticket:PublicKeyPemPath"] ?? "";
        string privStat = FileStat(priv);
        string pubStat = FileStat(pub);
        bool roomSecretSet = !string.IsNullOrWhiteSpace(cfg["Auth:RoomSecret"]);

        string googleClientId = cfg["Auth:GoogleAuth:ClientId"] ?? "(missing)";
        string googleSecret = cfg["Auth:GoogleAuth:ClientSecret"] ?? "(missing)";
        string googleRedirect = cfg["Auth:GoogleAuth:RedirectUri"] ?? "(missing)";
        bool secretSet = !string.IsNullOrWhiteSpace(googleSecret);

        // ───────────── DB 연결 ─────────────
        string pg = cfg.GetConnectionString("AuthDb") ?? "(missing)";
        string pgMasked = MaskPg(pg);


        // ───────────── 출력 시작 ─────────────
        Log($"========== [{appName}] Startup Config ==========");
        Log($"Env            : {env}");
        Log($"BaseDir        : {baseDir}");
        foreach (var f in Directory.GetFiles(baseDir, "appsettings*.json"))
            Log($"- found config : {Path.GetFileName(f)}");

        Log($"-- Redis --------------------------------------");
        Log($"App:Redis      : {redisMasked}");

        Log($"-- PostgreSQL -------------------------------");
        Log($"AuthDb         : {pgMasked}");

        Log($"-- GameServer -------------------------------");
        Log($"Mode/Id/Host   : {gsMode} / {gsId} / {gsHost}");
        Log($"Port/TickRate  : {gsPort} / {gsTick}");

        Log($"-- Auth.Ticket ------------------------------");
        Log($"Algorithm      : {alg}");
        Log($"Issuer/Audience: {issuer} / {aud}");
        Log($"PrivateKeyPath : {privStat}");
        Log($"PublicKeyPath  : {pubStat}");
        Log($"RoomSecret set : {roomSecretSet}");

        Log($"-- Auth.GoogleAuth --------------------------");
        Log($"ClientId       : {MaskGoogleClientId(googleClientId)}");
        Log($"RedirectUri    : {googleRedirect}");
        Log($"Secret set     : {secretSet}");

        if (googleClientId == "(missing)" || googleRedirect == "(missing)")
            Log("( ! ) GoogleAuth configuration missing or incomplete");

        Log($"-- Versioning -------------------------------");
        Log($"ProtoVer       : {protoVer}");
        Log($"MinClientVer   : {minClient}");
        Log($"HeaderName     : {headerName}");

        Log($"-- RateLimit -------------------------------");
        Log($"CreateRoom/min : {createRoom}");
        Log($"JoinRoom/min   : {joinRoom}");
        Log($"ReadyToggle/s  : {readyToggle}");
        Log($"GlobalRps/IP   : {globalRps}");

        Log($"-- Providers -------------------------------");
        if (cfg is IConfigurationRoot root)
            foreach (var p in root.Providers) Log($"Provider       : {p}");

        Log($"================================================");
    }

    // ────────────────────────────────────────────────
    static string MaskGoogleClientId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "(missing)";
        if(id.Length <= 10) return id;
        return id[..6] + "..." + id[^8..]; 
    } 
    static string MaskRedis(string cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return "(missing)";
        return Regex.Replace(cs, @"(?i)(password\s*=\s*)([^,;]+)", "$1********");
    }

    static string MaskPg(string cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return "(missing)";
        return Regex.Replace(cs, @"(?i)(Password\s*=\s*)([^;]+)", "$1********");
    }

    static string FileStat(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "(none)";
        return File.Exists(path) ? $"{path} [OK]" : $"{path} [MISSING]";
    }

    static void Log(string msg)
    {
        Console.WriteLine(msg);
        //try { LogManager.Instance?.LogInfo("Startup", msg); } catch { /* ignore */ }
    }
}

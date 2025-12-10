namespace Lobby.Api.Config;

public sealed class AppOptions
{
    public string Redis { get; set; } = "";
    public TicketOptions Ticket { get; set; } = new();
    public int MatchTtlSeconds { get; set; } = 900;

    public VersioningOptions Versioning { get; set; } = new();
    public RateLimitOptions RateLimit { get; set; } = new();
    public class VersioningOptions
    {
        public string MinClientVersion { get; set; } = "1.0.0"; // 이보다 낮으면 거절
        public string HeaderName { get; set; } = "X-Client-Version"; // HTTP 헤더명
        public int ProtoVer { get; set; } = 1;
    }
    public class RateLimitOptions
    {
        public int CreateRoomPerMin { get; set; } = 10;
        public int JoinPerMin { get; set; } = 30;
        public int ReadyTogglePerSec { get; set; } = 10; // WS 토글 보호
        public int GlobalRpsPerIp { get; set; } = 60;
    }

    public sealed class TicketOptions
    {
        public string Issuer { get; set; } = "LobbyAuth";
        public string Audience { get; set; } = "GameServer";
        public string PrivateKeyPemPath { get; set; } = "keys/lobby_private.pem";
        public string PublicKeyPemPath { get; set; } = "keys/lobby_public.pem";

        public int TtlSeconds { get; set; } = 120;
    }
    public sealed class GameServerOptions
    {
        public string Mode { get; set; } = "Static";
        public StaticOpt Static { get; set; } = new();
        public RegistryOpt Registry { get; set; } = new();

        public sealed class StaticOpt
        {
            public string Id { get; set; } = "gs1"; 
            public string PublicHost { get; set; } = "127.0.0.1"; 
            public int Port { get; set; } = 13221; 
            public int TickRate { get; set; } = 30;
        }
        public sealed class RegistryOpt 
        { 
            public string Prefix { get; set; } = "gs:"; 
            public string Region { get; set; } = ""; 
            public string Strategy { get; set; } = "leastLoad"; 
            public int HeartbeatTtlSec { get; set; } = 5; 
        }
    }


}

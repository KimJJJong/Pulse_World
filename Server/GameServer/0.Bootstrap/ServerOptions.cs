namespace Server.Bootstrap;

public sealed class ServerOptions
{
    public string ServerId { get; set; } = "";
    public string Region { get; set; } = "local";
    public string BuildVersion { get; set; } = "1";
    public int Capacity { get; set; } = 0;
    public int HeartbeatSec { get; set; } = 5;
    public int PresenceLeaseTtlSeconds { get; set; } = 0;
    public RoleOptions Role { get; set; } = new();
    public EndpointOptions Bind { get; set; } = new();
    public EndpointOptions Public { get; set; } = new();
    public ContentOptions Content { get; init; } = new();


}

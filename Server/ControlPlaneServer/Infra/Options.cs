namespace ControlPlane.Infra;

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string KeyPrefix { get; set; } = "cp:";
}

public sealed class TicketOptions
{
    public int DefaultTtlSeconds { get; set; } = 30;
}

public sealed class RegistryOptions
{
    public int HeartbeatTtlSeconds { get; set; } = 10;
}

public sealed class SecurityOptions
{
    public string ServiceSharedSecret { get; set; } = "";
}

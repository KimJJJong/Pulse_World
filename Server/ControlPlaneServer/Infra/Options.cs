namespace ControlPlaneServer.Infra;

public sealed class ControlPlaneOptions
{
    public string Secret { get; set; } = "CHANGE_ME";
    public string Prefix { get; set; } = "cp:";

    public int LeaseTtlSeconds { get; set; } = 30;
    public int TicketDefaultTtlSeconds { get; set; } = 15;
    public int ReservationTtlSeconds { get; set; } = 20;
    public int TransitionTtlSeconds { get; set; } = 15;
}

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public int Database { get; set; } = 0;
}

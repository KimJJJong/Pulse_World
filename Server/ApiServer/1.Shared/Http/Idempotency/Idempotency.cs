namespace ApiServer.Shared.Http.Idempotency;

public sealed class IdempotencyEntry
{
    public bool Completed { get; set; }
    public int StatusCode { get; set; }
    public string ContentType { get; set; } = "application/json";
    public byte[] Body { get; set; } = Array.Empty<byte>();
    public DateTimeOffset ExpireAt { get; set; }
}

namespace ApiServer.Shared.Http.Idempotency;

public interface IIdempotencyStore
{
    /// <summary>
    /// 이미 완료된 결과가 있으면 반환.
    /// 아직 없으면 "진행중 락"을 잡고 null 반환.
    /// 이미 진행중이면 "in-flight" 신호 반환.
    /// </summary>
    Task<(IdempotencyEntry? entry, bool inFlight)> TryBeginAsync(string key, TimeSpan ttl, CancellationToken ct);

    /// <summary>
    /// 완료 결과 저장.
    /// </summary>
    Task CompleteAsync(string key, IdempotencyEntry entry, CancellationToken ct);

    /// <summary>
    /// 실패/취소 시 락 해제(다시 시도 가능)
    /// </summary>
    Task AbandonAsync(string key, CancellationToken ct);
}

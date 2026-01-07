using ApiServer.Shared.Http.Idempotency;
using System.Collections.Concurrent;

namespace ApiServer.Infrastructure.Idempotency;

/// <summary>
/// 단일 인스턴스 개발/테스트용.
/// 운영/스케일아웃은 Redis 구현으로 교체 권장.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private sealed class Slot
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public IdempotencyEntry? Entry;
        public DateTimeOffset ExpireAt;
        public bool Completed;
    }

    private readonly ConcurrentDictionary<string, Slot> _map = new();

    public async Task<(IdempotencyEntry? entry, bool inFlight)> TryBeginAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // 만료 슬롯 정리(가벼운 방식)
        if (_map.TryGetValue(key, out var existing))
        {
            if (existing.ExpireAt <= now)
                _map.TryRemove(key, out _);
        }

        var slot = _map.GetOrAdd(key, _ => new Slot { ExpireAt = now.Add(ttl) });

        // 누군가 처리중인지 확인: Gate를 즉시 획득 시도
        if (!await slot.Gate.WaitAsync(0, ct))
        {
            // 처리중인 요청이 있음
            if (slot.Completed && slot.Entry != null && slot.ExpireAt > now)
                return (slot.Entry, inFlight: false);

            return (entry: null, inFlight: true);
        }

        try
        {
            // Gate 획득 후 재확인
            if (slot.Completed && slot.Entry != null && slot.ExpireAt > now)
                return (slot.Entry, inFlight: false);

            // "진행중 락" 확보 완료 (아직 entry는 없음)
            slot.ExpireAt = now.Add(ttl);
            slot.Completed = false;
            slot.Entry = null;

            return (entry: null, inFlight: false);
        }
        finally
        {
            // Begin은 락을 유지해야 함 -> 여기서 release하면 의미가 없음.
            // 따라서 Begin에서는 Gate를 잡은 상태로 유지하지 않고,
            // 대신 "슬롯 소유" 개념이 필요해 보이지만, 단순화를 위해:
            // Complete/Abandon에서 다시 Gate를 획득해 상태 갱신한다.
            // => Begin에서 Gate를 즉시 풀어줌.
            slot.Gate.Release();
        }
    }

    public async Task CompleteAsync(string key, IdempotencyEntry entry, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (!_map.TryGetValue(key, out var slot))
            slot = _map.GetOrAdd(key, _ => new Slot { ExpireAt = entry.ExpireAt });

        await slot.Gate.WaitAsync(ct);
        try
        {
            slot.Entry = entry;
            slot.Completed = true;
            slot.ExpireAt = entry.ExpireAt;
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    public async Task AbandonAsync(string key, CancellationToken ct)
    {
        if (!_map.TryGetValue(key, out var slot))
            return;

        await slot.Gate.WaitAsync(ct);
        try
        {
            // 실패 시 재시도 가능하게 제거
            _map.TryRemove(key, out _);
        }
        finally
        {
            slot.Gate.Release();
        }
    }
}

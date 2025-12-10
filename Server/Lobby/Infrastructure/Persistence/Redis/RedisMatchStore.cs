using StackExchange.Redis;
using Lobby.Domain.Auth.Interface;

namespace Lobby.Infrastructure.Persistence.Redis
{
    public sealed class RedisMatchStore : IMatchStore
    {
        private readonly IDatabase _db;
        private readonly TimeSpan _defaultTtl;

        // 필요 시 기본 TTL 주입 (없으면 30분)
        public RedisMatchStore(IDatabase db, IConfiguration cfg)
        {
            _db = db;
            var ttlSec = cfg.GetSection("App")?.GetValue<int?>("MatchTtlSeconds") ?? 1800;
            _defaultTtl = TimeSpan.FromSeconds(ttlSec);
        }

        private static string Key(string matchId) => $"match:{matchId}";

        public async Task CreateAsync(string matchId, string gsId, string roomId, string uidA, string uidB, TimeSpan? ttl = null)
        {
            var key = Key(matchId);

            var entries = new HashEntry[]
            {
                new("gsId", gsId),
                new("roomId", roomId),
                new("uidA", uidA),
                new("uidB", uidB),
                new("status", "created"),
                new("ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            };

            // HSET 다중 필드
            await _db.HashSetAsync(key, entries);

            // TTL 설정
            await _db.KeyExpireAsync(key, ttl ?? _defaultTtl);
        }

        public async Task SetFieldsAsync(string matchId, IDictionary<string, string> fields, TimeSpan? ttl = null)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException("matchId is required.", nameof(matchId));
            if (fields is null || fields.Count == 0)
                return;

            var key = Key(matchId);

            // Dictionary -> HashEntry[]
            var entries = new List<HashEntry>(fields.Count);
            foreach (var kv in fields)
            {
                if (kv.Key is null) continue;
                entries.Add(new HashEntry(kv.Key, kv.Value ?? string.Empty));
            }

            // HSET 다중 필드
            await _db.HashSetAsync(key, entries.ToArray());

            // 필요 시 TTL 갱신
            if (ttl.HasValue)
                await _db.KeyExpireAsync(key, ttl.Value);
        }

        public async Task<Dictionary<string, string>> GetAllAsync(string matchId)
        {
            var key = Key(matchId);
            var all = await _db.HashGetAllAsync(key);
            return all.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
        }

        public async Task<bool> ExistsAsync(string matchId)
        {
            var key = Key(matchId);
            return await _db.KeyExistsAsync(key);
        }
    }
}

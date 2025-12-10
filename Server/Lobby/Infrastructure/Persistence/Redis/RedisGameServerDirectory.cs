using StackExchange.Redis;

namespace Lobby.Infrastructure.Persistence.Redis;

public sealed class RedisGameServerDirectory
{
    private readonly IDatabase _db;
    private readonly LoadedLuaScript _reserveScript;

    public RedisGameServerDirectory(IConnectionMultiplexer mux, IDatabase db)
    {
        _db = db;
        var prepared = LuaScript.Prepare(RedisLua.ReserveGsSlot);
        _reserveScript = prepared.Load(mux.GetServer(mux.GetEndPoints()[0]));
    }

    public record GsInfo(string Id, string Host, int Port, int TickRate);

    public async Task<GsInfo> PickAndReserveAsync()
    {
        var alive = await _db.SetMembersAsync("gs:alive");
        foreach (var idVal in alive)
        {
            var id = (string)idVal!;
            var key = $"gs:{id}";
            var remain = (int)(long)await _db.ScriptEvaluateAsync(
                RedisLua.ReserveGsSlot,
                new RedisKey[] { key },
                Array.Empty<RedisValue>());

            if (remain >= 0) // 예약 성공
            {
                var h = await _db.HashGetAllAsync(key);
                var map = h.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
                return new GsInfo(id, map["host"], int.Parse(map["port"]), int.Parse(map["tickRate"]));
            }
        }
        throw new InvalidOperationException("no_capacity");
    }

    // 실패/타임아웃 시 used-1 롤백 (선택)
    public Task ReleaseAsync(string gsId)
        => _db.HashDecrementAsync($"gs:{gsId}", "used");
}

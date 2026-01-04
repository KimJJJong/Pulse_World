using ControlPlaneServer.Infra;
using StackExchange.Redis;

namespace ControlPlaneServer.Domain.Rooms;

public sealed class RoomService
{
    private readonly RedisStore _redis;

    public RoomService(RedisStore redis)
    {
        _redis = redis;
    }

    // 멱등 생성: 이미 있으면 OK(기존 값 반환도 가능)
    public async Task<(bool created, RoomRecord record)> CreateIfAbsentAsync(
        string serverId, string roomId, string uid, string map, int maxPlayers, long nowMs)
    {
        string key = _redis.KeyRoom(serverId, roomId);

        // Hash가 이미 있으면 "이미 생성됨"으로 간주
        if (await _redis.Db.KeyExistsAsync(key))
        {
            var v = await _redis.Db.HashGetAsync(key, new RedisValue[] { "uid", "map", "maxPlayers", "createdAtMs" });
            var rec = new RoomRecord(
                ServerId: serverId,
                RoomId : roomId,
                Uid: (string)v[0],
                Map: (string)v[1],
                MaxPlayers: (int)v[2],
                CreatedAtMs: long.TryParse(v[3], out var ca) ? ca : 0
            );
            return (false, rec);
        }

        var entries = new HashEntry[]
        {
            new("uid", uid),
            new("map", map ?? ""),
            new("maxPlayers", maxPlayers),
            new("createdAtMs", nowMs.ToString())
        };

        // 경쟁 조건 방지: SETNX 느낌으로 Hash는 직접 안 되니까,
        // 여기서는 KeyExists 검사 후 HashSet으로 가되, 동시에 2번 만들면 "같은 결과"라 멱등 OK.
        await _redis.Db.HashSetAsync(key, entries);

        var created = new RoomRecord(serverId, roomId, uid, map ?? "", maxPlayers, nowMs);
        return (true, created);
    }
}

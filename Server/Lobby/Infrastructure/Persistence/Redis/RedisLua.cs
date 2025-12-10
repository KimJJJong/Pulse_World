namespace Lobby.Infrastructure.Persistence.Redis;

public static class RedisLua
{
    // gs:{id} 해시에서 cap/used를 읽어 used+1 (빈자리 없으면 0, 없으면 -1 반환)
    public const string ReserveGsSlot = @"
local key = KEYS[1]
local cap = tonumber(redis.call('HGET', key, 'cap') or '-1')
local used = tonumber(redis.call('HGET', key, 'used') or '-1')
if cap < 0 or used < 0 then return -1 end
if used >= cap then return 0 end
redis.call('HINCRBY', key, 'used', 1)
return cap - (used + 1)
";
}

using StackExchange.Redis;
using System.Threading.Tasks;

public static class RedisAuth
{
    public static IDatabase DB = null!;
    public static LoadedLuaScript VerifyScript = null!;
    public static string VerifyScriptText = "";
    public static string ExpectedGsKey = "";
    public static string GsKeyPrefix = "gs:"; // gs:{id}

    public static async Task InitAsync(string conn, string gsId)
    {
        ConnectionMultiplexer mux = await ConnectionMultiplexer.ConnectAsync(conn);
        DB = mux.GetDatabase();

        //lua Lang
        VerifyScriptText = @"
-- KEYS:
-- 1 = match:{matchId}
-- 2 = match:{matchId}:conn
-- 3 = ticket:{matchId}:nonce:{nonce}
-- 4 = ticket:{matchId}:black:{jti or _none}
-- ARGV:
-- 1 = expectedGsKey
-- 2 = side
-- 3 = uid
-- 4 = connTtlSec
-- 5 = nonceTtlSec
-- 6 = allowRejoin
local matchKey   = KEYS[1]
local connKey    = KEYS[2]
local nonceKey   = KEYS[3]
local blackKey   = KEYS[4]
local expectedGs = ARGV[1]
local side       = ARGV[2]
local uid        = ARGV[3]
local connTtl    = tonumber(ARGV[4]) or 120
local nonceTtl   = tonumber(ARGV[5]) or 900
local allowRe    = tostring(ARGV[6]) == '1'

if string.sub(blackKey, -5) ~= '_none' then
  if redis.call('EXISTS', blackKey) == 1 then
    return 'err=blacklisted'
  end
end

if redis.call('EXISTS', matchKey) ~= 1 then
  return 'err=no_match'
end

local gsId = redis.call('HGET', matchKey, 'gsId')
if not gsId or gsId ~= expectedGs then
  return 'err=wrong_gs'
end

local seatField = (side == 'A') and 'uidA' or 'uidB'
local seatUid = redis.call('HGET', matchKey, seatField)
if not seatUid or seatUid ~= uid then
  return 'err=seat_mismatch'
end

local setOk = redis.call('SETNX', nonceKey, '1')
if setOk == 0 then
  return 'err=replay'
end
redis.call('EXPIRE', nonceKey, nonceTtl)

local prev = redis.call('HGET', connKey, uid)
if prev then
  if allowRe then
    redis.call('HSET', connKey, uid, 'joined')
    redis.call('EXPIRE', connKey, connTtl)
    return 'ok=rejoin'
  else
    return 'err=already_connected'
  end
else
  redis.call('HSET', connKey, uid, 'joined')
  redis.call('EXPIRE', connKey, connTtl)
  return 'ok=claimed'
end
";
        var prepared = LuaScript.Prepare(VerifyScriptText);
        VerifyScript = prepared.Load(mux.GetServer(mux.GetEndPoints()[0]));

        ExpectedGsKey = $"{GsKeyPrefix}{gsId}";

        await DB.PingAsync();
    }
}

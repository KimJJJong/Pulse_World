// Shared/RedisScripts.cs
namespace Shared;
public static class RedisScripts
{
    public const string VerifyAndClaim = @"
local matchKey=KEYS[1]; local connKey=KEYS[2]; local nonceKey=KEYS[3]; local blackKey=KEYS[4];
local expectedGs=ARGV[1]; local side=ARGV[2]; local uid=ARGV[3]; local nonceTtl=tonumber(ARGV[4]); local connTtl=tonumber(ARGV[5]); local allowSame=(ARGV[6]=='1');
if blackKey and blackKey ~= '' then if redis.call('EXISTS',blackKey)==1 then return {err='blacklisted'} end end
if redis.call('EXISTS',matchKey)==0 then return {err='no_match'} end
local gsId=redis.call('HGET',matchKey,'gsId'); if gsId~=expectedGs then return {err='wrong_server'} end
local expectedUidField = (side=='A') and 'uidA' or 'uidB'
local expectedUid = redis.call('HGET',matchKey,expectedUidField)
if expectedUid~=uid then return {err='uid_mismatch'} end
local setOk=redis.call('SETNX',nonceKey,'1'); if setOk==0 then return {err='replay_or_reuse'} end
redis.call('EXPIRE',nonceKey,nonceTtl)
local cur=redis.call('HGET',connKey,side)
if (not cur) or cur=='' then
  redis.call('HSET',connKey,side,uid); redis.call('EXPIRE',connKey,connTtl); return {ok='claimed'}
end
if cur==uid then
  if connTtl and connTtl>0 then redis.call('EXPIRE',connKey,connTtl) end; return {ok='rejoin'}
end
return {err='slot_taken'}
";
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

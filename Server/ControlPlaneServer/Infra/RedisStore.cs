using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ControlPlaneServer.Infra;

public sealed class RedisStore
{
    private readonly ConnectionMultiplexer _mux;
    private readonly IDatabase _db;
    private readonly ControlPlaneOptions _opt;

    // Lua Scripts
    //private readonly LuaScript _luaReserveOrConsume;
    //private readonly LuaScript _luaAttachPresence;
    //private readonly LuaScript _luaRenewLease;

    public RedisStore(IOptions<RedisOptions> redisOpt, IOptions<ControlPlaneOptions> cpOpt)
    {
        _opt = cpOpt.Value;

        _mux = ConnectionMultiplexer.Connect(redisOpt.Value.ConnectionString);
        _db = _mux.GetDatabase(redisOpt.Value.Database);

        //_luaReserveOrConsume = LuaScript.Load(LuaTexts.ReserveOrConsumeTicket);
        //_luaAttachPresence = LuaScript.Prepare(LuaTexts.AttachPresence);
        //_luaRenewLease = LuaScript.Prepare(LuaTexts.RenewLease);
    }

    public IDatabase Db => _db;
    public string Prefix => _opt.Prefix;

    public string KeyTicket(string ticketId) => $"{Prefix}ticket:{ticketId}";
    public string KeyPresence(string uid) => $"{Prefix}presence:{uid}";
    public string KeyTransition(string uid) => $"{Prefix}transition:{uid}";
    public string KeyServersIndex(string type) => $"{Prefix}servers:{type}";
    public string KeyServer(string type, string serverId) => $"{Prefix}server:{type}:{serverId}";
    public string KeyReservation(string reservationId) => $"{Prefix}reservation:{reservationId}";
    public string KeyRoom(string serverId, string roomId) => $"{Prefix}room:{serverId}:{roomId}";
    public string KeyWaitingRoom(string roomId) => $"{Prefix}waiting_room:{roomId}";
    public string KeyWaitingRoomIndex() => $"{Prefix}waiting_rooms:index";

    // ----- Ticket: ReserveOrConsume -----
    // Return tuple: (ok:int, errCode:int, uid, key, issuedServerId, pinnedServerId, expireAtMs)
    public async Task<RedisResult> EvalReserveOrConsumeTicketAsync(string ticketKey, string expectedTarget, string verifierServerId, string connId, long nowMs)
    {
        return await _db.ScriptEvaluateAsync(
            LuaTexts.LUA_RESERVE_CONSUM_TICKET,
            keys: new RedisKey[] { ticketKey },
            values: new RedisValue[] { expectedTarget, verifierServerId, connId, nowMs }
        );
    }

    // ----- Presence: Attach (epoch++) -----
    // Return: (ok:int, errCode:int, newEpoch, prevState, prevServerId, prevConnId, prevEpoch)
    public async Task<RedisResult> EvalAttachPresenceAsync(string presenceKey, string newState, string newServerId, string newConnId, int leaseTtlSec, long nowMs)
    {
        return await _db.ScriptEvaluateAsync(
            LuaTexts.LUA_ATTACH_PRESENCE,
            keys: new RedisKey[] { presenceKey },
            values: new RedisValue[] { newState, newServerId, newConnId, leaseTtlSec, nowMs }
        );
    }

    // ----- Presence: Renew lease -----
    // Return: (ok:int, errCode:int)
    public async Task<RedisResult> EvalRenewLeaseAsync(string presenceKey, string serverId, string connId, long epoch, int leaseTtlSec, long nowMs)
    {
        return await _db.ScriptEvaluateAsync(
            LuaTexts.LUA_RENEW_LEASE,
            keys: new RedisKey[] { presenceKey },
            values: new RedisValue[] { serverId, connId, epoch, leaseTtlSec, nowMs }
        );
    }

    private static class LuaTexts
    {
        // ticketKey hash fields:
        // uid, target, key, issuedServerId, pinnedServerId(optional), used(0/1), expireAtMs
        public const string LUA_RESERVE_CONSUM_TICKET = @"
local ticketKey = KEYS[1]
local expectedTarget = ARGV[1]
local verifierServerId = ARGV[2]
local connId = ARGV[3]
local nowMs = tonumber(ARGV[4])

if redis.call('EXISTS', ticketKey) == 0 then
  return {0, 10, '', '', '', '', 0} -- NOT_FOUND
end

local target = redis.call('HGET', ticketKey, 'target')
local used = redis.call('HGET', ticketKey, 'used')
local expireAtMs = tonumber(redis.call('HGET', ticketKey, 'expireAtMs') or '0')
local pinnedServerId = redis.call('HGET', ticketKey, 'pinnedServerId') or ''
local uid = redis.call('HGET', ticketKey, 'uid') or ''
local key = redis.call('HGET', ticketKey, 'key') or ''
local issuedServerId = redis.call('HGET', ticketKey, 'issuedServerId') or ''

if expireAtMs <= nowMs then
  return {0, 11, '', '', '', pinnedServerId, expireAtMs} -- EXPIRED
end

if target ~= expectedTarget then
  return {0, 13, '', '', '', pinnedServerId, expireAtMs} -- TARGET_MISMATCH
end

if pinnedServerId ~= '' and pinnedServerId ~= verifierServerId then
  return {0, 14, '', '', issuedServerId, pinnedServerId, expireAtMs} -- PINNED_MISMATCH
end

if used == '1' then
  return {0, 12, '', '', issuedServerId, pinnedServerId, expireAtMs} -- ALREADY_USED
end

-- consume
redis.call('HSET', ticketKey, 'used', '1')
redis.call('HSET', ticketKey, 'boundConnId', connId)

return {1, 0, uid, key, issuedServerId, pinnedServerId, expireAtMs}
";

        // presenceKey hash fields:
        // state, serverId, connId, epoch, expAtMs
        // Attach: epoch++ and overwrite state/server/conn. Return previous snapshot too.
        public const string LUA_ATTACH_PRESENCE = @"
local presenceKey = KEYS[1]
local newState = ARGV[1]
local newServerId = ARGV[2]
local newConnId = ARGV[3]
local leaseTtlSec = tonumber(ARGV[4])
local nowMs = tonumber(ARGV[5])

local prevState = ''
local prevServerId = ''
local prevConnId = ''
local prevEpoch = 0
local newEpoch = 1

if redis.call('EXISTS', presenceKey) == 1 then
  prevState = redis.call('HGET', presenceKey, 'state') or ''
  prevServerId = redis.call('HGET', presenceKey, 'serverId') or ''
  prevConnId = redis.call('HGET', presenceKey, 'connId') or ''
  prevEpoch = tonumber(redis.call('HGET', presenceKey, 'epoch') or '0')
  newEpoch = prevEpoch + 1
end

local expAtMs = nowMs + leaseTtlSec * 1000

redis.call('HSET', presenceKey,
  'state', newState,
  'serverId', newServerId,
  'connId', newConnId,
  'epoch', tostring(newEpoch),
  'expAtMs', tostring(expAtMs)
)

redis.call('PEXPIRE', presenceKey, leaseTtlSec * 1000)

return {1, 0, newEpoch, prevState, prevServerId, prevConnId, prevEpoch}
";

        // Renew: only if serverId/connId/epoch match
        public const string LUA_RENEW_LEASE = @"
local presenceKey = KEYS[1]
local serverId = ARGV[1]
local connId = ARGV[2]
local epoch = tonumber(ARGV[3])
local leaseTtlSec = tonumber(ARGV[4])
local nowMs = tonumber(ARGV[5])

if redis.call('EXISTS', presenceKey) == 0 then
  return {0, 31} -- PRESENCE_NOT_FOUND
end

local curServerId = redis.call('HGET', presenceKey, 'serverId') or ''
local curConnId = redis.call('HGET', presenceKey, 'connId') or ''
local curEpoch = tonumber(redis.call('HGET', presenceKey, 'epoch') or '0')

if curServerId ~= serverId or curConnId ~= connId or curEpoch ~= epoch then
  return {0, 30} -- EPOCH_MISMATCH
end

local expAtMs = nowMs + leaseTtlSec * 1000
redis.call('HSET', presenceKey, 'expAtMs', tostring(expAtMs))
redis.call('PEXPIRE', presenceKey, leaseTtlSec * 1000)

return {1, 0}
";
    }
}

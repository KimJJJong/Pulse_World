using ControlPlane.Grpc.V1;
using ControlPlane.Infra;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace ControlPlane.Domain.Tickets;

public sealed class TicketService
{
    private readonly RedisStore _redis;
    private readonly TicketOptions _opt;

    public TicketService(RedisStore redis, IOptions<TicketOptions> opt)
    {
        _redis = redis;
        _opt = opt.Value;
    }

    // ticket:{tid} => HASH(uid,target,serverId,expMs,used,key)
    public async Task<TicketData> IssueAsync(
        string uid,
        TicketTarget target,
        string serverId,
        string key,
        int? ttlSeconds,
        long nowMs)
    {
        int ttl = ttlSeconds ?? _opt.DefaultTtlSeconds;
        long expMs = nowMs + ttl * 1000L;

        string tid = NewTid();
        string ticketKey = _redis.Key($"ticket:{tid}");

        var entries = new HashEntry[]
        {
            new("uid", uid ?? ""),
            new("target", (int)target),
            new("serverId", serverId ?? ""),
            new("expMs", expMs),
            new("used", 0),
            new("key", key ?? "")
        };

        await _redis.Db.HashSetAsync(ticketKey, entries);
        await _redis.Db.KeyExpireAsync(ticketKey, TimeSpan.FromSeconds(ttl));

        return new TicketData(tid, uid, target, serverId, expMs, key ?? "");
    }

    public async Task<VerifyConsumeResult> VerifyAndConsumeAsync(
        string tid,
        TicketTarget expectedTarget,
        long nowMs)
    {
        string ticketKey = _redis.Key($"ticket:{tid}");

        // 원자적으로:
        // - 존재 확인
        // - used==0 확인
        // - target 일치 확인
        // - expMs 유효 확인
        // - used=1 consume
        // - uid/serverId/key 반환
        const string lua = @"
local used = redis.call('HGET', KEYS[1], 'used')
if not used then return {0,'not_found'} end
if tonumber(used) ~= 0 then return {0,'already_used'} end

local target = redis.call('HGET', KEYS[1], 'target')
if not target then return {0,'not_found'} end
if tonumber(target) ~= tonumber(ARGV[1]) then return {0,'target_mismatch'} end

local expMs = redis.call('HGET', KEYS[1], 'expMs')
if expMs and tonumber(ARGV[2]) > tonumber(expMs) then return {0,'expired'} end

redis.call('HSET', KEYS[1], 'used', 1)

local uid = redis.call('HGET', KEYS[1], 'uid') or ''
local sid = redis.call('HGET', KEYS[1], 'serverId') or ''
local key = redis.call('HGET', KEYS[1], 'key') or ''
return {1, uid, sid, key}
";

        var raw = (RedisResult[])await _redis.Db.ScriptEvaluateAsync(
            lua,
            new RedisKey[] { ticketKey },
            new RedisValue[] { (int)expectedTarget, nowMs });

        int ok = (int)raw[0];
        if (ok == 0)
        {
            string reason = raw.Length > 1 ? raw[1].ToString() ?? "unknown" : "unknown";
            return reason switch
            {
                "not_found" => new(false, "", "", "", ErrorCode.TicketNotFound, "ticket not found"),
                "already_used" => new(false, "", "", "", ErrorCode.TicketAlreadyUsed, "ticket already used"),
                "target_mismatch" => new(false, "", "", "", ErrorCode.TicketTargetMismatch, "ticket target mismatch"),
                "expired" => new(false, "", "", "", ErrorCode.TicketExpired, "ticket expired"),
                _ => new(false, "", "", "", ErrorCode.Unspecified, $"verify failed: {reason}")
            };
        }

        return new VerifyConsumeResult(
            true,
            raw[1].ToString() ?? "",
            raw[2].ToString() ?? "",
            raw[3].ToString() ?? "",
            ErrorCode.Unspecified,
            "ok");
    }

    private static string NewTid()
    {
        Span<byte> b = stackalloc byte[16];
        RandomNumberGenerator.Fill(b);
        return Convert.ToHexString(b).ToLowerInvariant();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ApiServer.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ApiServer.Domain.GameResult;

public sealed class GameResultService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    private readonly RedisStore _redis;
    private readonly ILogger<GameResultService> _logger;

    public GameResultService(RedisStore redis, ILogger<GameResultService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<GameResultStoreResult> SaveAsync(GameResultReportRequest request, CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.RoomId))
            throw new ArgumentException("RoomId is required", nameof(request));

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string roomKey = _redis.KeyGameResult(request.RoomId);

        var existingJson = await _redis.Db.StringGetAsync(roomKey);
        if (existingJson.HasValue)
        {
            var existingRecord = DeserializeRecord(existingJson.ToString()) ?? CreateRecord(request, nowMs);
            _logger.LogInformation("[GameResult] Duplicate ignored room={RoomId}", request.RoomId);
            return new GameResultStoreResult(existingRecord, true, "duplicate");
        }

        var record = CreateRecord(request, nowMs);
        var json = JsonSerializer.Serialize(record, JsonOptions);
        var stored = await _redis.Db.StringSetAsync(roomKey, json, expiry: TimeSpan.FromDays(7), when: When.NotExists);
        if (!stored)
        {
            existingJson = await _redis.Db.StringGetAsync(roomKey);
            var existingRecord = DeserializeRecord(existingJson.ToString()) ?? record;
            _logger.LogInformation("[GameResult] Duplicate race ignored room={RoomId}", request.RoomId);
            return new GameResultStoreResult(existingRecord, true, "duplicate");
        }

        await _redis.Db.SetAddAsync(_redis.KeyGameResultIndex(), request.RoomId);
        await UpdateRewardLedgersAsync(record, request, nowMs);

        _logger.LogInformation(
            "[GameResult] Stored room={RoomId} clear={Clear} playTime={PlayTimeMs} damage={Damage} players={PlayerCount}",
            record.RoomId,
            record.IsClear,
            record.VerifiedPlayTimeMs,
            record.TotalDamage,
            record.PlayerUids.Length);

        return new GameResultStoreResult(record, false, "stored");
    }

    private async Task UpdateRewardLedgersAsync(GameResultRecord record, GameResultReportRequest request, long nowMs)
    {
        foreach (var uid in record.PlayerUids)
        {
            if (string.IsNullOrWhiteSpace(uid))
                continue;

            string ledgerKey = _redis.KeyGameRewardLedger(uid);
            await _redis.Db.HashIncrementAsync(ledgerKey, "ReportCount", 1);
            if (request.IsClear)
                await _redis.Db.HashIncrementAsync(ledgerKey, "ClearCount", 1);

            var entries = new[]
            {
                new HashEntry("LastRoomId", record.RoomId),
                new HashEntry("LastMapId", record.MapId),
                new HashEntry("LastHostUid", record.HostUid),
                new HashEntry("LastHostActorId", record.HostActorId),
                new HashEntry("LastIsClear", record.IsClear ? 1 : 0),
                new HashEntry("LastReportedPlayTimeMs", record.ReportedPlayTimeMs),
                new HashEntry("LastVerifiedPlayTimeMs", record.VerifiedPlayTimeMs),
                new HashEntry("LastTotalDamage", record.TotalDamage),
                new HashEntry("LastResultAtMs", nowMs),
            };

            await _redis.Db.HashSetAsync(ledgerKey, entries);
            await _redis.Db.KeyExpireAsync(ledgerKey, TimeSpan.FromDays(30));
        }
    }

    private static GameResultRecord CreateRecord(GameResultReportRequest request, long nowMs)
    {
        return new GameResultRecord
        {
            RoomId = request.RoomId ?? "",
            MapId = request.MapId ?? "",
            HostUid = request.HostUid ?? "",
            HostActorId = request.HostActorId,
            IsClear = request.IsClear,
            ReportedPlayTimeMs = request.ReportedPlayTimeMs,
            VerifiedPlayTimeMs = request.VerifiedPlayTimeMs > 0 ? request.VerifiedPlayTimeMs : request.ReportedPlayTimeMs,
            TotalDamage = request.TotalDamage,
            PlayerUids = (request.PlayerUids ?? new List<string>())
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SubmittedAtMs = request.SubmittedAtMs > 0 ? request.SubmittedAtMs : nowMs,
            StoredAtMs = nowMs
        };
    }

    private static GameResultRecord? DeserializeRecord(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GameResultRecord>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class GameResultStoreResult
{
    public GameResultStoreResult(GameResultRecord record, bool isDuplicate, string status)
    {
        Record = record;
        IsDuplicate = isDuplicate;
        Status = status;
    }

    public GameResultRecord Record { get; }
    public bool IsDuplicate { get; }
    public string Status { get; }
}

public sealed class GameResultReportRequest
{
    public string RoomId { get; set; } = "";
    public string MapId { get; set; } = "";
    public string HostUid { get; set; } = "";
    public int HostActorId { get; set; }
    public bool IsClear { get; set; }
    public long ReportedPlayTimeMs { get; set; }
    public long VerifiedPlayTimeMs { get; set; }
    public int TotalDamage { get; set; }
    public List<string> PlayerUids { get; set; } = new();
    public long SubmittedAtMs { get; set; }
}

public sealed class GameResultRecord
{
    public string RoomId { get; set; } = "";
    public string MapId { get; set; } = "";
    public string HostUid { get; set; } = "";
    public int HostActorId { get; set; }
    public bool IsClear { get; set; }
    public long ReportedPlayTimeMs { get; set; }
    public long VerifiedPlayTimeMs { get; set; }
    public int TotalDamage { get; set; }
    public string[] PlayerUids { get; set; } = Array.Empty<string>();
    public long SubmittedAtMs { get; set; }
    public long StoredAtMs { get; set; }
}

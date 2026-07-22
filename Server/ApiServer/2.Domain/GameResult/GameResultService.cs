using System.Text;
using System.Text.Json;
using ApiServer.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ApiServer.Domain.GameResult;

public sealed class GameResultService
{
    private const int MaxResultDocumentBytes = 1_000_000;
    private const long MaxSubmissionClockSkewMs = 5 * 60 * 1000;
    private const int MaxIdentifierLength = 256;
    private const int MaxPlayerCount = 64;

    private const string RewardProjectionScript = """
        local eventAt = tonumber(ARGV[11])
        local cutoffAt = tonumber(ARGV[12])
        local isNew = redis.call('ZADD', KEYS[2], 'NX', eventAt, ARGV[1])
        if isNew == 1 then
            redis.call('HINCRBY', KEYS[1], 'ReportCount', 1)
            if ARGV[2] == '1' then
                redis.call('ZADD', KEYS[3], 'NX', eventAt, ARGV[1])
                redis.call('HINCRBY', KEYS[1], 'ClearCount', 1)
            end
        end

        local expiredReports = redis.call('ZCOUNT', KEYS[2], '-inf', cutoffAt)
        if expiredReports > 0 then
            redis.call('ZREMRANGEBYSCORE', KEYS[2], '-inf', cutoffAt)
            redis.call('HINCRBY', KEYS[1], 'ReportCount', -expiredReports)
        end

        local expiredClears = redis.call('ZCOUNT', KEYS[3], '-inf', cutoffAt)
        if expiredClears > 0 then
            redis.call('ZREMRANGEBYSCORE', KEYS[3], '-inf', cutoffAt)
            redis.call('HINCRBY', KEYS[1], 'ClearCount', -expiredClears)
        end

        local previousAt = tonumber(redis.call('HGET', KEYS[1], 'LastResultAtMs') or '0')
        if eventAt > cutoffAt and eventAt >= previousAt then
            redis.call('HSET', KEYS[1],
                'LastMatchId', ARGV[1],
                'LastRoomId', ARGV[3],
                'LastMapId', ARGV[4],
                'LastHostUid', ARGV[5],
                'LastHostActorId', ARGV[6],
                'LastIsClear', ARGV[2],
                'LastReportedPlayTimeMs', ARGV[7],
                'LastVerifiedPlayTimeMs', ARGV[8],
                'LastTotalDamage', ARGV[9],
                'LastResultAtMs', ARGV[11])
        end

        redis.call('EXPIRE', KEYS[1], ARGV[10])
        redis.call('EXPIRE', KEYS[2], ARGV[10])
        redis.call('EXPIRE', KEYS[3], ARGV[10])
        return isNew
        """;

    private const string StoreAndQueueArchiveScript = """
        local stored = redis.call('SET', KEYS[1], ARGV[1], 'NX', 'EX', ARGV[2])
        if stored and ARGV[3] == '1' then
            redis.call('SADD', KEYS[2], ARGV[4])
        end
        return stored and 1 or 0
        """;

    private const string RollbackReservedResultScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            redis.call('DEL', KEYS[1])
            redis.call('SREM', KEYS[2], ARGV[2])
            return 1
        end
        return 0
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    private readonly RedisStore _redis;
    private readonly IGameResultArchive _archive;
    private readonly ILogger<GameResultService> _logger;

    public GameResultService(
        RedisStore redis,
        IGameResultArchive archive,
        ILogger<GameResultService> logger)
    {
        _redis = redis;
        _archive = archive;
        _logger = logger;
    }

    public bool ArchiveEnabled => _archive.IsEnabled;

    public async Task<GameResultStoreResult> SaveAsync(
        GameResultReportRequest request,
        CancellationToken ct = default)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Validate(request, nowMs);

        var incoming = CreateRecord(request, nowMs);
        var json = JsonSerializer.Serialize(incoming, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaxResultDocumentBytes)
            throw new ArgumentException("Game-result payload exceeds the 1 MB limit.", nameof(request));

        var resultKey = _redis.KeyGameResult(incoming.MatchId);

        var existingJson = await _redis.Db.StringGetAsync(resultKey);
        if (existingJson.HasValue)
        {
            var existing = DeserializeRequired(existingJson!, incoming.MatchId);
            EnsureSamePayload(existing, incoming);

            var archiveStatus = await TryArchiveAsync(existing, ct);
            await EnsureRedisProjectionAsync(existing);
            await UpdateArchivePendingAsync(existing.MatchId, archiveStatus);

            _logger.LogInformation(
                "[GameResult] Duplicate accepted match={MatchId} room={RoomId} archive={ArchiveStatus}",
                existing.MatchId,
                existing.RoomId,
                archiveStatus);

            return new GameResultStoreResult(existing, true, "duplicate", archiveStatus);
        }

        // Redis atomically reserves MatchId and registers the durable handoff before Mongo I/O.
        // If the process stops after this point, the reconciler can still archive the result.
        var storeResult = await _redis.Db.ScriptEvaluateAsync(
            StoreAndQueueArchiveScript,
            new RedisKey[]
            {
                resultKey,
                _redis.KeyGameResultArchivePending()
            },
            new RedisValue[]
            {
                json,
                7 * 24 * 60 * 60,
                _archive.IsEnabled ? 1 : 0,
                incoming.MatchId
            });
        var stored = string.Equals(storeResult.ToString(), "1", StringComparison.Ordinal);

        if (!stored)
        {
            existingJson = await _redis.Db.StringGetAsync(resultKey);
            var existing = DeserializeRequired(existingJson, incoming.MatchId);
            EnsureSamePayload(existing, incoming);

            var archiveStatus = await TryArchiveAsync(existing, ct);
            await EnsureRedisProjectionAsync(existing);
            await UpdateArchivePendingAsync(existing.MatchId, archiveStatus);

            _logger.LogInformation(
                "[GameResult] Duplicate race accepted match={MatchId} room={RoomId} archive={ArchiveStatus}",
                existing.MatchId,
                existing.RoomId,
                archiveStatus);

            return new GameResultStoreResult(existing, true, "duplicate", archiveStatus);
        }

        GameResultArchiveWriteStatus firstArchiveStatus;
        try
        {
            firstArchiveStatus = await TryArchiveAsync(incoming, ct);
        }
        catch (GameResultArchiveConflictException)
        {
            await RollbackReservedResultAsync(resultKey, json, incoming.MatchId);
            throw;
        }
        await EnsureRedisProjectionAsync(incoming);
        await UpdateArchivePendingAsync(incoming.MatchId, firstArchiveStatus);

        _logger.LogInformation(
            "[GameResult] Stored match={MatchId} room={RoomId} clear={Clear} playTime={PlayTimeMs} damage={Damage} players={PlayerCount} archive={ArchiveStatus}",
            incoming.MatchId,
            incoming.RoomId,
            incoming.IsClear,
            incoming.VerifiedPlayTimeMs,
            incoming.TotalDamage,
            incoming.PlayerUids.Length,
            firstArchiveStatus);

        return new GameResultStoreResult(incoming, false, "stored", firstArchiveStatus);
    }

    public Task<ArchivedGameResult?> FindByMatchIdAsync(
        string matchId,
        CancellationToken ct = default)
        => _archive.FindByMatchIdAsync(matchId, ct);

    public Task<IReadOnlyList<ArchivedGameResult>> FindByPlayerAsync(
        string uid,
        string? mapId,
        int limit,
        CancellationToken ct = default)
        => _archive.FindByPlayerAsync(uid, mapId, limit, ct);

    private async Task RollbackReservedResultAsync(
        string resultKey,
        string expectedJson,
        string matchId)
    {
        await _redis.Db.ScriptEvaluateAsync(
            RollbackReservedResultScript,
            new RedisKey[]
            {
                resultKey,
                _redis.KeyGameResultArchivePending()
            },
            new RedisValue[] { expectedJson, matchId });
    }
    private async Task<GameResultArchiveWriteStatus> TryArchiveAsync(
        GameResultRecord record,
        CancellationToken ct)
    {
        if (!_archive.IsEnabled)
            return GameResultArchiveWriteStatus.Disabled;

        try
        {
            return await _archive.StoreAsync(record, ct);
        }
        catch (GameResultArchiveConflictException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[GameResult] Mongo archive unavailable; queued for reconciliation match={MatchId}",
                record.MatchId);
            return GameResultArchiveWriteStatus.PendingRetry;
        }
    }

    private async Task UpdateArchivePendingAsync(
        string matchId,
        GameResultArchiveWriteStatus status)
    {
        if (!_archive.IsEnabled)
            return;

        var pendingKey = _redis.KeyGameResultArchivePending();
        if (status == GameResultArchiveWriteStatus.PendingRetry)
            await _redis.Db.SetAddAsync(pendingKey, matchId);
        else
            await _redis.Db.SetRemoveAsync(pendingKey, matchId);
    }

    internal async Task EnsureRedisProjectionAsync(GameResultRecord record)
    {
        const int ledgerTtlSeconds = 31 * 24 * 60 * 60;
        var cutoffAtMs = DateTimeOffset.UtcNow
            .Subtract(TimeSpan.FromDays(30))
            .ToUnixTimeMilliseconds();

        foreach (var uid in record.PlayerUids)
        {
            if (string.IsNullOrWhiteSpace(uid))
                continue;

            var keys = new RedisKey[]
            {
                _redis.KeyGameRewardLedger(uid),
                _redis.KeyGameRewardLedgerProcessed(uid),
                _redis.KeyGameRewardLedgerCleared(uid)
            };
            var values = new RedisValue[]
            {
                record.MatchId,
                record.IsClear ? 1 : 0,
                record.RoomId,
                record.MapId,
                record.HostUid,
                record.HostActorId,
                record.ReportedPlayTimeMs,
                record.VerifiedPlayTimeMs,
                record.TotalDamage,
                ledgerTtlSeconds,
                record.SubmittedAtMs,
                cutoffAtMs
            };

            await _redis.Db.ScriptEvaluateAsync(RewardProjectionScript, keys, values);
        }
    }
    private void EnsureSamePayload(GameResultRecord existing, GameResultRecord incoming)
    {
        var existingHash = GameResultFingerprint.Compute(existing);
        var incomingHash = GameResultFingerprint.Compute(incoming);
        if (string.Equals(existingHash, incomingHash, StringComparison.Ordinal))
            return;

        _logger.LogWarning(
            "[GameResult] Idempotency conflict match={MatchId} existingHash={ExistingHash} incomingHash={IncomingHash}",
            incoming.MatchId,
            existingHash,
            incomingHash);
        throw new GameResultArchiveConflictException(incoming.MatchId);
    }

    private static void Validate(GameResultReportRequest request, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.MatchId))
            throw new ArgumentException("MatchId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RoomId))
            throw new ArgumentException("RoomId is required.", nameof(request));
        if (request.MatchId.Length > MaxIdentifierLength ||
            request.RoomId.Length > MaxIdentifierLength ||
            (request.MapId?.Length ?? 0) > MaxIdentifierLength ||
            (request.HostUid?.Length ?? 0) > MaxIdentifierLength)
        {
            throw new ArgumentException("Game-result identifiers must be 256 characters or fewer.", nameof(request));
        }
        if ((request.PlayerUids?.Count ?? 0) > MaxPlayerCount ||
            (request.PlayerUids?.Any(uid => (uid?.Length ?? 0) > MaxIdentifierLength) ?? false))
        {
            throw new ArgumentException("PlayerUids exceeds the allowed count or identifier length.", nameof(request));
        }
        if (request.ReportedPlayTimeMs < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "ReportedPlayTimeMs cannot be negative.");
        if (request.VerifiedPlayTimeMs < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "VerifiedPlayTimeMs cannot be negative.");
        if (request.TotalDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "TotalDamage cannot be negative.");
        if (request.SubmittedAtMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "SubmittedAtMs must be a positive Unix timestamp.");
        if (request.SubmittedAtMs < nowMs - MaxSubmissionClockSkewMs || request.SubmittedAtMs > nowMs + MaxSubmissionClockSkewMs)
            throw new ArgumentOutOfRangeException(nameof(request), "SubmittedAtMs must be within 5 minutes of the API server clock.");
    }

    private static GameResultRecord CreateRecord(GameResultReportRequest request, long nowMs)
    {
        return new GameResultRecord
        {
            MatchId = request.MatchId.Trim(),
            RoomId = request.RoomId.Trim(),
            MapId = request.MapId?.Trim() ?? "",
            HostUid = request.HostUid?.Trim() ?? "",
            HostActorId = request.HostActorId,
            IsClear = request.IsClear,
            ReportedPlayTimeMs = request.ReportedPlayTimeMs,
            VerifiedPlayTimeMs = request.VerifiedPlayTimeMs > 0
                ? request.VerifiedPlayTimeMs
                : request.ReportedPlayTimeMs,
            TotalDamage = request.TotalDamage,
            PlayerUids = (request.PlayerUids ?? new List<string>())
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .Select(uid => uid.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SubmittedAtMs = request.SubmittedAtMs,
            StoredAtMs = nowMs,
            Telemetry = CloneTelemetry(request.Telemetry)
        };
    }

    private static Dictionary<string, JsonElement>? CloneTelemetry(
        IReadOnlyDictionary<string, JsonElement>? telemetry)
    {
        if (telemetry == null || telemetry.Count == 0)
            return null;

        return telemetry.ToDictionary(
            x => x.Key,
            x => x.Value.Clone(),
            StringComparer.Ordinal);
    }

    internal static GameResultRecord? DeserializeRecord(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GameResultRecord>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static GameResultRecord DeserializeRequired(RedisValue json, string matchId)
        => DeserializeRequired(json.ToString(), matchId);

    private static GameResultRecord DeserializeRequired(string json, string matchId)
    {
        return DeserializeRecord(json)
            ?? throw new InvalidOperationException(
                $"Cached game result for match ''{matchId}'' is invalid.");
    }
}

public sealed class GameResultStoreResult
{
    public GameResultStoreResult(
        GameResultRecord record,
        bool isDuplicate,
        string status,
        GameResultArchiveWriteStatus archiveStatus)
    {
        Record = record;
        IsDuplicate = isDuplicate;
        Status = status;
        ArchiveStatus = archiveStatus;
    }

    public GameResultRecord Record { get; }
    public bool IsDuplicate { get; }
    public string Status { get; }
    public GameResultArchiveWriteStatus ArchiveStatus { get; }
}

public sealed class GameResultReportRequest
{
    public string MatchId { get; set; } = "";
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
    public Dictionary<string, JsonElement>? Telemetry { get; set; }
}

public sealed class GameResultRecord
{
    public string MatchId { get; set; } = "";
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
    public Dictionary<string, JsonElement>? Telemetry { get; set; }
}
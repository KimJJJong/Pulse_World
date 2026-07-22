using ApiServer.Domain.GameResult;
using ApiServer.Infrastructure.Options;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace ApiServer.Infrastructure.Persistence.Mongo;

public sealed class GameResultArchiveReconciler : BackgroundService
{
    private const string MoveToDeadLetterScript = """
        if ARGV[2] ~= '' then
            redis.call('SET', KEYS[4], ARGV[2], 'EX', ARGV[3])
            if redis.call('GET', KEYS[1]) == ARGV[2] then
                redis.call('DEL', KEYS[1])
            end
        end
        redis.call('SADD', KEYS[3], ARGV[1])
        redis.call('SREM', KEYS[2], ARGV[1])
        return 1
        """;
    private readonly RedisStore _redis;
    private readonly IGameResultArchive _archive;
    private readonly GameResultService _gameResults;
    private readonly MongoOptions _options;
    private readonly ILogger<GameResultArchiveReconciler> _logger;
    private bool _indexesReady;

    public GameResultArchiveReconciler(
        RedisStore redis,
        IGameResultArchive archive,
        GameResultService gameResults,
        IOptions<MongoOptions> options,
        ILogger<GameResultArchiveReconciler> logger)
    {
        _redis = redis;
        _archive = archive;
        _gameResults = gameResults;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_archive.IsEnabled)
        {
            _logger.LogInformation("[Mongo] Game-result archive is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(
            Math.Clamp(_options.ReconcileIntervalSeconds, 5, 3600));

        await ReconcileSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ReconcileSafelyAsync(stoppingToken);
    }

    private async Task ReconcileSafelyAsync(CancellationToken ct)
    {
        try
        {
            if (!_indexesReady)
            {
                await _archive.EnsureIndexesAsync(ct);
                _indexesReady = true;
                _logger.LogInformation("[Mongo] Game-result indexes are ready.");
            }

            await ReconcilePendingAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Mongo] Game-result reconciliation failed; retrying later.");
        }
    }

    private async Task ReconcilePendingAsync(CancellationToken ct)
    {
        var pendingKey = _redis.KeyGameResultArchivePending();
        var deadLetterKey = _redis.KeyGameResultArchiveDeadLetter();
        var batchSize = Math.Clamp(_options.ReconcileBatchSize, 1, 1000);
        var processedCount = 0;
        var archivedCount = 0;

        await foreach (var value in _redis.Db.SetScanAsync(pendingKey, pageSize: batchSize))
        {
            if (processedCount++ >= batchSize)
                break;

            ct.ThrowIfCancellationRequested();

            var matchId = value.ToString();
            if (string.IsNullOrWhiteSpace(matchId))
            {
                await _redis.Db.SetRemoveAsync(pendingKey, value);
                continue;
            }

            var json = await _redis.Db.StringGetAsync(_redis.KeyGameResult(matchId));
            if (json.IsNullOrEmpty)
            {
                _logger.LogError(
                    "[Mongo] Archive task expired before reconciliation match={MatchId}",
                    matchId);
                await MoveToDeadLetterAsync(matchId, json.ToString());
                continue;
            }

            var record = GameResultService.DeserializeRecord(json!);
            if (record == null)
            {
                _logger.LogError(
                    "[Mongo] Invalid archive task moved to dead letter match={MatchId}",
                    matchId);
                await MoveToDeadLetterAsync(matchId, json.ToString());
                continue;
            }

            try
            {
                var status = await _archive.StoreAsync(record, ct);
                if (status is GameResultArchiveWriteStatus.Stored
                    or GameResultArchiveWriteStatus.AlreadyExists)
                {
                    await _gameResults.EnsureRedisProjectionAsync(record);
                    await _redis.Db.SetRemoveAsync(pendingKey, value);
                    await _redis.Db.SetRemoveAsync(deadLetterKey, value);
                    await _redis.Db.KeyDeleteAsync(
                        _redis.KeyGameResultArchiveDeadLetterPayload(matchId));
                    archivedCount++;
                }
            }
            catch (GameResultArchiveConflictException ex)
            {
                _logger.LogError(
                    ex,
                    "[Mongo] Archive conflict moved to dead letter match={MatchId}",
                    matchId);
                await MoveToDeadLetterAsync(matchId, json.ToString());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (FormatException ex)
            {
                _logger.LogError(
                    ex,
                    "[Mongo] Invalid BSON moved to dead letter match={MatchId}",
                    matchId);
                await MoveToDeadLetterAsync(matchId, json.ToString());
            }
            catch (BsonSerializationException ex)
            {
                _logger.LogError(
                    ex,
                    "[Mongo] BSON serialization failure moved to dead letter match={MatchId}",
                    matchId);
                await MoveToDeadLetterAsync(matchId, json.ToString());
            }
            catch (MongoWriteException ex) when (
                ex.WriteError?.Code is 2 or 9 or 14 or 121 or 10334)
            {
                _logger.LogError(
                    ex,
                    "[Mongo] Permanent write rejection moved to dead letter match={MatchId} code={Code}",
                    matchId,
                    ex.WriteError?.Code);
                await MoveToDeadLetterAsync(matchId, json.ToString());
            }
            catch (MongoCommandException ex) when (ex.Code is 2 or 9 or 14 or 121 or 10334)
            {
                _logger.LogError(
                    ex,
                    "[Mongo] Permanent command rejection moved to dead letter match={MatchId} code={Code}",
                    matchId,
                    ex.Code);
                await MoveToDeadLetterAsync(matchId, json.ToString());
            }
            catch (Exception ex)
            {
                // Leave transient failures pending, but continue so one item cannot starve the batch.
                _logger.LogWarning(
                    ex,
                    "[Mongo] Archive retry failed; keeping pending match={MatchId}",
                    matchId);
            }
        }

        if (archivedCount > 0)
        {
            _logger.LogInformation(
                "[Mongo] Reconciled game-result archive count={Count}",
                archivedCount);
        }
    }

    private async Task MoveToDeadLetterAsync(string matchId, string? payload)
    {
        await _redis.Db.ScriptEvaluateAsync(
            MoveToDeadLetterScript,
            new RedisKey[]
            {
                _redis.KeyGameResult(matchId),
                _redis.KeyGameResultArchivePending(),
                _redis.KeyGameResultArchiveDeadLetter(),
                _redis.KeyGameResultArchiveDeadLetterPayload(matchId)
            },
            new RedisValue[]
            {
                matchId,
                payload ?? "",
                30 * 24 * 60 * 60
            });
    }
}


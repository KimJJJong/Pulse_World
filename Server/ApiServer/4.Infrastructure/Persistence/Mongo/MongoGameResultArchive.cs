using System.Text.Json;
using ApiServer.Domain.GameResult;
using ApiServer.Infrastructure.Options;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace ApiServer.Infrastructure.Persistence.Mongo;

public sealed class MongoGameResultArchive : IGameResultArchive
{
    private const int CurrentSchemaVersion = 1;

    private readonly MongoOptions _options;
    private readonly TimeSpan _operationTimeout;
    private readonly IMongoDatabase? _database;
    private readonly IMongoCollection<GameResultDocument>? _collection;
    private readonly ILogger<MongoGameResultArchive> _logger;

    public MongoGameResultArchive(
        IOptions<MongoOptions> options,
        ILogger<MongoGameResultArchive> logger)
    {
        _options = options.Value;
        _logger = logger;
        _operationTimeout = TimeSpan.FromSeconds(
            Math.Clamp(_options.OperationTimeoutSeconds, 1, 60));

        if (!_options.Enabled)
            return;

        var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
        settings.ApplicationName = "PulseWorld.ApiServer";
        settings.ConnectTimeout = TimeSpan.FromSeconds(
            Math.Clamp(_options.ConnectTimeoutSeconds, 1, 60));
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(
            Math.Clamp(_options.ServerSelectionTimeoutSeconds, 1, 60));
        settings.SocketTimeout = _operationTimeout;

        var client = new MongoClient(settings);
        _database = client.GetDatabase(_options.DatabaseName);
        var writeConcern = WriteConcern.WMajority.With(
            wTimeout: TimeSpan.FromSeconds(
                Math.Clamp(_options.WriteConcernTimeoutSeconds, 1, 60)));
        _collection = _database
            .GetCollection<GameResultDocument>(_options.GameResultsCollection)
            .WithWriteConcern(writeConcern);

        _logger.LogInformation(
            "[Mongo] Game-result archive configured database={Database} collection={Collection}",
            _options.DatabaseName,
            _options.GameResultsCollection);
    }

    public bool IsEnabled => _options.Enabled;

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        if (!IsEnabled)
            return;

        var database = RequiredDatabase();
        var collection = RequiredCollection();

        await database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: ct);

        var indexes = new[]
        {
            new CreateIndexModel<GameResultDocument>(
                Builders<GameResultDocument>.IndexKeys
                    .Ascending(x => x.PlayerUids)
                    .Descending(x => x.StoredAtMs),
                new CreateIndexOptions { Name = "ix_player_stored_at" }),
            new CreateIndexModel<GameResultDocument>(
                Builders<GameResultDocument>.IndexKeys
                    .Ascending(x => x.MapId)
                    .Ascending(x => x.IsClear)
                    .Descending(x => x.StoredAtMs),
                new CreateIndexOptions { Name = "ix_map_clear_stored_at" }),
            new CreateIndexModel<GameResultDocument>(
                Builders<GameResultDocument>.IndexKeys
                    .Ascending(x => x.RoomId)
                    .Descending(x => x.StoredAtMs),
                new CreateIndexOptions { Name = "ix_room_stored_at" })
        };

        await collection.Indexes.CreateManyAsync(indexes, cancellationToken: ct);
    }

    public async Task<GameResultArchiveWriteStatus> StoreAsync(
        GameResultRecord record,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
            return GameResultArchiveWriteStatus.Disabled;

        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.MatchId))
            throw new ArgumentException("MatchId is required.", nameof(record));

        using var operationCts = CreateOperationTimeout(ct);
        var operationToken = operationCts.Token;
        var collection = RequiredCollection();
        var document = GameResultDocument.From(record);

        try
        {
            await collection.InsertOneAsync(document, cancellationToken: operationToken);
            return GameResultArchiveWriteStatus.Stored;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await collection
                .Find(x => x.MatchId == record.MatchId)
                .FirstOrDefaultAsync(operationToken);

            if (existing != null &&
                string.Equals(existing.PayloadHash, document.PayloadHash, StringComparison.Ordinal))
            {
                return GameResultArchiveWriteStatus.AlreadyExists;
            }

            throw new GameResultArchiveConflictException(record.MatchId);
        }
    }

    public async Task<ArchivedGameResult?> FindByMatchIdAsync(
        string matchId,
        CancellationToken ct = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(matchId))
            return null;

        using var operationCts = CreateOperationTimeout(ct);
        var operationToken = operationCts.Token;
        var document = await RequiredCollection()
            .Find(x => x.MatchId == matchId)
            .FirstOrDefaultAsync(operationToken);

        return document?.ToDomain();
    }

    public async Task<IReadOnlyList<ArchivedGameResult>> FindByPlayerAsync(
        string uid,
        string? mapId,
        int limit,
        CancellationToken ct = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(uid))
            return Array.Empty<ArchivedGameResult>();

        using var operationCts = CreateOperationTimeout(ct);
        var operationToken = operationCts.Token;
        var maxLimit = Math.Max(1, _options.MaxHistoryLimit);
        var effectiveLimit = Math.Clamp(limit, 1, maxLimit);
        var filters = new List<FilterDefinition<GameResultDocument>>
        {
            Builders<GameResultDocument>.Filter.AnyEq(x => x.PlayerUids, uid)
        };

        if (!string.IsNullOrWhiteSpace(mapId))
            filters.Add(Builders<GameResultDocument>.Filter.Eq(x => x.MapId, mapId));

        var documents = await RequiredCollection()
            .Find(Builders<GameResultDocument>.Filter.And(filters))
            .SortByDescending(x => x.StoredAtMs)
            .Limit(effectiveLimit)
            .ToListAsync(operationToken);

        return documents.Select(x => x.ToDomain()).ToArray();
    }

    private CancellationTokenSource CreateOperationTimeout(CancellationToken ct)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_operationTimeout);
        return timeoutCts;
    }
    private IMongoDatabase RequiredDatabase()
        => _database ?? throw new InvalidOperationException("MongoDB archive is not configured.");

    private IMongoCollection<GameResultDocument> RequiredCollection()
        => _collection ?? throw new InvalidOperationException("MongoDB archive is not configured.");

    [BsonIgnoreExtraElements]
    private sealed class GameResultDocument
    {
        [BsonId]
        public string MatchId { get; init; } = "";

        [BsonElement("schemaVersion")]
        public int SchemaVersion { get; init; }

        [BsonElement("payloadHash")]
        public string PayloadHash { get; init; } = "";

        [BsonElement("roomId")]
        public string RoomId { get; init; } = "";

        [BsonElement("mapId")]
        public string MapId { get; init; } = "";

        [BsonElement("hostUid")]
        public string HostUid { get; init; } = "";

        [BsonElement("hostActorId")]
        public int HostActorId { get; init; }

        [BsonElement("isClear")]
        public bool IsClear { get; init; }

        [BsonElement("reportedPlayTimeMs")]
        public long ReportedPlayTimeMs { get; init; }

        [BsonElement("verifiedPlayTimeMs")]
        public long VerifiedPlayTimeMs { get; init; }

        [BsonElement("totalDamage")]
        public int TotalDamage { get; init; }

        [BsonElement("playerUids")]
        public string[] PlayerUids { get; init; } = Array.Empty<string>();

        [BsonElement("submittedAtMs")]
        public long SubmittedAtMs { get; init; }

        [BsonElement("storedAtMs")]
        public long StoredAtMs { get; init; }

        [BsonElement("archivedAtUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime ArchivedAtUtc { get; init; }

        [BsonElement("telemetry")]
        [BsonIgnoreIfNull]
        public BsonDocument? Telemetry { get; init; }

        public static GameResultDocument From(GameResultRecord record)
        {
            return new GameResultDocument
            {
                MatchId = record.MatchId,
                SchemaVersion = CurrentSchemaVersion,
                PayloadHash = GameResultFingerprint.Compute(record),
                RoomId = record.RoomId,
                MapId = record.MapId,
                HostUid = record.HostUid,
                HostActorId = record.HostActorId,
                IsClear = record.IsClear,
                ReportedPlayTimeMs = record.ReportedPlayTimeMs,
                VerifiedPlayTimeMs = record.VerifiedPlayTimeMs,
                TotalDamage = record.TotalDamage,
                PlayerUids = record.PlayerUids,
                SubmittedAtMs = record.SubmittedAtMs,
                StoredAtMs = record.StoredAtMs,
                ArchivedAtUtc = DateTime.UtcNow,
                Telemetry = SerializeTelemetry(record.Telemetry)
            };
        }

        public ArchivedGameResult ToDomain()
        {
            return new ArchivedGameResult
            {
                SchemaVersion = SchemaVersion,
                MatchId = MatchId,
                PayloadHash = PayloadHash,
                RoomId = RoomId,
                MapId = MapId,
                HostUid = HostUid,
                HostActorId = HostActorId,
                IsClear = IsClear,
                ReportedPlayTimeMs = ReportedPlayTimeMs,
                VerifiedPlayTimeMs = VerifiedPlayTimeMs,
                TotalDamage = TotalDamage,
                PlayerUids = PlayerUids,
                SubmittedAtMs = SubmittedAtMs,
                StoredAtMs = StoredAtMs,
                ArchivedAtUtc = ArchivedAtUtc,
                Telemetry = DeserializeTelemetry(Telemetry)
            };
        }

        private static BsonDocument? SerializeTelemetry(
            IReadOnlyDictionary<string, JsonElement>? telemetry)
        {
            if (telemetry == null || telemetry.Count == 0)
                return null;

            return BsonDocument.Parse(JsonSerializer.Serialize(telemetry));
        }

        private static Dictionary<string, JsonElement>? DeserializeTelemetry(BsonDocument? telemetry)
        {
            if (telemetry == null)
                return null;

            var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson };
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(telemetry.ToJson(settings));
        }
    }
}


namespace ApiServer.Infrastructure.Options;

public sealed class MongoOptions
{
    public bool Enabled { get; init; }
    public string ConnectionString { get; init; } = "mongodb://localhost:27017";
    public string DatabaseName { get; init; } = "rhythm_analytics";
    public string GameResultsCollection { get; init; } = "game_results";
    public int ConnectTimeoutSeconds { get; init; } = 5;
    public int ServerSelectionTimeoutSeconds { get; init; } = 5;
    public int OperationTimeoutSeconds { get; init; } = 5;
    public int WriteConcernTimeoutSeconds { get; init; } = 5;
    public int ReconcileIntervalSeconds { get; init; } = 15;
    public int ReconcileBatchSize { get; init; } = 100;
    public int DefaultHistoryLimit { get; init; } = 20;
    public int MaxHistoryLimit { get; init; } = 100;
}

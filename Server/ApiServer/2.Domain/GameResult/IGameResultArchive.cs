using System.Text.Json;

namespace ApiServer.Domain.GameResult;

public interface IGameResultArchive
{
    bool IsEnabled { get; }

    Task EnsureIndexesAsync(CancellationToken ct = default);
    Task<GameResultArchiveWriteStatus> StoreAsync(GameResultRecord record, CancellationToken ct = default);
    Task<ArchivedGameResult?> FindByMatchIdAsync(string matchId, CancellationToken ct = default);
    Task<IReadOnlyList<ArchivedGameResult>> FindByPlayerAsync(
        string uid,
        string? mapId,
        int limit,
        CancellationToken ct = default);
}

public enum GameResultArchiveWriteStatus
{
    Disabled,
    Stored,
    AlreadyExists,
    PendingRetry
}

public sealed class ArchivedGameResult
{
    public int SchemaVersion { get; init; }
    public string MatchId { get; init; } = "";
    public string PayloadHash { get; init; } = "";
    public string RoomId { get; init; } = "";
    public string MapId { get; init; } = "";
    public string HostUid { get; init; } = "";
    public int HostActorId { get; init; }
    public bool IsClear { get; init; }
    public long ReportedPlayTimeMs { get; init; }
    public long VerifiedPlayTimeMs { get; init; }
    public int TotalDamage { get; init; }
    public string[] PlayerUids { get; init; } = Array.Empty<string>();
    public long SubmittedAtMs { get; init; }
    public long StoredAtMs { get; init; }
    public DateTime ArchivedAtUtc { get; init; }
    public Dictionary<string, JsonElement>? Telemetry { get; init; }
}

public sealed class GameResultArchiveConflictException : Exception
{
    public GameResultArchiveConflictException(string matchId)
        : base($"A different game result is already stored for match ''{matchId}''.")
    {
        MatchId = matchId;
    }

    public string MatchId { get; }
}

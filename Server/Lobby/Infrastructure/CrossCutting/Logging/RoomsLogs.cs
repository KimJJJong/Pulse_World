namespace Lobby.Logging;

/// <summary>
/// Rooms 컨트롤러용 고성능 로그 래퍼 (Source Generator 기반)
/// </summary>
public static partial class RoomsLogs
{
    // GET /rooms
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "rooms.get 304 NotModified etag={Etag} tookMs={ElapsedMs}")]
    public static partial void RoomsGet304(ILogger logger, string etag, double elapsedMs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "rooms.get 200 OK count={Count} etag={Etag} tookMs={ElapsedMs}")]
    public static partial void RoomsGetOk(ILogger logger, int count, string etag, double elapsedMs);

    // POST /rooms
    [LoggerMessage(EventId = 1101, Level = LogLevel.Information,
        Message = "rooms.create 200 OK roomId={RoomId} userId={UserId} map={Map} max={Max} visibility={Visibility} wsUrl={WsUrl} tookMs={ElapsedMs}")]
    public static partial void RoomsCreateOk(ILogger logger, string roomId, string userId, string map, int max, string visibility, string wsUrl, double elapsedMs);

    // POST /rooms/{id}/join
    [LoggerMessage(EventId = 1201, Level = LogLevel.Warning,
        Message = "rooms.join failed roomId={RoomId} reason={Reason} tookMs={ElapsedMs}")]
    public static partial void RoomsJoinFail(ILogger logger, string roomId, string reason, double elapsedMs);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information,
        Message = "rooms.join 200 OK roomId={RoomId} userId={UserId} slot={Slot} cur={Cur} status={Status} tookMs={ElapsedMs}")]
    public static partial void RoomsJoinOk(ILogger logger, string roomId, string userId, int slot, int cur, string status, double elapsedMs);
}

using Microsoft.Extensions.Logging;

namespace Lobby.Logging;

/// <summary>
/// Room WS Hub 로그 (Source Generator)
/// </summary>
public static partial class RoomHubLogs
{
    // 연결/인증
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "ws.accept roomId={RoomId}")]
    public static partial void WsAccept(ILogger logger, string roomId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "auth.fail reason={Reason}")]
    public static partial void AuthFail(ILogger logger, string reason);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "client.version.unsupported client={Client} min={Min}")]
    public static partial void VersionUnsupported(ILogger logger, string client, string min);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "room.not_found id={RoomId}")]
    public static partial void RoomNotFound(ILogger logger, string roomId);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "ws.join userId={UserId}")]
    public static partial void WsJoin(ILogger logger, string userId);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "welcome.sent members={Members} hasCountdown={HasCountdown}")]
    public static partial void WelcomeSent(ILogger logger, int members, bool hasCountdown);

    // 상태 브로드캐스트
    [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "member.join.broadcast userId={UserId}")]
    public static partial void MemberJoinBroadcast(ILogger logger, string userId);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "member.update.broadcast userId={UserId} ready={Ready}")]
    public static partial void MemberUpdateBroadcast(ILogger logger, string userId, bool ready);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "member.leave userId={UserId}")]
    public static partial void MemberLeave(ILogger logger, string userId);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Information, Message = "room.update cur={Cur} status={Status}")]
    public static partial void RoomUpdate(ILogger logger, int cur, string status);

    // 카운트다운/게임 시작
    [LoggerMessage(EventId = 2200, Level = LogLevel.Information, Message = "countdown.start sec={Seconds} startAtMs={StartAtMs}")]
    public static partial void CountdownStart(ILogger logger, int seconds, long startAtMs);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Information, Message = "countdown.cancel")]
    public static partial void CountdownCancel(ILogger logger);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Information, Message = "game.begin host={Host} port={Port} ticket8={Ticket8}")]
    public static partial void GameBegin(ILogger logger, string host, int port, string ticket8);

    // 레이트 리밋/알 수 없는 op/종료
    [LoggerMessage(EventId = 2300, Level = LogLevel.Warning, Message = "ready.rate_limited userId={UserId} perSec={PerSec}")]
    public static partial void ReadyRateLimited(ILogger logger, string userId, int perSec);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Warning, Message = "unknown.op op={Op}")]
    public static partial void UnknownOp(ILogger logger, string op);

    [LoggerMessage(EventId = 2302, Level = LogLevel.Information, Message = "ws.remove")]
    public static partial void WsRemove(ILogger logger);

    [LoggerMessage(EventId = 2399, Level = LogLevel.Error, Message = "receive.loop.error {Message}")]
    public static partial void ReceiveLoopError(ILogger logger, string message, Exception? ex);
    [LoggerMessage(EventId = 2398, Level = LogLevel.Error, Message = "unexpectErr ")]
    public static partial void GameBeginError(ILogger logger, string message, Exception? ex);
}

using System.Collections.Concurrent;
namespace Lobby.Domain.Rooms;

public sealed class Room
{
    public string Id { get; init; } = default!;
    public string Title { get; set; } = "";
    public string Map { get; set; } = "";
    public int MaxPlayers { get; set; } = 2;
    public int CurPlayers => Members.Count;
    public RoomStatus Status { get; set; } = RoomStatus.Open;
    public RoomVisibility Visibility { get; set; } = RoomVisibility.Public;
    public long UpdatedAtMs { get; set; }
    public ConcurrentDictionary<string, Member> Members { get; } = new();
    // Countdown 관리
    public int? CountdownSeconds { get; set; }
    public long? CountdownStartAtMs { get; set; }
    public CancellationTokenSource? CountdownCts { get; set; }
    public CancellationTokenSource? DeleteCts { get; set; }        // 삭제 유예 토큰
    public long? DeleteDueAtMs { get; set; }                       // 언제 삭제 예정인지(디버그/로그용)
}
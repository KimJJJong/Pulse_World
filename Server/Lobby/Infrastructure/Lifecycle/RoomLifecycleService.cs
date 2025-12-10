using Lobby.Domain.Auth.Interface;
using Lobby.Domain.Rooms;

namespace Lobby.Infrastructure.Lifecycle;

public interface IRoomLifecycle
{
    void ScheduleDeletion(Room r, TimeSpan grace);
    void CancelDeletion(Room r);
}

public sealed class RoomLifecycleService : IRoomLifecycle
{
    private readonly IRoomRepository _repo;
    public RoomLifecycleService(IRoomRepository repo) => _repo = repo;

    public void ScheduleDeletion(Room r, TimeSpan grace)
    {
        // 이미 스케줄되어 있으면 무시
        if (r.DeleteCts != null) return;

        r.DeleteCts = new CancellationTokenSource();
        r.DeleteDueAtMs = DateTimeOffset.UtcNow.Add(grace).ToUnixTimeMilliseconds();
        var roomId = r.Id;
        var cts = r.DeleteCts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(grace, cts!.Token);
                await _repo.DeleteAsync(roomId);
            }
            catch (TaskCanceledException) { /* 취소됨 */ }
            finally
            {
                r.DeleteCts = null;
                r.DeleteDueAtMs = null;
            }
        });
    }

    public void CancelDeletion(Room r)
    {
        r.DeleteCts?.Cancel();
        r.DeleteCts = null;
        r.DeleteDueAtMs = null;
    }
}

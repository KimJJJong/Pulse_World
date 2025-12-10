using Lobby.Domain.Auth.Interface;
using Lobby.Domain.Rooms;
using System.Diagnostics;

namespace Lobby.Infrastructure.Persistence.InMemory;

public sealed class RoomRepository : IRoomRepository, IRoomReadModel
{
    private readonly InMemoryRoomStore _store;
    public RoomRepository(InMemoryRoomStore store) => _store = store;

    public ValueTask<Room?> GetAsync(string roomId)
        => ValueTask.FromResult(_store.Rooms.TryGetValue(roomId, out var r) ? r : null);

    // 하부의GetSnapshotWithEtagAsync 로 통일 이후 Interface 구조 변경 필요 ==>
    public ValueTask<IReadOnlyList<Room>> GetAllAsync(int pageSize, string? cursor)
        => ValueTask.FromResult((IReadOnlyList<Room>)_store.Rooms.Values
            .OrderByDescending(r => r.UpdatedAtMs).Take(pageSize).ToList());

    public async ValueTask<(string cursor, IReadOnlyList<Room> rooms)> GetPagedAsync(int pageSize, string? cursor)
        => ("", await GetAllAsync(pageSize, cursor));
    // <==
    public ValueTask<Room> CreateAsync(Room r)
    {
        _store.Rooms[r.Id] = r;
        Interlocked.Increment(ref _store.Version);
        return ValueTask.FromResult(r);
    }

    public ValueTask<bool> DeleteAsync(string roomId)
    {
        var ok = _store.Rooms.TryRemove(roomId, out _);
        if (ok) Interlocked.Increment(ref _store.Version);
        return ValueTask.FromResult(ok);
    }

    public ValueTask UpdateAsync(Room r)
    {
        r.UpdatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Interlocked.MemoryBarrier();
        Interlocked.Increment(ref _store.Version);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryJoinAsync(string roomId, Member m)
    {
        if (!_store.Rooms.TryGetValue(roomId, out var r)) return ValueTask.FromResult(false);

        lock (r) // 룸 단위 원자성 보장
        {

            if (r.Status != RoomStatus.Open) return ValueTask.FromResult(false);
            if (r.CurPlayers >= r.MaxPlayers) return ValueTask.FromResult(false);

            if (r.Members.ContainsKey(m.UserId)) return ValueTask.FromResult(true);

            //  빈 슬롯 찾기 (1..MaxPlayers 중 미사용)
            var used = r.Members.Values.Select(x => x.Slot).ToHashSet();
            var slot = Enumerable.Range(1, r.MaxPlayers).First(s => !used.Contains(s));
            m = new Member { UserId = m.UserId, Name = m.Name, Slot = slot, Ready = false };

            var added = r.Members.TryAdd(m.UserId, m);
            if (!added) return ValueTask.FromResult(false);

            r.Status = r.CurPlayers >= r.MaxPlayers ? RoomStatus.Full : RoomStatus.Open;
            r.UpdatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Interlocked.Increment(ref _store.Version);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<bool> LeaveAsync(string roomId, string userId)
    {
        if (!_store.Rooms.TryGetValue(roomId, out var r)) return ValueTask.FromResult(false);
        lock (r)
        {
            var removed = r.Members.TryRemove(userId, out _);
            if (removed)
            {
                r.Status = r.CurPlayers >= r.MaxPlayers ? RoomStatus.Full : RoomStatus.Open;
                r.UpdatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Interlocked.Increment(ref _store.Version);
            }
            return ValueTask.FromResult(removed);
        }
    }


    // ETag 스냅샷(간단히 Version 기반)
    public ValueTask<(string etag, IReadOnlyList<Room>)> GetSnapshotWithEtagAsync(int pageSize, string? cursor)
    {
        var etag = $"\"rlist-{_store.Version}\"";
        return ValueTask.FromResult((etag, _store.Rooms.Values
            .OrderByDescending(r => r.UpdatedAtMs).Take(pageSize).ToList() as IReadOnlyList<Room>));
    }
}
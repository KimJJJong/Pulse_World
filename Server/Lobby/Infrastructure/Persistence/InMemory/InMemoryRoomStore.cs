using System.Collections.Concurrent;
using Lobby.Domain.Rooms;

namespace Lobby.Infrastructure.Persistence.InMemory;

public sealed class InMemoryRoomStore
{
    public ConcurrentDictionary<string, Room> Rooms { get; } = new();
    // 전체 리스트 버전: 방 생성/삭제/상태변화 시 증가 → ETag 기반
    public int Version;
}
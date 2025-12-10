using Lobby.Domain.Auth.Interface;
using Lobby.Domain.Rooms;

namespace Lobby.Infrastructure.Realtime;

public sealed class RoomBroadcaster : IRoomBroadcaster
{
    public Task PublishRoomUpdateAsync(Room r) => Task.CompletedTask;
    public Task PublishMemberUpdateAsync(Room r, Member m) => Task.CompletedTask;
    public Task PublishCountdownAsync(Room r, bool start, int? seconds, long? startAtMs) => Task.CompletedTask;
    public Task PublishGameBeginAsync(Room r, string host, int port, string ticket) => Task.CompletedTask;
}
using Contracts.Packet;
using Lobby.Domain.Rooms;

namespace Lobby.Domain.Shared;

public static class ContractMap
{
    public static RoomDto ToContract(this Room r) => new RoomDto
    {
        Id = r.Id,
        Title = r.Title,
        Map = r.Map,
        Max = r.MaxPlayers,
        Cur = r.CurPlayers,
        Status = r.Status.ToString(),
        Visibility = r.Visibility.ToString(),
        UpdatedAtMs = r.UpdatedAtMs
    };

    public static MemberDto ToContract(this Member m) => new MemberDto
    {
        UserId = m.UserId,
        Name = m.Name,
        Slot = m.Slot,
        Ready = m.Ready
    };
}

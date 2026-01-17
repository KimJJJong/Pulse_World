using System.Collections.Generic;

namespace ApiServer.Application.Ports.Models;

public sealed class WaitingRoomDto
{
    public string RoomId { get; set; } = "";
    public string Title { get; set; } = "";
    public string MapId { get; set; } = "";
    public int MaxPlayers { get; set; }
    public string OwnerUid { get; set; } = "";
    public string Status { get; set; } = "";
    public List<string> MemberUids { get; set; } = new();
    public Dictionary<string, bool> MemberReady { get; set; } = new();
}

using System.Collections.Generic;

namespace ApiServer.Presentation.Http.Responses;

public sealed class RoomListResponse
{
    public List<RoomItemResponse> rooms { get; set; } = new();
    public string nextCursor { get; set; } = "";
}

public sealed class RoomItemResponse
{
    public string roomId { get; set; }
    public string title { get; set; }
    public string mapId { get; set; }
    public int maxPlayers { get; set; }
    public int memberCount { get; set; }
    public string status { get; set; }
    public string ownerUid { get; set; }
}

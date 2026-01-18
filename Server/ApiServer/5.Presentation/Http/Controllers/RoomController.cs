using ApiServer.Application.Ports;
using ApiServer.Presentation.Http.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers;

[ApiController]
[Route("rooms")]
public class RoomController : ControllerBase
{
    private readonly IControlPlanePort _cp;

    public RoomController(IControlPlanePort cp)
    {
        _cp = cp;
    }

    [HttpGet]
    public async Task<ActionResult<RoomListResponse>> GetList([FromQuery] int limit = 20, [FromQuery] string cursor = "")
    {
        if (limit > 50) limit = 50;
        
        var (rooms, nextCursor) = await _cp.GetWaitingRoomListAsync(limit, cursor, HttpContext.RequestAborted);
        
        var resp = new RoomListResponse
        {
            nextCursor = nextCursor,
            rooms = rooms.Select(r => new RoomItemResponse
            {
                roomId = r.RoomId,
                title = r.Title,
                mapId = r.MapId,
                maxPlayers = r.MaxPlayers,
                memberCount = r.MemberUids.Count,
                status = r.Status,
                ownerUid = r.OwnerUid
            }).ToList()
        };

        return Ok(resp);
    }
}

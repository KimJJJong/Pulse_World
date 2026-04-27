using ApiServer.Application.Ports;
using ApiServer.Presentation.Http.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers;

[ApiController]
[Route("rooms")]
public class RoomController : ControllerBase
{
    private readonly ApiServer.Domain.WaitingRoom.WaitingRoomService _roomService;
    private readonly ILogger<RoomController> _logger;

    public RoomController(ApiServer.Domain.WaitingRoom.WaitingRoomService roomService, ILogger<RoomController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<RoomListResponse>> GetList([FromQuery] int limit = 20, [FromQuery] string cursor = "")
    {
        _logger.LogInformation("GetList Request: Limit={limit}, Cursor={cursor}", limit, cursor);

        if (limit > 50) limit = 50;
        
        var (rooms, nextCursor) = await _roomService.GetListAsync(limit, cursor);
        
        _logger.LogInformation("GetList Result: Count={count}, NextCursor={next}", rooms.Count, nextCursor);

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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest req)
    {
        var uid = HttpContext.Items["uid"]?.ToString() ?? "";
        _logger.LogInformation("CreateRoom Request: Uid={uid}, ReqRoomId={reqId}, Map={map}", uid, req.roomId, req.mapId);

        if (string.IsNullOrEmpty(uid)) return Unauthorized();

        var name = HttpContext.Items["name"]?.ToString() ?? uid; // Fallback to UID if name missing
        
        // If title is missing, use roomId (from client input) or Default
        var title = !string.IsNullOrEmpty(req.title) ? req.title : 
                   !string.IsNullOrEmpty(req.roomId) ? req.roomId : $"{name}'s Room";

        var newId = await _roomService.CreateAsync(
            title, req.mapId, req.maxPlayers, uid, name, req.useP2PRelay);

        if (newId == null) 
        {
            _logger.LogWarning("CreateRoom Failed: Uid={uid}", uid);
            return BadRequest("Create Failed");
        }

        _logger.LogInformation("CreateRoom Success: NewRoomId={newId}", newId);

        return Ok(new { roomId = newId });
    }

    public class CreateRoomRequest
    {
        public string roomId { get; set; }
        public string title { get; set; }
        public string mapId { get; set; }
        public int maxPlayers { get; set; }
        public bool useP2PRelay { get; set; }
    }
}

using ApiServer.Domain.Town;
using ApiServer.Presentation.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers;

[ApiController]
[Route("townRooms")]
public sealed class TownRoomController : ControllerBase
{
    private readonly TownRoomService _townRooms;
    private readonly ILogger<TownRoomController> _logger;

    public TownRoomController(TownRoomService townRooms, ILogger<TownRoomController> logger)
    {
        _townRooms = townRooms;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<TownRoomListResponse>> GetList(
        [FromQuery] string mapId = "",
        [FromQuery] int limit = 20,
        [FromQuery] string cursor = "")
    {
        var (rooms, nextCursor) = await _townRooms.GetListAsync(mapId ?? "", limit, cursor ?? "");
        return Ok(new TownRoomListResponse
        {
            nextCursor = nextCursor,
            rooms = rooms.Select(ToSummary).ToList()
        });
    }

    [HttpGet("{roomId}")]
    public async Task<ActionResult<TownRoomSummaryResponse>> Get(string roomId)
    {
        var room = await _townRooms.GetAsync(roomId);
        if (room == null)
            return NotFound("TownRoomNotFound");

        return Ok(ToSummary(room));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTownRoomRequest req)
    {
        var uid = HttpContext.RequireUid();
        var name = HttpContext.Items["name"]?.ToString() ?? uid;
        var title = !string.IsNullOrWhiteSpace(req.title)
            ? req.title
            : $"{name}'s Town";

        var room = await _townRooms.CreateAsync(
            title,
            req.mapId ?? "",
            req.maxPlayers,
            uid,
            name,
            req.steamId64 ?? "",
            req.clientVersion ?? "");

        if (room == null)
            return BadRequest("CreateTownRoomFailed");

        _logger.LogInformation("[TownRoom] Create uid={Uid} room={RoomId} map={MapId}", uid, room.RoomId, room.MapId);
        return Ok(new CreateTownRoomResponse { roomId = room.RoomId, room = ToSummary(room) });
    }

    [HttpPost("{roomId}/join")]
    public async Task<IActionResult> Join(string roomId, [FromBody] JoinTownRoomRequest req)
    {
        var uid = HttpContext.RequireUid();
        var name = HttpContext.Items["name"]?.ToString() ?? uid;
        var joined = await _townRooms.JoinAsync(
            roomId,
            uid,
            name,
            req.steamId64 ?? "",
            req.clientVersion ?? "");

        if (!joined.ok)
            return BadRequest(joined.error);

        var room = await _townRooms.GetAsync(roomId);
        return Ok(new JoinTownRoomResponse { room = room != null ? ToSummary(room) : null });
    }

    [HttpPost("{roomId}/leave")]
    public async Task<IActionResult> Leave(string roomId)
    {
        var uid = HttpContext.RequireUid();
        await _townRooms.LeaveAsync(roomId, uid);
        return NoContent();
    }

    [HttpPost("{roomId}/steam-lobby")]
    public async Task<IActionResult> BindSteamLobby(string roomId, [FromBody] BindTownSteamLobbyRequest req)
    {
        var uid = HttpContext.RequireUid();
        var ok = await _townRooms.BindSteamLobbyAsync(roomId, uid, req.steamLobbyId ?? "");
        return ok ? NoContent() : BadRequest("BindSteamLobbyFailed");
    }

    [HttpPost("{roomId}/game-room")]
    public async Task<IActionResult> SetActiveGameRoom(string roomId, [FromBody] SetTownGameRoomRequest req)
    {
        var uid = HttpContext.RequireUid();
        var result = await _townRooms.SetActiveGameRoomAsync(
            roomId,
            uid,
            req.gameRoomId ?? "",
            req.mapId ?? "",
            req.title ?? "");

        if (!result.ok)
            return BadRequest(result.error);

        var room = await _townRooms.GetAsync(roomId);
        return Ok(new TownGameRoomResponse { room = room != null ? ToSummary(room) : null });
    }

    [HttpPost("{roomId}/game-room/clear")]
    public async Task<IActionResult> ClearActiveGameRoom(string roomId)
    {
        var uid = HttpContext.RequireUid();
        var result = await _townRooms.ClearActiveGameRoomAsync(roomId, uid);
        if (!result.ok)
            return BadRequest(result.error);

        var room = await _townRooms.GetAsync(roomId);
        return Ok(new TownGameRoomResponse { room = room != null ? ToSummary(room) : null });
    }

    private static TownRoomSummaryResponse ToSummary(TownRoomDto room)
    {
        return new TownRoomSummaryResponse
        {
            roomId = room.RoomId,
            title = room.Title,
            mapId = room.MapId,
            maxPlayers = room.MaxPlayers,
            memberCount = room.Participants.Count,
            status = room.Status,
            ownerUid = room.OwnerUid,
            hostUid = room.HostUid,
            steamLobbyId = room.SteamLobbyId,
            activeGameRoomId = room.ActiveGameRoomId,
            activeGameMapId = room.ActiveGameMapId,
            activeGameTitle = room.ActiveGameTitle,
            activeGameHostUid = room.ActiveGameHostUid,
            activeGameCreatedAtMs = room.ActiveGameCreatedAtMs,
            createdAtMs = room.CreatedAtMs
        };
    }

    public sealed class CreateTownRoomRequest
    {
        public string title { get; set; } = "";
        public string mapId { get; set; } = "";
        public int maxPlayers { get; set; } = 16;
        public string steamId64 { get; set; } = "";
        public string clientVersion { get; set; } = "";
    }

    public sealed class JoinTownRoomRequest
    {
        public string steamId64 { get; set; } = "";
        public string clientVersion { get; set; } = "";
    }

    public sealed class BindTownSteamLobbyRequest
    {
        public string steamLobbyId { get; set; } = "";
    }

    public sealed class SetTownGameRoomRequest
    {
        public string gameRoomId { get; set; } = "";
        public string mapId { get; set; } = "";
        public string title { get; set; } = "";
    }

    public sealed class CreateTownRoomResponse
    {
        public string roomId { get; set; } = "";
        public TownRoomSummaryResponse? room { get; set; }
    }

    public sealed class JoinTownRoomResponse
    {
        public TownRoomSummaryResponse? room { get; set; }
    }

    public sealed class TownGameRoomResponse
    {
        public TownRoomSummaryResponse? room { get; set; }
    }

    public sealed class TownRoomListResponse
    {
        public List<TownRoomSummaryResponse> rooms { get; set; } = new();
        public string nextCursor { get; set; } = "0";
    }

    public sealed class TownRoomSummaryResponse
    {
        public string roomId { get; set; } = "";
        public string title { get; set; } = "";
        public string mapId { get; set; } = "";
        public int maxPlayers { get; set; }
        public int memberCount { get; set; }
        public string status { get; set; } = "";
        public string ownerUid { get; set; } = "";
        public string hostUid { get; set; } = "";
        public string steamLobbyId { get; set; } = "";
        public string activeGameRoomId { get; set; } = "";
        public string activeGameMapId { get; set; } = "";
        public string activeGameTitle { get; set; } = "";
        public string activeGameHostUid { get; set; } = "";
        public long activeGameCreatedAtMs { get; set; }
        public long createdAtMs { get; set; }
    }
}

using System.Diagnostics;
using Contracts.Packet;
using Lobby.Api.WebSockets;
using Lobby.Domain.Auth.Interface;
using Lobby.Domain.Rooms;
using Lobby.Domain.Shared;
using Lobby.Infrastructure.CrossCutting.Logging;
using Lobby.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Lobby.Api.Http;

[ApiController]
[Route("rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomRepository _repo;
    private readonly IRoomReadModel _read;
    private readonly ConnectionRegistry _conns;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(
        IRoomRepository repo,
        IRoomReadModel read,
        ConnectionRegistry connection,
        ILogger<RoomsController> logger)
    {
        _repo = repo;
        _read = read;
        _conns = connection;
        _logger = logger;
    }

    // --------------------------
    // GET /rooms
    // --------------------------
    [HttpGet]
    public async Task<IActionResult> GetRooms([FromQuery] int pageSize = 50, [FromQuery] string? cursor = null)
    {
        using var scope = _logger.BeginScopeWithTrace(HttpContext);
        var sw = Stopwatch.StartNew();

        var (etag, rooms) = await _read.GetSnapshotWithEtagAsync(Math.Clamp(pageSize, 1, 100), cursor);

        // 캐시 적중 304
        if (Request.Headers.IfNoneMatch.Contains(etag))
        {
            RoomsLogs.RoomsGet304(_logger, etag, sw.Elapsed.TotalMilliseconds);
            return StatusCode(304);
        }

        // 200 OK
        Response.Headers.ETag = etag;

        var res = new GetRoomsRes
        {
            Rooms = rooms.Select(r => r.ToContract()).ToList(),
            NextCursor = null,
            ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Version = "1.0.0"
        };

        RoomsLogs.RoomsGetOk(_logger, res.Rooms.Count, etag, sw.Elapsed.TotalMilliseconds);
        return Ok(res);
    }

    // --------------------------
    // POST /rooms
    // --------------------------
    [EnableRateLimiting("create_room")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomReq req)
    {
        using var scope = _logger.BeginScopeWithTrace(HttpContext);
        var sw = Stopwatch.StartNew();

        //  미들웨어에서 이미 인증 완료됨
        var userId = HttpContext.Items["UserId"]?.ToString();
        if (userId is null)
            return Unauthorized(new { error = "unauthorized", traceId = HttpContext.TraceIdentifier });

        IDictionary<string, object> userClaims = HttpContext.Items["UserClaims"] as Dictionary<string, object> ?? new();
        string userName = userClaims.TryGetValue("name", out var v) ? v?.ToString() ?? "Player" : "Player";

        try
        {
            // --- 방 생성 ---
            var room = new Room
            {
                Id = $"r_{Guid.NewGuid():N}"[..8],
                Title = req.Title,
                Map = req.Map,
                MaxPlayers = Math.Clamp(req.Max, 2, 4),
                Visibility = Enum.Parse<RoomVisibility>(req.Visibility, true),
                UpdatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await _repo.CreateAsync(room);
/*            await _repo.TryJoinAsync(room.Id, new Member
            {
                UserId = userId,
                Name = userName,
                Slot = 1,
                Ready = false
            });*/

            var wsUrl = $"{(Request.Scheme == "https" ? "wss" : "ws")}://{Request.Host}/ws/room/{room.Id}";

            RoomsLogs.RoomsCreateOk(
                _logger, room.Id, userId, room.Map, room.MaxPlayers,
                room.Visibility.ToString(), wsUrl, sw.Elapsed.TotalMilliseconds
            );

            return Ok(new CreateRoomRes { RoomId = room.Id, WsUrl = wsUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ROOM-ERR] Create failed | user={UserId} | trace={TraceId} | msg={Msg}",
                userId ?? "(unknown)", HttpContext.TraceIdentifier, ex.Message);

            return StatusCode(500, new { error = "internal_error", message = ex.Message });
        }
    }

    // --------------------------
    // POST /rooms/{id}/join
    // --------------------------
    [EnableRateLimiting("join_room")]
    [HttpPost("{id}/join")]
    public async Task<IActionResult> Join(string id)
    {
        using var scope = _logger.BeginScopeWithTrace(HttpContext);
        var sw = Stopwatch.StartNew();

        var userId = HttpContext.Items["UserId"]?.ToString();
        if (userId is null)
            return Unauthorized(new { error = "unauthorized", traceId = HttpContext.TraceIdentifier });

        var userClaims = HttpContext.Items["UserClaims"] as Dictionary<string, object> ?? new();
        var userName = userClaims.TryGetValue("name", out var v) ? v?.ToString() ?? "Player" : "Player";

        // --- 방 검증 ---
        var room = await _repo.GetAsync(id);
        if (room is null)
        {
            RoomsLogs.RoomsJoinFail(_logger, id, "room_not_found", sw.Elapsed.TotalMilliseconds);
            return NotFound(new ErrorRes { Error = "room_not_found", TraceId = HttpContext.TraceIdentifier });
        }
        if (room.Status is RoomStatus.Starting or RoomStatus.Closed)
        {
            RoomsLogs.RoomsJoinFail(_logger, id, "room_closed", sw.Elapsed.TotalMilliseconds);
            return BadRequest(new ErrorRes { Error = "room_closed", TraceId = HttpContext.TraceIdentifier });
        }

        // --- Join 처리 ---
        var okJoin = await _repo.TryJoinAsync(id, new Member
        {
            UserId = userId,
            Name = userName,
            Slot = room.CurPlayers + 1,
            Ready = false
        });
        //Console.WriteLine($"OKJoin = {okJoin}");    // TMP
        if (!okJoin)
        {
            string reason = (room.CurPlayers >= room.MaxPlayers) ? "room_full" : "room_closed";
            RoomsLogs.RoomsJoinFail(_logger, id, reason, sw.Elapsed.TotalMilliseconds);
            return BadRequest(new ErrorRes { Error = reason, TraceId = HttpContext.TraceIdentifier });
        }

        // --- Broadcast: 새 멤버 참가 통보 ---
        if (room.Members.TryGetValue(userId, out var me))
        {
            await _conns.BroadcastAsync(id, new MemberJoinMsg { Member = me.ToContract() });
            await _conns.BroadcastAsync(id, new RoomUpdateMsg
            {
                Patch = new RoomPatch
                {
                    Cur = room.CurPlayers,
                    Status = room.Status.ToString(),
                    UpdatedAtMs = room.UpdatedAtMs
                }
            });

            RoomsLogs.RoomsJoinOk(_logger, id, userId, me.Slot, room.CurPlayers, room.Status.ToString(), sw.Elapsed.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning("Join inconsistency: member not found in room after join, id={RoomId}, user={UserId}", id, userId);
        }

        var wsUrl = $"{(Request.Scheme == "https" ? "wss" : "ws")}://{Request.Host}/ws/room/{id}";
        return Ok(new JoinRoomRes { WsUrl = wsUrl, Room = room.ToContract() });
    }
}

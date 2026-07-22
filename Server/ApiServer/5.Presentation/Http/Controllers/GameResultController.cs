using ApiServer.Domain.GameResult;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers.Game;

[ApiController]
[Route("api/game/result")]
public sealed class GameResultController : ControllerBase
{
    private readonly GameResultService _service;
    private readonly ILogger<GameResultController> _logger;

    public GameResultController(
        GameResultService service,
        ILogger<GameResultController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Submit(
        [FromBody] GameResultReportRequest request,
        CancellationToken ct)
    {
        if (!IsSystemRequest())
            return Unauthorized();

        if (request == null ||
            string.IsNullOrWhiteSpace(request.MatchId) ||
            string.IsNullOrWhiteSpace(request.RoomId) ||
            request.ReportedPlayTimeMs < 0 ||
            request.VerifiedPlayTimeMs < 0 ||
            request.TotalDamage < 0)
        {
            return BadRequest("Invalid payload");
        }

        try
        {
            var result = await _service.SaveAsync(request, ct);

            _logger.LogInformation(
                "[GameResult] Submit match={MatchId} room={RoomId} status={Status} archive={ArchiveStatus} clear={Clear} dup={Dup}",
                result.Record.MatchId,
                result.Record.RoomId,
                result.Status,
                result.ArchiveStatus,
                result.Record.IsClear,
                result.IsDuplicate);

            return Ok(new GameResultSubmitResponse
            {
                MatchId = result.Record.MatchId,
                RoomId = result.Record.RoomId,
                MapId = result.Record.MapId,
                HostUid = result.Record.HostUid,
                HostActorId = result.Record.HostActorId,
                IsClear = result.Record.IsClear,
                ReportedPlayTimeMs = result.Record.ReportedPlayTimeMs,
                VerifiedPlayTimeMs = result.Record.VerifiedPlayTimeMs,
                TotalDamage = result.Record.TotalDamage,
                PlayerCount = result.Record.PlayerUids.Length,
                Status = result.Status,
                ArchiveStatus = result.ArchiveStatus.ToString(),
                Duplicate = result.IsDuplicate,
                StoredAtMs = result.Record.StoredAtMs
            });
        }
        catch (GameResultArchiveConflictException ex)
        {
            _logger.LogWarning(
                ex,
                "[GameResult] Conflicting retry rejected match={MatchId}",
                ex.MatchId);

            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Game result conflict",
                Detail = "A different result already exists for this MatchId."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid game result",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{matchId}")]
    public async Task<IActionResult> GetByMatchId(
        string matchId,
        CancellationToken ct)
    {
        if (!_service.ArchiveEnabled)
            return MongoUnavailable();

        var result = await _service.FindByMatchIdAsync(matchId, ct);
        if (result == null)
            return NotFound();

        if (!CanRead(result.PlayerUids))
            return Forbid();

        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? uid,
        [FromQuery] string? mapId,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (!_service.ArchiveEnabled)
            return MongoUnavailable();

        var callerUid = CurrentUid();
        var targetUid = string.IsNullOrWhiteSpace(uid) ? callerUid : uid.Trim();
        if (string.IsNullOrWhiteSpace(targetUid))
            return Unauthorized();

        if (!IsSystemRequest() &&
            !string.Equals(callerUid, targetUid, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var results = await _service.FindByPlayerAsync(
            targetUid,
            string.IsNullOrWhiteSpace(mapId) ? null : mapId.Trim(),
            limit <= 0 ? 20 : limit,
            ct);

        return Ok(results);
    }

    private bool CanRead(IEnumerable<string> playerUids)
    {
        if (IsSystemRequest())
            return true;

        var callerUid = CurrentUid();
        return !string.IsNullOrWhiteSpace(callerUid) &&
               playerUids.Contains(callerUid, StringComparer.OrdinalIgnoreCase);
    }

    private ObjectResult MongoUnavailable()
    {
        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Game-result archive unavailable",
                Detail = "MongoDB game-result archive is disabled."
            });
    }

    private string CurrentUid() => HttpContext.Items["uid"]?.ToString() ?? "";

    private bool IsSystemRequest()
        => string.Equals(CurrentUid(), "SYSTEM", StringComparison.OrdinalIgnoreCase);
}

public sealed class GameResultSubmitResponse
{
    public string MatchId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public string MapId { get; set; } = "";
    public string HostUid { get; set; } = "";
    public int HostActorId { get; set; }
    public bool IsClear { get; set; }
    public long ReportedPlayTimeMs { get; set; }
    public long VerifiedPlayTimeMs { get; set; }
    public int TotalDamage { get; set; }
    public int PlayerCount { get; set; }
    public string Status { get; set; } = "";
    public string ArchiveStatus { get; set; } = "";
    public bool Duplicate { get; set; }
    public long StoredAtMs { get; set; }
}
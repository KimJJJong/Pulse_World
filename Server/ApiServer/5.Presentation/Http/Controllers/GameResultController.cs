using System;
using System.Threading;
using System.Threading.Tasks;
using ApiServer.Domain.GameResult;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers.Game;

[ApiController]
[Route("api/game/result")]
public sealed class GameResultController : ControllerBase
{
    private readonly GameResultService _service;
    private readonly ILogger<GameResultController> _logger;

    public GameResultController(GameResultService service, ILogger<GameResultController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] GameResultReportRequest request, CancellationToken ct)
    {
        if (!IsSystemRequest())
            return Unauthorized();

        if (request == null || string.IsNullOrWhiteSpace(request.RoomId))
            return BadRequest("Invalid payload");

        var result = await _service.SaveAsync(request, ct);

        _logger.LogInformation(
            "[GameResult] Submit room={RoomId} status={Status} clear={Clear} dup={Dup}",
            result.Record.RoomId,
            result.Status,
            result.Record.IsClear,
            result.IsDuplicate);

        return Ok(new GameResultSubmitResponse
        {
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
            Duplicate = result.IsDuplicate,
            StoredAtMs = result.Record.StoredAtMs
        });
    }

    private bool IsSystemRequest()
        => string.Equals(HttpContext.Items["uid"]?.ToString(), "SYSTEM", StringComparison.OrdinalIgnoreCase);
}

public sealed class GameResultSubmitResponse
{
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
    public bool Duplicate { get; set; }
    public long StoredAtMs { get; set; }
}

using System;
using ApiServer.Domain.GameMatch;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers.Game;

[ApiController]
[Route("api/game/match-manifest")]
public sealed class GameMatchController : ControllerBase
{
    private readonly GameMatchService _service;

    public GameMatchController(GameMatchService service)
    {
        _service = service;
    }

    [HttpGet("{roomId}")]
    public async Task<IActionResult> GetByRoomId(string roomId, CancellationToken ct)
    {
        if (!IsSystemRequest())
            return Unauthorized();

        var manifest = await _service.GetByRoomIdAsync(roomId, ct);
        if (manifest == null)
            return NotFound();

        return Ok(manifest);
    }

    private bool IsSystemRequest()
        => string.Equals(HttpContext.Items["uid"]?.ToString(), "SYSTEM", StringComparison.OrdinalIgnoreCase);
}

using ApiServer.Domain.Town;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers.Town;

[ApiController]
[Route("api/town/match-manifest")]
public sealed class TownMatchController : ControllerBase
{
    private readonly TownRoomService _townRooms;

    public TownMatchController(TownRoomService townRooms)
    {
        _townRooms = townRooms;
    }

    [HttpGet("{roomId}")]
    public async Task<IActionResult> GetByRoomId(string roomId, CancellationToken ct)
    {
        if (!IsSystemRequest())
            return Unauthorized();

        var manifest = await _townRooms.GetManifestAsync(roomId, ct);
        if (manifest == null)
            return NotFound();

        return Ok(manifest);
    }

    private bool IsSystemRequest()
        => string.Equals(HttpContext.Items["uid"]?.ToString(), "SYSTEM", StringComparison.OrdinalIgnoreCase);
}

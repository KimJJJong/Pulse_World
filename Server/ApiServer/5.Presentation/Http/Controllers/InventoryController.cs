using ApiServer.Domain.Items;
using ApiServer.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryRepository _repository;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IInventoryRepository repository, ILogger<InventoryController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // GET api/inventory/{uid}
    // Called by GameServer on User Login
    [HttpGet("{uid}")]
    public async Task<IActionResult> GetInventory(string uid)
    {
        // Security Check: Only GameServer (System) or the User themselves should access (if we allow Client access later)
        // For now, let's assume this is mostly for GameServer via API Key, or User with Token.
        // If it's GameServer, it has "SYSTEM" role.
        
        var items = await _repository.GetItemsAsync(uid);
        var equipments = await _repository.GetEquipmentsAsync(uid);

        return Ok(new
        {
            items,
            equipments
        });
    }

    // POST api/inventory/{uid}
    // Called by GameServer on Save
    [HttpPost("{uid}")]
    public async Task<IActionResult> UpdateInventory(string uid, [FromBody] UpdateInventoryRequest request)
    {
        // 1. Validate (Simple check)
        if (request.Items == null || request.Equipments == null)
        {
            return BadRequest("Invalid payload");
        }

        // 2. Update DB
        await _repository.UpdateInventoryAsync(uid, request.Items, request.Equipments);

        return Ok();
    }
}

public class UpdateInventoryRequest
{
    public List<Item> Items { get; set; } = new();
    public List<Equipment> Equipments { get; set; } = new();
}

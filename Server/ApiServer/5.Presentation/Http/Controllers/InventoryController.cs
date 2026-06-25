using ApiServer.Domain.Items;
using ApiServer.Application.Services;
using ApiServer.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryRepository _repository;
    private readonly IStarterEquipmentService _starterEquipmentService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IInventoryRepository repository,
        IStarterEquipmentService starterEquipmentService,
        ILogger<InventoryController> logger)
    {
        _repository = repository;
        _starterEquipmentService = starterEquipmentService;
        _logger = logger;
    }

    // GET api/inventory/{uid}
    // Called by GameServer on User Login
    [HttpGet("{uid}")]
    public async Task<IActionResult> GetInventory(string uid)
    {
        var allItems = await _starterEquipmentService.EnsureStarterEquipmentAsync(uid);

        // Split by TemplateId range: 100000~399999 = Equipment
        var items = allItems.Where(x => x.TemplateId < 100000 || x.TemplateId >= 400000).ToList();
        var equipments = allItems.Where(x => x.TemplateId >= 100000 && x.TemplateId <= 399999).ToList();

        return Ok(new
        {
            Items = items,
            Equipments = equipments
        });
    }

    // POST api/inventory/{uid}
    // Called by GameServer on Save
    [HttpPost("{uid}")]
    public async Task<IActionResult> UpdateInventory(string uid, [FromBody] UpdateInventoryRequest request)
    {
        // 1. Validate (Simple check)
        if (request.Items == null)
        {
            return BadRequest("Invalid payload");
        }

        // 2. Merge Items + Equipments into single list for DB (single table)
        var allItems = new List<ApiServer.Domain.Items.GameItem>();
        allItems.AddRange(request.Items);
        if (request.Equipments != null)
        {
            allItems.AddRange(request.Equipments);
        }
        
        _logger.LogInformation("[UpdateInventory] uid={Uid} Items={ItemCount} Equipments={EquipCount} Total={Total}",
            uid, request.Items?.Count ?? 0, request.Equipments?.Count ?? 0, allItems.Count);
        foreach (var dbg in allItems)
            _logger.LogInformation("  -> Id={Id} TID={TID} Amt={Amt} Slot={Slot}", dbg.Id, dbg.TemplateId, dbg.Amount, dbg.SlotIndex);

        // 3. Update DB
        await _repository.UpdateInventoryAsync(uid, allItems);

        return Ok();
    }

    // DELETE api/inventory/{uid}/items/{itemId}
    [HttpDelete("{uid}/items/{itemId}")]
    public async Task<IActionResult> DeleteItem(string uid, long itemId)
    {
        await _repository.DeleteItemAsync(uid, itemId);
        return Ok();
    }
}

public class UpdateInventoryRequest
{
    public List<ApiServer.Domain.Items.GameItem> Items { get; set; } = new();
    public List<ApiServer.Domain.Items.GameItem> Equipments { get; set; } = new();
}


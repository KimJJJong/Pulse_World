using ApiServer.Application.Services;
using ApiServer.Domain.Items;
using ApiServer.Presentation.Http;
using ApiServer.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers;

[ApiController]
[Route("api/cheat")]
public class CheatController : ControllerBase
{
    private readonly IInventoryRepository _repository;
    private readonly IItemTemplateService _templates;
    private readonly ILogger<CheatController> _logger;

    public CheatController(
        IInventoryRepository repository,
        IItemTemplateService templates,
        ILogger<CheatController> logger)
    {
        _repository = repository;
        _templates = templates;
        _logger = logger;
    }

    [HttpPost("additem")]
    public async Task<IActionResult> AddItem([FromQuery] string uid, [FromQuery] int templateId, [FromQuery] int amount)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            uid = HttpContext.RequireUid();
        }

        // 0. Validate TemplateId against CSV data
        bool isEquipment = templateId >= 100000 && templateId <= 399999;
        if (isEquipment)
        {
            if (_templates.GetEquipment(templateId) == null)
            {
                _logger.LogWarning("[Cheat] Rejected: Unknown equipment TID {TemplateId}", templateId);
                return BadRequest(new { error = $"Unknown equipment TemplateId: {templateId}" });
            }
        }
        else
        {
            if (_templates.GetItem(templateId) == null)
            {
                _logger.LogWarning("[Cheat] Rejected: Unknown item TID {TemplateId}", templateId);
                return BadRequest(new { error = $"Unknown item TemplateId: {templateId}" });
            }
        }

        if (amount <= 0)
            return BadRequest(new { error = "Amount must be > 0" });

        // 1. Load Inventory
        var inventory = await _repository.GetInventoryAsync(uid);

        // 2. Check if Equipment (TID 100000~399999)

        if (isEquipment)
        {
            // Equipment: Always create individual rows (Amount=1 each)
            for (int i = 0; i < amount; i++)
            {
                var newItem = new GameItem
                {
                    OwnerUid = uid,
                    TemplateId = templateId,
                    Amount = 1,
                    SlotIndex = FindEmptySlot(inventory),
                    IsEquipped = false,
                    EnhancementLevel = 0,
                    BaseStats = "{}",
                    RandomOptions = "{}"
                };
                inventory.Add(newItem);
            }
            _logger.LogInformation("[Cheat] Created {Count} equipment instances of TID {TemplateId}", amount, templateId);
        }
        else
        {
            // Consumable/Material: Stack into existing or create new
            var existing = inventory.FirstOrDefault(x => x.TemplateId == templateId);
            if (existing != null)
            {
                existing.Amount += amount;
            }
            else
            {
                var newItem = new GameItem
                {
                    OwnerUid = uid,
                    TemplateId = templateId,
                    Amount = amount,
                    SlotIndex = FindEmptySlot(inventory),
                    IsEquipped = false,
                    EnhancementLevel = 0,
                    BaseStats = "{}",
                    RandomOptions = "{}"
                };
                inventory.Add(newItem);
            }
            _logger.LogInformation("[Cheat] Added {Amount}x item TID {TemplateId}", amount, templateId);
        }

        // 3. Save
        await _repository.UpdateInventoryAsync(uid, inventory);

        return Ok(new { success = true, added = amount });
    }

    private int FindEmptySlot(List<GameItem> items)
    {
        var usedSlots = new HashSet<int>(items.Select(x => x.SlotIndex));
        for (int i = 0; i < 200; i++)
        {
            if (!usedSlots.Contains(i)) return i;
        }
        return -1;
    }
}

using ApiServer.Domain.Items;
using ApiServer.Infrastructure.Persistence.Repositories;

namespace ApiServer.Application.Services;

public interface IStarterEquipmentService
{
    Task<List<GameItem>> EnsureStarterEquipmentAsync(string uid, IEnumerable<GameItem>? currentItems = null);
}

public class StarterEquipmentService : IStarterEquipmentService
{
    private static readonly Dictionary<string, int> DefaultEquippedBySlot = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Weapon", 100001 },
        { "Shoes", 240001 },
        { "Hat", 200001 },
        { "Accessory", 300001 }
    };

    private readonly IInventoryRepository _repository;
    private readonly IItemTemplateService _templates;
    private readonly ILogger<StarterEquipmentService> _logger;

    public StarterEquipmentService(
        IInventoryRepository repository,
        IItemTemplateService templates,
        ILogger<StarterEquipmentService> logger)
    {
        _repository = repository;
        _templates = templates;
        _logger = logger;
    }

    public async Task<List<GameItem>> EnsureStarterEquipmentAsync(string uid, IEnumerable<GameItem>? currentItems = null)
    {
        var inventory = currentItems?.ToList() ?? await _repository.GetInventoryAsync(uid);
        var ownedTemplateIds = inventory.Select(x => x.TemplateId).ToHashSet();
        var slotsWithEquippedItems = GetEquippedSlots(inventory);

        bool changed = EquipOwnedDefaultItems(inventory, slotsWithEquippedItems);
        foreach (var template in _templates.GetAllEquipments().OrderBy(x => x.Id))
        {
            if (ownedTemplateIds.Contains(template.Id))
                continue;

            bool shouldEquip = DefaultEquippedBySlot.TryGetValue(template.SlotType, out int defaultTemplateId)
                && defaultTemplateId == template.Id
                && !slotsWithEquippedItems.Contains(template.SlotType);

            inventory.Add(new GameItem
            {
                OwnerUid = uid,
                TemplateId = template.Id,
                Amount = 1,
                SlotIndex = FindEmptySlot(inventory),
                IsEquipped = shouldEquip,
                EnhancementLevel = 0,
                BaseStats = "{}",
                RandomOptions = "{}",
                AcquiredAt = DateTimeOffset.UtcNow
            });

            ownedTemplateIds.Add(template.Id);
            if (shouldEquip)
                slotsWithEquippedItems.Add(template.SlotType);

            changed = true;
        }

        if (!changed)
            return inventory;

        await _repository.UpdateInventoryAsync(uid, inventory);
        _logger.LogInformation("[StarterEquipment] ensured starter equipment uid={Uid}", uid);
        return await _repository.GetInventoryAsync(uid);
    }

    private static bool EquipOwnedDefaultItems(List<GameItem> inventory, HashSet<string> slotsWithEquippedItems)
    {
        bool changed = false;
        foreach (var entry in DefaultEquippedBySlot)
        {
            if (slotsWithEquippedItems.Contains(entry.Key))
                continue;

            var item = inventory.FirstOrDefault(x => x.TemplateId == entry.Value);
            if (item == null)
                continue;

            item.IsEquipped = true;
            slotsWithEquippedItems.Add(entry.Key);
            changed = true;
        }

        return changed;
    }

    private HashSet<string> GetEquippedSlots(IEnumerable<GameItem> items)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.Where(x => x.IsEquipped))
        {
            var template = _templates.GetEquipment(item.TemplateId);
            if (template == null)
                continue;

            if (DefaultEquippedBySlot.ContainsKey(template.SlotType))
                result.Add(template.SlotType);
        }

        return result;
    }

    private static int FindEmptySlot(IEnumerable<GameItem> items)
    {
        var usedSlots = items.Select(x => x.SlotIndex).ToHashSet();
        for (int i = 0; i < 200; i++)
        {
            if (!usedSlots.Contains(i))
                return i;
        }

        return -1;
    }
}

using GameServer.Infrastructure.Api;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameServer.Content.Item;

public class InventoryManager
{
    private readonly ILogger<InventoryManager> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IApiServerClient _apiClient;
    private readonly IItemTemplateManager _templates;

    // Changed version to v4 to force clear incompatbile cache (Offset removal)
    private const string InventoryKeyPrefix = "user:{0}:inventory:v4";


    public InventoryManager(
        ILogger<InventoryManager> logger,
        IConnectionMultiplexer redis,
        IApiServerClient apiClient,
        IItemTemplateManager templates)
    {
        _logger = logger;
        _redis = redis;
        _apiClient = apiClient;
        _templates = templates;
    }

    private string GetKey(string uid) => string.Format(InventoryKeyPrefix, uid);

    public async Task<List<ItemInstance>> LoadInventoryAsync(string uid, bool forceReload = false)
    {
        var db = _redis.GetDatabase();
        var key = GetKey(uid);

        // 1. Try Redis (Skip if forceReload is true)
        if (!forceReload)
        {
            var hashEntries = await db.HashGetAllAsync(key);
            if (hashEntries.Length > 0)
            {
                var list = new List<ItemInstance>();
                foreach (var entry in hashEntries)
                {
                    if (entry.Value.IsNullOrEmpty) continue;
                    try
                    {
                        var item = JsonConvert.DeserializeObject<ItemInstance>(entry.Value);
                        if (item != null) list.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse item from redis {Key} {Field}", key, entry.Name);
                    }
                }
                return list;
            }
        }
        else
        {
            _logger.LogInformation("Force Reloading inventory for {Uid} (Skipping Cache)", uid);
        }

        // 2. Fallback to ApiServer
        _logger.LogInformation("Cache miss for inventory {Uid}, loading from ApiServer", uid);
        var response = await _apiClient.GetAsync<InventoryResponse>($"api/inventory/{uid}");
        
        // DEBUG: Log the raw data to check Case Sensitivity
        try 
        {
            var debugJson = JsonConvert.SerializeObject(response);
            _logger.LogInformation($"[LoadInventory] Raw Response for {uid}: {debugJson}");
            if (response != null)
            {
                _logger.LogInformation($"[LoadInventory] Deserialized: Items={response.Items?.Count ?? -1}, Equipments={response.Equipments?.Count ?? -1}");
            }
            else
            {
                _logger.LogWarning($"[LoadInventory] Response is NULL for {uid}");
            }
        }
        catch {}
        
        var loadedItems = new List<ItemInstance>();
        if (response != null)
        {
            // Convert Entity to Instance
            foreach (var e in response.Items)
            {
                loadedItems.Add(new ItemInstance
                {
                    InstanceId = e.Id,
                    TemplateId = e.TemplateId,
                    Amount = e.Amount,
                    SlotIndex = e.SlotIndex,
                    AcquiredAt = e.AcquiredAt,
                    // Type distinction if needed
                });
            }
            foreach (var e in response.Equipments)
            {
                loadedItems.Add(new ItemInstance
                {
                    // RAW ID (No Offset)
                    InstanceId = e.Id,
                    TemplateId = e.TemplateId,
                    SlotIndex = e.SlotIndex,
                    EnhancementLevel = e.EnhancementLevel,
                    IsEquipped = e.IsEquipped,
                    BaseStats = SafeDeserialize(e.BaseStats),
                    RandomOptions = SafeDeserialize(e.RandomOptions),
                    AcquiredAt = e.AcquiredAt
                });
            }

            // 3. Cache to Redis
            if (loadedItems.Any())
            {
                var entries = loadedItems.Select(x => new HashEntry(GetFieldId(x), JsonConvert.SerializeObject(x))).ToArray();
                await db.HashSetAsync(key, entries);
                await db.KeyExpireAsync(key, TimeSpan.FromMinutes(60)); // TTL
            }
        }

        return loadedItems;
    }

    private Dictionary<string, int> SafeDeserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, int>();
        try { return JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>(); }
        catch { return new Dictionary<string, int>(); }
    }

    public async Task SaveInventoryAsync(string uid, List<ItemInstance> items)
    {
        var itemDtos = items.Where(x => !IsEquipment(x.TemplateId)).Select(x => new ItemDto
        {
            Id = x.InstanceId,
            OwnerUid = uid,
            TemplateId = x.TemplateId,
            Amount = x.Amount,
            SlotIndex = x.SlotIndex,
            AcquiredAt = x.AcquiredAt
        }).ToList();

        var equipDtos = items.Where(x => IsEquipment(x.TemplateId)).Select(x => new EquipmentDto
        {
            // RAW ID (No Offset Subtraction)
            Id = x.InstanceId,
            OwnerUid = uid,
            TemplateId = x.TemplateId,
            SlotIndex = x.SlotIndex,
            EnhancementLevel = x.EnhancementLevel,
            IsEquipped = x.IsEquipped,
            BaseStats = JsonConvert.SerializeObject(x.BaseStats),
            RandomOptions = JsonConvert.SerializeObject(x.RandomOptions),
            AcquiredAt = x.AcquiredAt
        }).ToList();

        _logger.LogInformation($"[SaveInventory] Saving for {uid}. Total: {items.Count}. Split -> Items: {itemDtos.Count}, Equips: {equipDtos.Count}");

        var request = new InventoryUpdateRequest
        {
            Items = itemDtos,
            Equipments = equipDtos
        };

        if (!await _apiClient.PostAsync($"api/inventory/{uid}", request))
        {
            _logger.LogError("Failed to save inventory for {Uid}", uid);
            // Retry logic or queueing needed here in production
        }
        else
        {
            _logger.LogInformation("Saved inventory for {Uid}", uid);
            // Invalidate Cache so next Load gets fresh IDs and data from DB
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(GetKey(uid));
        }
    }

    public async Task DeleteItemAsync(string uid, ItemInstance item)
    {
        if (await _apiClient.DeleteAsync($"api/inventory/{uid}/items/{item.InstanceId}"))
        {
             _logger.LogInformation($"[DeleteItem] Successfully deleted item {item.InstanceId} for {uid}");
             var db = _redis.GetDatabase();
             await db.KeyDeleteAsync(GetKey(uid));
        }
    }

    private void AddItemsHelper(List<ItemInstance> inventory, int templateId, int amount)
    {
        var template = _templates.Get(templateId);
        if (template == null) return;

        // 1. Stackable Item
        if (template.MaxStack > 1)
        {
            int remaining = amount;

            // A. Fill existing stacks
            var existingStacks = inventory
                .Where(x => x.TemplateId == templateId && x.Amount < template.MaxStack)
                .OrderBy(x => x.SlotIndex) // Fill from front
                .ToList();

            foreach (var stack in existingStacks)
            {
                int space = template.MaxStack - stack.Amount;
                int add = Math.Min(remaining, space);
                
                stack.Amount += add;
                remaining -= add;

                if (remaining <= 0) break;
            }

            // B. Create new stacks for remaining
            while (remaining > 0)
            {
                int add = Math.Min(remaining, template.MaxStack);
                inventory.Add(new ItemInstance
                {
                    InstanceId = 0, // Pending ID
                    TemplateId = templateId,
                    Amount = add,
                    SlotIndex = FindEmptySlot(inventory),
                    AcquiredAt = DateTimeOffset.UtcNow
                });
                remaining -= add;
            }
        }
        else
        {
            // 2. Non-Stackable (Equipment etc)
            for (int i = 0; i < amount; i++)
            {
                inventory.Add(new ItemInstance
                {
                    InstanceId = 0, // Pending ID
                    TemplateId = templateId,
                    Amount = 1,
                    SlotIndex = FindEmptySlot(inventory),
                    AcquiredAt = DateTimeOffset.UtcNow
                });
            }
        }
    }

    private int FindEmptySlot(List<ItemInstance> inventory)
    {
        var usedSlots = new HashSet<int>(inventory.Select(x => x.SlotIndex));
        for (int i = 0; i < 1000; i++) // Max 1000 slots check
        {
            if (!usedSlots.Contains(i)) return i;
        }
        return -1; // Full
    }


    private string GetFieldId(ItemInstance item)
    {
        if (item.InstanceId > 0) return item.InstanceId.ToString();
        // Fallback for pending items (should be saved first usually, but for safety)
        if (IsEquipment(item.TemplateId)) return $"e_{item.SlotIndex}";
        return $"i_{item.SlotIndex}";
    }

    private bool IsEquipment(int tid)
    {
        // Weapon: 100,000 ~ 199,999
        // Armor/Gear: 200,000 ~ 299,999
        // Accessory: 300,000 ~ 399,999
        if (tid >= 100000 && tid <= 399999) return true;
        
        return false;
    }

    /// <summary>
    /// Re-organizes the inventory:
    /// 1. Merges stackable items (same TemplateId) up to MaxStack.
    /// 2. Ensures equipments are never stacked (Amount 1).
    /// </summary>
    public void CompactInventory(List<ItemInstance> items)
    {
        // Separate logic for Equipment and Items
        var equipments = items.Where(x => IsEquipment(x.TemplateId)).ToList();
        var consumables = items.Where(x => !IsEquipment(x.TemplateId)).ToList();

        // 1. Process Equipments: Ensure Amount is 1
        foreach (var eq in equipments)
        {
            if (eq.Amount != 1) 
            {
                _logger.LogWarning($"[Compact] Equipment {eq.InstanceId} had Amount {eq.Amount}. Resetting to 1.");
                eq.Amount = 1;
            }
        }

        // 2. Process Consumables: Merge Stacks
        // Group by TemplateId
        var groups = consumables.GroupBy(x => x.TemplateId).ToList();
        
        // Clear original list of consumables to rebuild
        foreach (var c in consumables) items.Remove(c);

        foreach (var group in groups)
        {
            int templateId = group.Key;
            var tmpl = _templates.Get(templateId);
            int maxStack = tmpl?.MaxStack ?? 99; // Default 99 if not found
            if (maxStack <= 0) maxStack = 99; // Safety fallback

            // Calculate total amount
            long totalAmount = group.Sum(x => (long)x.Amount);
            
            // Existing instances to reuse (preserve InstanceId)
            var existingInstances = group.OrderBy(x => x.SlotIndex).ToList();
            int instanceIdx = 0;

            while (totalAmount > 0)
            {
                int chunk = (int)Math.Min(totalAmount, maxStack);
                
                _logger.LogInformation($"[Compact] TID:{templateId} Total:{totalAmount} Max:{maxStack} -> Chunk:{chunk} (InstIdx:{instanceIdx})");

                if (instanceIdx < existingInstances.Count)
                {
                    // Reuse existing
                    var inst = existingInstances[instanceIdx];
                    inst.Amount = chunk;
                    items.Add(inst); // Add back to main list
                    instanceIdx++;
                }
                else
                {
                     // Overflow - Should not happen if we are strictly merging, 
                     // unless Total > ExistingInstances * MaxStack (e.g. cheats or logic bug increasing amount)
                     // In that case, we might lose items or need to create new. 
                     // For now, let's log error.
                     _logger.LogWarning($"[Compact] Overflow! No more instances to hold {totalAmount} items of TID {templateId}. Items invalidly discarded or need new instance logic.");
                }
                totalAmount -= chunk;
            }

            // Remove unused instances (merged into others)
            // effective removing is done by not adding them back to 'items' list
        }
    }

    // DTOs for ApiServer communication
    private class InventoryResponse
    {
        [JsonProperty("Items")]
        public List<ItemDto> Items { get; set; } = new();
        
        [JsonProperty("Equipments")]
        public List<EquipmentDto> Equipments { get; set; } = new();
    }

    private class InventoryUpdateRequest
    {
        [JsonProperty("Items")]
        public List<ItemDto> Items { get; set; } = new();

        [JsonProperty("Equipments")]
        public List<EquipmentDto> Equipments { get; set; } = new();
    }

    private class ItemDto
    {
        [JsonProperty("Id")]
        public long Id { get; set; }

        [JsonProperty("OwnerUid")]
        public string OwnerUid { get; set; }

        [JsonProperty("TemplateId")]
        public int TemplateId { get; set; }

        [JsonProperty("Amount")]
        public int Amount { get; set; }

        [JsonProperty("SlotIndex")]
        public int SlotIndex { get; set; }

        [JsonProperty("acquiredAt")]
        public DateTimeOffset AcquiredAt { get; set; }
    }

    private class EquipmentDto
    {
        [JsonProperty("Id")]
        public long Id { get; set; }

        [JsonProperty("OwnerUid")]
        public string OwnerUid { get; set; }

        [JsonProperty("TemplateId")]
        public int TemplateId { get; set; }

        [JsonProperty("SlotIndex")]
        public int SlotIndex { get; set; }

        [JsonProperty("EnhancementLevel")]
        public int EnhancementLevel { get; set; }

        [JsonProperty("IsEquipped")]
        public bool IsEquipped { get; set; }

        [JsonProperty("BaseStats")]
        public string BaseStats { get; set; } = "{}";

        [JsonProperty("RandomOptions")]
        public string RandomOptions { get; set; } = "{}";

        [JsonProperty("acquiredAt")]
        public DateTimeOffset AcquiredAt { get; set; }
    }
}

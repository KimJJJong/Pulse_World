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

    private const string InventoryKeyPrefix = "user:{0}:inventory";

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

    public async Task<List<ItemInstance>> LoadInventoryAsync(string uid)
    {
        var db = _redis.GetDatabase();
        var key = GetKey(uid);

        // 1. Try Redis
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

        // 2. Fallback to ApiServer
        _logger.LogInformation("Cache miss for inventory {Uid}, loading from ApiServer", uid);
        var response = await _apiClient.GetAsync<InventoryResponse>($"api/inventory/{uid}");
        
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
                    // Type distinction if needed
                });
            }
            foreach (var e in response.Equipments)
            {
                loadedItems.Add(new ItemInstance
                {
                    InstanceId = e.Id,
                    TemplateId = e.TemplateId,
                    SlotIndex = e.SlotIndex,
                    EnhancementLevel = e.EnhancementLevel,
                    IsEquipped = e.IsEquipped,
                    BaseStats = SafeDeserialize(e.BaseStats),
                    RandomOptions = SafeDeserialize(e.RandomOptions)
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
        // Simple full-sync logic for now
        var request = new InventoryUpdateRequest
        {
            Items = items.Where(x => !IsEquipment(x.TemplateId)).Select(x => new ItemDto
            {
                Id = x.InstanceId,
                OwnerUid = uid,
                TemplateId = x.TemplateId,
                Amount = x.Amount,
                SlotIndex = x.SlotIndex
            }).ToList(),
            Equipments = items.Where(x => IsEquipment(x.TemplateId)).Select(x => new EquipmentDto
            {
                Id = x.InstanceId,
                OwnerUid = uid,
                TemplateId = x.TemplateId,
                SlotIndex = x.SlotIndex,
                EnhancementLevel = x.EnhancementLevel,
                IsEquipped = x.IsEquipped,
                BaseStats = JsonConvert.SerializeObject(x.BaseStats),
                RandomOptions = JsonConvert.SerializeObject(x.RandomOptions)
            }).ToList()
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

    private string GetFieldId(ItemInstance item)
    {
        // Simple keying: i_{slot} or e_{slot}
        // But for now let's just use unique ID if available, or slot index if not.
        // Assuming unique slot indexes across all items? Or separate bags?
        // Let's assume shared slot space for simplicity, or prefix.
        if (IsEquipment(item.TemplateId)) return $"e_{item.SlotIndex}";
        return $"i_{item.SlotIndex}";
    }

    private bool IsEquipment(int tid)
    {
        var tmpl = _templates.Get(tid);
        return tmpl != null && tmpl.Type == ItemType.Equipment;
    }

    // DTOs for ApiServer communication
    private class InventoryResponse
    {
        public List<ItemDto> Items { get; set; } = new();
        public List<EquipmentDto> Equipments { get; set; } = new();
    }

    private class InventoryUpdateRequest
    {
        public List<ItemDto> Items { get; set; } = new();
        public List<EquipmentDto> Equipments { get; set; } = new();
    }

    private class ItemDto
    {
        public long Id { get; set; }
        public string OwnerUid { get; set; }
        public int TemplateId { get; set; }
        public int Amount { get; set; }
        public int SlotIndex { get; set; }
    }

    private class EquipmentDto
    {
        public long Id { get; set; }
        public string OwnerUid { get; set; }
        public int TemplateId { get; set; }
        public int SlotIndex { get; set; }
        public int EnhancementLevel { get; set; }
        public bool IsEquipped { get; set; }
        public string BaseStats { get; set; } = "{}";
        public string RandomOptions { get; set; } = "{}";
    }
}

using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.IO;
using System;

namespace GameServer.Content.Item;

public interface IItemTemplateManager
{
    void Load();
    ItemTemplate? Get(int id);
    EquipmentTemplate? GetEquipment(int id);
}

public class ItemTemplateManager : IItemTemplateManager
{
    private readonly ILogger<ItemTemplateManager> _logger;
    private readonly IHostEnvironment _env;

    private Dictionary<int, ItemTemplate> _items = new();
    private Dictionary<int, EquipmentTemplate> _equipments = new();

    public ItemTemplateManager(ILogger<ItemTemplateManager> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public void Load()
    {
        _items.Clear();
        _equipments.Clear();

        string dataPath = Path.Combine(_env.ContentRootPath, "Content", "Data", "Json");
        _logger.LogInformation("Loading Item Data from {Path}", dataPath);

        LoadItems(Path.Combine(dataPath, "Items.json"));
        LoadEquipments(Path.Combine(dataPath, "Equipments.json"));
        
        _logger.LogInformation("Loaded {ItemCount} Items, {EquipCount} Equipments", _items.Count, _equipments.Count);
    }

    private void LoadItems(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("Item file not found: {Path}", path);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var list = JsonConvert.DeserializeObject<List<ItemTemplate>>(json);
            if (list == null) return;

            foreach (var item in list)
            {
                if (_items.ContainsKey(item.Id))
                {
                    _logger.LogWarning("Duplicate Item ID: {Id}", item.Id);
                    continue;
                }
                _items[item.Id] = item;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load items from {Path}", path);
        }
    }

    private void LoadEquipments(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("Equipment file not found: {Path}", path);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var list = JsonConvert.DeserializeObject<List<EquipmentTemplate>>(json);
            if (list == null) return;

            foreach (var equip in list)
            {
                if (_equipments.ContainsKey(equip.Id))
                {
                    _logger.LogWarning("Duplicate Equipment ID: {Id}", equip.Id);
                    continue;
                }
                _equipments[equip.Id] = equip;
                
                // Also add to _items for generic lookup if needed, checking for conflicts
                if (!_items.ContainsKey(equip.Id))
                {
                    _items[equip.Id] = equip;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load equipments from {Path}", path);
        }
    }

    public ItemTemplate? Get(int id)
    {
        if (_items.TryGetValue(id, out var item)) return item;
        return null;
    }

    public EquipmentTemplate? GetEquipment(int id)
    {
        if (_equipments.TryGetValue(id, out var equip)) return equip;
        return null;
    }
}

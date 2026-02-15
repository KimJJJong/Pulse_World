using Newtonsoft.Json;
using System.Collections.Generic;

namespace GameServer.Content.Item;

public class ItemInstance
{
    // For Stackable Items
    // If it's an equipment, InstanceId will be > 0 (assigned by DB or Snowflake)
    // For simple items, InstanceId might be 0.
    [JsonProperty("id")]
    public long InstanceId { get; set; }

    [JsonProperty("tid")]
    public int TemplateId { get; set; }

    [JsonProperty("amt")]
    public int Amount { get; set; }

    [JsonProperty("slot")]
    public int SlotIndex { get; set; }
    
    // Equipment specific
    [JsonProperty("enchant")]
    public int EnhancementLevel { get; set; }

    [JsonProperty("base")]
    public Dictionary<string, int> BaseStats { get; set; } = new();

    [JsonProperty("opts")]
    public Dictionary<string, int> RandomOptions { get; set; } = new();
    
    [JsonProperty("equipped")]
    public bool IsEquipped { get; set; }

    [JsonIgnore]
    public bool IsDirty { get; set; }
}

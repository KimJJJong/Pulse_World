using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GameServer.Content.Item;

[JsonConverter(typeof(StringEnumConverter))]
public enum ItemType
{
    None = 0,
    Equipment,
    Consumable,
    Material,
    Blueprint,
    Currency
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ItemGrade
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

[JsonConverter(typeof(StringEnumConverter))]
public enum EquipmentSlot
{
    None = 0,
    Weapon,
    Head,
    Armor,
    Pants,
    Shoes,
    Accessory
}

public class ItemTemplate
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type")]
    public ItemType Type { get; set; }

    [JsonProperty("grade")]
    public ItemGrade Grade { get; set; }

    [JsonProperty("max_stack")]
    public int MaxStack { get; set; }

    [JsonProperty("sell_price")]
    public int SellPrice { get; set; }

    [JsonProperty("icon_path")]
    public string IconPath { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
    
    // Derived or Extra properties
    [JsonProperty("effect_id")]
    public int EffectId { get; set; }

    [JsonProperty("effect_val")]
    public int EffectValue { get; set; }
}

public class EquipmentTemplate : ItemTemplate
{
    [JsonProperty("equip_slot")]
    public EquipmentSlot SlotType { get; set; }

    [JsonProperty("base_atk")]
    public int Atk { get; set; }

    [JsonProperty("base_def")]
    public int Def { get; set; }

    [JsonProperty("base_hp")]
    public int Hp { get; set; }
    
    [JsonProperty("base_str")]
    public int Str { get; set; }

    [JsonProperty("base_dex")]
    public int Dex { get; set; }

    [JsonProperty("crit_rate")] // JSON doesn't have this? Check JSON.
    public float CritRate { get; set; }

    [JsonProperty("crit_dmg")]
    public float CritDmg { get; set; }

    [JsonProperty("normal_attack_skill_id")]
    public string NormalAttackSkillId { get; set; } = string.Empty;

    [JsonProperty("skill_id")]
    public string SkillId { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasActiveSkill => !string.IsNullOrEmpty(SkillId);
}

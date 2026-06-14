using System.Collections.Generic;
using UnityEngine;

namespace Client.Content.Item
{
    public enum ItemType
    {
        None = 0,
        Equipment,
        Consumable,
        Material,
        Blueprint,
        Currency
    }

    public enum ItemGrade
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum EquipmentSlot
    {
        None = 0,
        Weapon,
        Shoes,
        Hat,
        Accessory
    }

    [System.Serializable]
    public class ItemTemplate
    {
        public int id;
        public string name;
        public string type; // Enum as string in Json
        public string grade; // Enum as string
        public int max_stack;
        public int sell_price;
        public string icon_path;
        public string description;
        public int effect_id;
        public int effect_val;

        public ItemType TypeEnum => System.Enum.TryParse(type, true, out ItemType res) ? res : ItemType.None;
        public ItemGrade GradeEnum => System.Enum.TryParse(grade, true, out ItemGrade res) ? res : ItemGrade.Common;
    }

    [System.Serializable]
    public class EquipmentTemplate : ItemTemplate
    {
        public string equip_slot; // Enum as string
        public int base_atk;
        public int base_def;
        public int base_hp; // "hp" in json? Check json
        public float crit_rate;
        public float crit_dmg;
        public string model_path;

        public string normal_attack_skill_id;
        public string skill_id;

        public EquipmentSlot SlotEnum => System.Enum.TryParse(equip_slot, true, out EquipmentSlot res) ? res : EquipmentSlot.None;
    }
}

using System.Collections.Generic;
using UnityEngine;
using System;

namespace Client.Content.Item
{
    public class ItemDataManager : MonoBehaviour
    {
        public static ItemDataManager Instance { get; private set; }

        private Dictionary<int, ItemTemplate> _items = new Dictionary<int, ItemTemplate>();
        private Dictionary<int, EquipmentTemplate> _equipments = new Dictionary<int, EquipmentTemplate>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Load();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Load()
        {
            _items.Clear();
            _equipments.Clear();

            LoadItems("Data/Json/Items");
            LoadEquipments("Data/Json/Equipments");
            
            Debug.Log($"[ItemDataManager] Loaded {_items.Count} items, {_equipments.Count} equipments.");
        }

        private void LoadItems(string path)
        {
            TextAsset ta = Resources.Load<TextAsset>(path);
            if (ta == null)
            {
                Debug.LogError($"[ItemDataManager] Failed to load {path}");
                return;
            }

            // JsonUtility wrapper hack for array
            string json = "{ \"list\": " + ta.text + "}";
            ItemRoot wrapper = JsonUtility.FromJson<ItemRoot>(json);
            if (wrapper != null && wrapper.list != null)
            {
                foreach (var item in wrapper.list)
                {
                    if (!_items.ContainsKey(item.id))
                        _items.Add(item.id, item);
                }
            }
        }

        private void LoadEquipments(string path)
        {
            TextAsset ta = Resources.Load<TextAsset>(path);
            if (ta == null)
            {
                Debug.LogError($"[ItemDataManager] Failed to load {path}");
                return;
            }

            string json = "{ \"list\": " + ta.text + "}";
            EquipmentRoot wrapper = JsonUtility.FromJson<EquipmentRoot>(json);
            if (wrapper != null && wrapper.list != null)
            {
                foreach (var equip in wrapper.list)
                {
                    // Fix: Equipment JSON doesn't have "type" field
                    if (string.IsNullOrEmpty(equip.type)) equip.type = "Equipment";

                    if (!_equipments.ContainsKey(equip.id))
                        _equipments.Add(equip.id, equip);
                    
                    // Also add to _items for generic lookup
                    if (!_items.ContainsKey(equip.id))
                        _items.Add(equip.id, equip);
                }
            }
        }

        public ItemTemplate Get(int id)
        {
            if (_items.TryGetValue(id, out var item)) return item;
            return null;
        }

        public EquipmentTemplate GetEquipment(int id)
        {
             if (_equipments.TryGetValue(id, out var equip)) return equip;
             return null;
        }

        public EquipmentTemplate FindEquipmentBySkillId(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return null;

            foreach (var equip in _equipments.Values)
            {
                if (equip == null)
                    continue;

                if (string.Equals(equip.skill_id, skillId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(equip.normal_attack_skill_id, skillId, StringComparison.OrdinalIgnoreCase))
                {
                    return equip;
                }
            }

            return null;
        }

        [System.Serializable]
        private class ItemRoot
        {
            public List<ItemTemplate> list;
        }

        [System.Serializable]
        private class EquipmentRoot
        {
            public List<EquipmentTemplate> list;
        }
    }
}

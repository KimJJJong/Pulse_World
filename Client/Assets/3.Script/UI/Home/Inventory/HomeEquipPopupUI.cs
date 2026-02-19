using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;

public class HomeEquipPopupUI : MonoBehaviour
{
    [SerializeField] private Transform _content;
    [SerializeField] private GameObject _itemPrefab; // Should have HomeEquipPopupItemUI component
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private Button _closeBtn;

    private EquipmentSlot _currentSlot;

    private void Awake()
    {
        _closeBtn.onClick.AddListener(() => gameObject.SetActive(false));
        gameObject.SetActive(false);
    }

    public void Show(EquipmentSlot slot)
    {
        _currentSlot = slot;
        _title.text = $"Select {slot}";
        gameObject.SetActive(true);
        RefreshList();
    }

    private void RefreshList()
    {
        // Clear old
        foreach(Transform t in _content) Destroy(t.gameObject);

        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // Filter items matching slot
        var candidates = inv.Equipments.FindAll(x => 
        {
            var tmpl = ItemDataManager.Instance.GetEquipment(x.TemplateId);
            if (tmpl == null) 
            {
                Debug.LogWarning($"[Popup] Template not found for ID {x.TemplateId}");
                return false;
            }
            // Debug.Log($"[Popup] Item {x.TemplateId} Slot={tmpl.SlotEnum} Target={_currentSlot}");
            return tmpl.SlotEnum == _currentSlot;
        });

        Debug.Log($"[HomeEquipPopupUI] Slot={_currentSlot}, Candidates={candidates.Count}");

        foreach (var item in candidates)
        {
            var go = Instantiate(_itemPrefab, _content);
            go.SetActive(true);
            var itemUI = go.GetComponent<HomeEquipPopupItemUI>(); // create this next
            itemUI.Setup(item, () => OnEquipRequest(item));
        }
    }

    private void OnEquipRequest(SC_Inventory.Equipments item)
    {
        // Use Client-Side Logic + API Save for Home Scene
        bool toEquip = !item.IsEquipped; 
        InventoryManager.Instance.EquipItemApi(item.InstanceId, true); // Always "Select to Equip" for now
        
        gameObject.SetActive(false);
    }
}

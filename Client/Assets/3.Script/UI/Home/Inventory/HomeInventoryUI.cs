using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Client.Content.Item;

public class HomeInventoryUI : MonoBehaviour
{
    [SerializeField] private HomeEquipSlotUI[] _slots;
    [SerializeField] private HomeEquipPopupUI _popup;

    private void Start()
    {
        // Auto-setup for WorldSpace Interaction
        Canvas cvs = GetComponent<Canvas>();
        if (cvs != null && cvs.renderMode == RenderMode.WorldSpace && cvs.worldCamera == null)
        {
            cvs.worldCamera = Camera.main;
        }

        // Initial Refresh
        InventoryManager.Instance?.LoadFromApi();
        Refresh();
        
        // Listen to updates
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryUpdated += Refresh;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryUpdated -= Refresh;
    }

    public void Refresh()
    {
        if (InventoryManager.Instance == null) return;
        
        var equips = InventoryManager.Instance.Equipments;
        Debug.Log($"[HomeInventoryUI] Refreshing. Total Equips: {equips.Count}");

        if (ItemDataManager.Instance == null)
        {
            Debug.LogError("[HomeInventoryUI] ItemDataManager is NULL!");
        }

        foreach (var slot in _slots)
        {
            if(slot != null) slot.Refresh(equips);
        }
    }

    public void OnSlotClicked(EquipmentSlot slotType)
    {
        // Open Popup
        _popup.Show(slotType);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Client.Content.Item;
using RhythmRPG.Visual;

public class HomeInventoryUI : MonoBehaviour
{
    [SerializeField] private HomeEquipSlotUI[] _slots;
    [SerializeField] private HomeEquipPopupUI _popup;
    [SerializeField] private bool _enableAppearanceSelector = true;

    private void Awake()
    {
        ResolveReferences();
        DisableDecorativeEquipmentButtons();
    }

    private void Start()
    {
        ResolveReferences();
        DisableDecorativeEquipmentButtons();

        // Auto-setup for WorldSpace Interaction
        Canvas cvs = GetComponent<Canvas>();
        if (cvs != null && cvs.renderMode == RenderMode.WorldSpace && cvs.worldCamera == null)
        {
            cvs.worldCamera = Camera.main;
        }

        // Initial Refresh
        InventoryManager.Instance?.LoadFromApi();
        Refresh();

        if (_enableAppearanceSelector)
        {
            var appearanceSelector = GetComponent<HomeAppearanceSelectorUI>();
            if (appearanceSelector == null)
            {
                appearanceSelector = gameObject.AddComponent<HomeAppearanceSelectorUI>();
            }
            else
            {
                appearanceSelector.enabled = true;
            }
        }

        var previewRoot = GameObject.Find("Barbarian");
        if (previewRoot != null && previewRoot.GetComponent<HomeAppearancePreviewController>() == null)
            previewRoot.AddComponent<HomeAppearancePreviewController>();
        
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
        ResolveReferences();
        if (_popup == null)
        {
            Debug.LogError("[HomeInventoryUI] Equip popup reference is missing.");
            return;
        }

        _popup.Show(slotType, gameObject);
    }

    private void ResolveReferences()
    {
        if (_popup == null)
            _popup = GetComponentInChildren<HomeEquipPopupUI>(true);

        if (_slots == null || _slots.Length == 0)
            _slots = GetComponentsInChildren<HomeEquipSlotUI>(true);
    }

    private void DisableDecorativeEquipmentButtons()
    {
        DisableDecorativeButton("Button_AutoEquip");
        DisableDecorativeButton("Button_ManageLoadout");
    }

    private void DisableDecorativeButton(string buttonName)
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button == null || button.gameObject.name != buttonName)
                continue;

            button.onClick.RemoveAllListeners();
            button.interactable = false;
            button.enabled = false;

            if (button.targetGraphic != null)
                button.targetGraphic.raycastTarget = false;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;
        }
    }
}

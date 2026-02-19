using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Client.Content.Item;

public class TownInventoryDetailsUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descText;
    [SerializeField] private TextMeshProUGUI _statText;
    [SerializeField] private Button _useBtn;
    [SerializeField] private Button _trashBtn;

    [SerializeField] private ItemQuantityPopupUI _popup;

    private object _currentItem;

    private void Start()
    {
        if (_trashBtn != null)
        {
            _trashBtn.onClick.AddListener(OnTrashClicked);
        }
    }

    private void OnTrashClicked()
    {
        if (_currentItem == null) return;

        long iid = 0;
        int maxAmt = 1;
        string name = "";
        bool isEquipped = false;

        if (_currentItem is SC_Inventory.Items i)
        {
            iid = i.InstanceId;
            maxAmt = i.Amount; 
            var tmpl = ItemDataManager.Instance.Get(i.TemplateId);
            name = tmpl != null ? tmpl.name : "Item";
            // Items are not "equipped" in the same sense, but check logic if needed
        }
        else if (_currentItem is SC_Inventory.Equipments e)
        {
            iid = e.InstanceId;
            maxAmt = 1;
            var tmpl = ItemDataManager.Instance.Get(e.TemplateId);
            name = tmpl != null ? tmpl.name : "Equipment";
            isEquipped = e.IsEquipped;
        }

        if (iid == 0) return;

        // 1. Prevent Equipped Delete
        if (isEquipped)
        {
            Debug.LogWarning("[TownInventoryDetails] Cannot trash equipped item.");
            // Ideally show a toast/message
            return;
        }

        // 2. Quantity Logic
        if (maxAmt > 1)
        {
            if (_popup != null)
            {
                _popup.Open(name, maxAmt, (amt) => 
                {
                    Debug.Log($"[TownInventoryDetailsUI] Confirm Callback. ID: {iid}, Amount: {amt}");
                    InventoryNetworkController.Instance.RequestDestroyItem(iid, amt);
                    Close();
                });
            }
            else
            {
                // Fallback: Delete All if no popup
                Debug.LogWarning("Popup not assigned, deleting all.");
                InventoryNetworkController.Instance.RequestDestroyItem(iid, maxAmt);
                Close();
            }
        }
        else
        {
            // Single item (or equip)
            InventoryNetworkController.Instance.RequestDestroyItem(iid, 1);
            Close();
        }
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }
    
    public void Show(object item)
    {
        _currentItem = item;
        gameObject.SetActive(true);

        int tid = 0;
        if (item is SC_Inventory.Items i) tid = i.TemplateId;
        else if (item is SC_Inventory.Equipments e) tid = e.TemplateId;

        var tmpl = ItemDataManager.Instance.Get(tid);
        
        if (tmpl != null)
        {
            if (_nameText != null) _nameText.text = tmpl.name;
            if (_descText != null) _descText.text = tmpl.description;
            
            // Icon
            if (_icon != null)
            {
                // Try Loading from GameResourceManager first
                var sprite = RhythmRPG.Managers.GameResourceManager.Instance.GetIcon(tid);
                if (sprite == null && !string.IsNullOrEmpty(tmpl.icon_path))
                {
                    sprite = Resources.Load<Sprite>(tmpl.icon_path);
                }
                
                if (sprite != null) _icon.sprite = sprite;
            }
        }
        
        if (_statText != null && item is SC_Inventory.Equipments eq)
        {
             _statText.text = $"Level: +{eq.EnhancementLevel}\nStats: {eq.BaseStats}";
        }
        else if (_statText != null)
        {
             _statText.text = "";
        }

        // Feature: Show Preview Character if available
        // Need reference to a "PreviewCharacter" in the UI Scene
        // Not implemented fully yet, but hook is here.
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Client.Content.Item;

public class HomeEquipSlotUI : MonoBehaviour
{
    [SerializeField] private EquipmentSlot _targetSlot;
    [SerializeField] private Image _icon;
    [SerializeField] private Button _btn;
    [SerializeField] private GameObject _emptyVisual;
    [SerializeField] private GameObject _filledVisual;

    private HomeInventoryUI _parent;

    private void Awake()
    {
        _parent = GetComponentInParent<HomeInventoryUI>(true);
        if (_btn == null)
            _btn = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);

        if (_btn != null)
        {
            _btn.onClick.RemoveListener(HandleClick);
            _btn.onClick.AddListener(HandleClick);
        }
        else
        {
            Debug.LogWarning($"[HomeEquipSlotUI] Button reference is missing on {name}.");
        }
    }

    private void HandleClick()
    {
        if (_parent == null)
            _parent = GetComponentInParent<HomeInventoryUI>(true);

        if (_parent == null)
        {
            Debug.LogError($"[HomeEquipSlotUI] HomeInventoryUI parent is missing on {name}.");
            return;
        }

        _parent.OnSlotClicked(_targetSlot);
    }

    public void Refresh(List<SC_Inventory.Equipments> currentEquips)
    {
        // Find equipped item in this slot
        var equipped = currentEquips.Find(x => x.IsEquipped && IsMatchSlot(x.TemplateId, _targetSlot));
        
        if (equipped != null)
        {
            _emptyVisual.SetActive(false);
            _filledVisual.SetActive(true);
            
            // Set Icon
            var tmpl = ItemDataManager.Instance.GetEquipment(equipped.TemplateId);
            if (tmpl != null)
            {
               // _icon.sprite = Resources.Load<Sprite>(tmpl.icon_path); // TODO: Resource Manager
               if (!string.IsNullOrEmpty(tmpl.icon_path))
               {
                   var sprite = Resources.Load<Sprite>(tmpl.icon_path);
                   if (sprite != null) _icon.sprite = sprite;
                   else Debug.LogWarning($"[HomeEquipSlot] Sprite not found: {tmpl.icon_path}");
               }
            }
        }
        else
        {
             _emptyVisual.SetActive(true);
             _filledVisual.SetActive(false);
        }
    }

    private bool IsMatchSlot(int tid, EquipmentSlot slot)
    {
        var tmpl = ItemDataManager.Instance.GetEquipment(tid);
        if (tmpl == null) return false;
        
        // Debug
        // Debug.Log($"Checking Slot: ItemEnum={tmpl.SlotEnum}, Target={slot}");
        return tmpl.SlotEnum == slot;
    }
}

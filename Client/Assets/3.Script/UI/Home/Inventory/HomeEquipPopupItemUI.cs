using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;

public class HomeEquipPopupItemUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText; // +1, +2
    [SerializeField] private Button _btn;
    [SerializeField] private GameObject _equippedMark;

    private SC_Inventory.Equipments _data;
    private System.Action _onClick;

    public void Setup(SC_Inventory.Equipments data, System.Action onClick)
    {
        _data = data;
        _onClick = onClick;

        var tmpl = ItemDataManager.Instance.GetEquipment(data.TemplateId);
        if (tmpl != null)
        {
            _nameText.text = tmpl.name;
            // Load Icon
            if (!string.IsNullOrEmpty(tmpl.icon_path))
            {
               var sprite = Resources.Load<Sprite>(tmpl.icon_path);
               if (sprite != null) _icon.sprite = sprite;
            }
        }
        else
        {
            _nameText.text = $"Unknown ({data.TemplateId})";
        }

        _levelText.text = data.EnhancementLevel > 0 ? $"+{data.EnhancementLevel}" : "";
        _equippedMark.SetActive(data.IsEquipped);
        
        _btn.onClick.RemoveAllListeners();
        _btn.onClick.AddListener(() => _onClick?.Invoke());
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;

public class TownInventorySlotUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _amountText;
    [SerializeField] private Button _btn;
    [SerializeField] private GameObject _equipMark;

    private object _data;
    private System.Action<object> _onClick;

    public void Setup(object data, System.Action<object> onClick)
    {
        _data = data;
        _onClick = onClick;
        _btn.onClick.RemoveAllListeners();
        _btn.onClick.AddListener(() => _onClick?.Invoke(data));

        int tid = 0;
        int amt = 1;
        bool isEquipped = false;

        if (data is SC_Inventory.Items i)
        {
            tid = i.TemplateId;
            amt = i.Amount;
        }
        else if (data is SC_Inventory.Equipments e)
        {
            tid = e.TemplateId;
            amt = 1; // Equip is unique
            isEquipped = e.IsEquipped;
        }

        if (Client.Content.Item.ItemDataManager.Instance != null)
        {
            var tmpl = Client.Content.Item.ItemDataManager.Instance.Get(tid);
            if (tmpl != null)
            {
                if (!string.IsNullOrEmpty(tmpl.icon_path) && _icon != null)
                {
                    var sprite = Resources.Load<Sprite>(tmpl.icon_path);
                    if (sprite != null) _icon.sprite = sprite;
                }
            }
        }

        if (_amountText != null)
        {
            bool showCount = amt > 1;
            _amountText.text = amt.ToString();
            _amountText.gameObject.SetActive(showCount);
        }
        if (_equipMark) _equipMark.SetActive(isEquipped);
    }
}

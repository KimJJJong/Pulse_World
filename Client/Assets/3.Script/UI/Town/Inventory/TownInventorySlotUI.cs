using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;

public class TownInventorySlotUI : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite> SpritePathCache = new Dictionary<string, Sprite>();

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

        if (_icon != null)
            _icon.sprite = null;

        if (Client.Content.Item.ItemDataManager.Instance != null)
        {
            var tmpl = Client.Content.Item.ItemDataManager.Instance.Get(tid);
            if (tmpl != null)
            {
                if (_icon != null)
                {
                    var sprite = RhythmRPG.Managers.GameResourceManager.Instance.GetIcon(tid);
                    if (sprite == null && !string.IsNullOrEmpty(tmpl.icon_path))
                        sprite = GetSpriteByPath(tmpl.icon_path);

                    _icon.sprite = sprite;
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

    private static Sprite GetSpriteByPath(string path)
    {
        if (SpritePathCache.TryGetValue(path, out var cached))
            return cached;

        var sprite = Resources.Load<Sprite>(path);
        SpritePathCache[path] = sprite;
        return sprite;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;

public class TownInventorySlotUI : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite> SpritePathCache = new Dictionary<string, Sprite>();
    private static readonly Color EquipMarkColor = new Color(1f, 0.86f, 0.12f, 0.95f);

    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _amountText;
    [SerializeField] private Button _btn;
    [SerializeField] private GameObject _equipMark;

    private object _data;
    private System.Action<object> _onClick;

    private void Awake()
    {
        ResolveReferences();
        ConfigureEquipMark();
        if (_equipMark != null)
            _equipMark.SetActive(false);
    }

    public void Setup(object data, System.Action<object> onClick)
    {
        ResolveReferences();
        ConfigureEquipMark();

        _data = data;
        _onClick = onClick;
        if (_btn != null)
        {
            _btn.onClick.RemoveAllListeners();
            _btn.onClick.AddListener(() => _onClick?.Invoke(data));
        }

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
        if (_equipMark != null)
            _equipMark.SetActive(isEquipped);
    }

    private void ResolveReferences()
    {
        if (_icon == null)
            _icon = FindChildComponent<Image>("Icon", "ItemIcon");

        if (_amountText == null)
            _amountText = FindChildComponent<TextMeshProUGUI>("Amount", "AmountText", "CountText");

        if (_btn == null)
            _btn = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);

        if (_equipMark == null)
            _equipMark = FindChild("EquipMark", "EquippedMark", "Equipped");
    }

    private void ConfigureEquipMark()
    {
        if (_equipMark == null)
            return;

        var rect = _equipMark.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(-6f, -6f);
            rect.sizeDelta = new Vector2(22f, 18f);
        }

        var image = _equipMark.GetComponent<Image>();
        if (image != null)
        {
            image.color = EquipMarkColor;
            image.raycastTarget = false;
            image.preserveAspect = false;
        }
    }

    private T FindChildComponent<T>(params string[] names) where T : Component
    {
        foreach (var name in names)
        {
            var child = FindChild(name);
            if (child == null)
                continue;

            var component = child.GetComponent<T>();
            if (component != null)
                return component;
        }

        return GetComponentInChildren<T>(true);
    }

    private GameObject FindChild(params string[] names)
    {
        var children = GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (child == null || child == transform)
                continue;

            foreach (var name in names)
            {
                if (child.name == name)
                    return child.gameObject;
            }
        }

        return null;
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

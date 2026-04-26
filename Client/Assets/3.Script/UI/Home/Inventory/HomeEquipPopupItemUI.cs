using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;
using System.IO;

public class HomeEquipPopupItemUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText; // +1, +2
    [SerializeField] private Button _btn;
    [SerializeField] private GameObject _equippedMark;

    private SC_Inventory.Equipments _data;
    private System.Action _onClick;
    private bool _isSelected;
    private static TMP_FontAsset _koreanFont;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Setup(SC_Inventory.Equipments data, System.Action onClick, bool isSelected)
    {
        ResolveReferences();
        _data = data;
        _onClick = onClick;
        _isSelected = isSelected;

        var tmpl = ItemDataManager.Instance != null ? ItemDataManager.Instance.GetEquipment(data.TemplateId) : null;
        if (tmpl != null)
        {
            if (_nameText != null)
            {
                _nameText.text = tmpl.name;
                ApplyKoreanFont(_nameText);
                _nameText.fontSize = 16f;
                _nameText.enableAutoSizing = true;
                _nameText.fontSizeMin = 11f;
                _nameText.fontSizeMax = 16f;
                _nameText.overflowMode = TextOverflowModes.Ellipsis;
                _nameText.alignment = TextAlignmentOptions.MidlineLeft;
            }

            if (_icon != null)
            {
                var sprite = RhythmRPG.Managers.GameResourceManager.Instance != null
                    ? RhythmRPG.Managers.GameResourceManager.Instance.GetIcon(data.TemplateId)
                    : null;

                if (sprite == null)
                    sprite = LoadIcon(tmpl.icon_path);

                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
                _icon.preserveAspect = true;
                _icon.raycastTarget = false;
                var iconRect = _icon.rectTransform;
                if (iconRect != null)
                {
                    iconRect.anchorMin = new Vector2(0f, 0.5f);
                    iconRect.anchorMax = new Vector2(0f, 0.5f);
                    iconRect.pivot = new Vector2(0f, 0.5f);
                    iconRect.sizeDelta = new Vector2(40f, 40f);
                    iconRect.anchoredPosition = new Vector2(8f, 0f);
                }
            }
        }
        else
        {
            if (_nameText != null)
                _nameText.text = $"Unknown ({data.TemplateId})";

            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.enabled = false;
            }
        }

        if (_levelText != null)
        {
            _levelText.text = data.EnhancementLevel > 0 ? $"+{data.EnhancementLevel}" : "";
            ApplyKoreanFont(_levelText);
            _levelText.fontSize = 11f;
            _levelText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        if (_equippedMark != null)
        {
            _equippedMark.SetActive(data.IsEquipped);
            var markText = _equippedMark.GetComponent<TextMeshProUGUI>();
            if (markText != null)
            {
                ApplyKoreanFont(markText);
                markText.fontSize = 11f;
                markText.alignment = TextAlignmentOptions.Center;
            }
        }

        if (_btn != null)
        {
            _btn.onClick.RemoveAllListeners();
            _btn.onClick.AddListener(() => _onClick?.Invoke());
        }
        ApplySelectionVisual();
    }

    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        ApplySelectionVisual();
    }

    public long InstanceId => _data.InstanceId;

    private void ApplySelectionVisual()
    {
        var bg = GetComponent<Image>();
        if (bg != null)
        {
            bg.color = _isSelected
                ? new Color(0.28f, 0.44f, 0.62f, 0.98f)
                : new Color(0.18f, 0.18f, 0.24f, 0.96f);
        }

        if (_nameText != null)
            _nameText.color = _isSelected ? new Color(1f, 0.95f, 0.8f, 1f) : Color.white;

        if (_levelText != null)
            _levelText.color = _isSelected ? new Color(0.95f, 0.95f, 1f, 1f) : new Color(0.85f, 0.85f, 0.92f, 1f);
    }

    private void ResolveReferences()
    {
        if (_icon == null)
            _icon = FindImage("Icon", "ItemIcon", "EquipmentIcon");

        if (_nameText == null)
            _nameText = FindTMP("NameText", "ItemName", "Title");

        if (_levelText == null)
            _levelText = FindTMP("LevelText", "ItemLevel", "EnhanceText");

        if (_btn == null)
            _btn = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);

        if (_equippedMark == null)
            _equippedMark = FindGameObject("EquippedMark", "Equipped", "Mark");

        var layout = GetComponent<LayoutElement>();
        if (layout == null)
            layout = gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 54f;
        layout.minHeight = 50f;
        layout.preferredWidth = 232f;

        if (_nameText != null)
        {
            var nameRect = _nameText.rectTransform;
            if (nameRect != null)
            {
                nameRect.anchorMin = new Vector2(0f, 0.5f);
                nameRect.anchorMax = new Vector2(0f, 0.5f);
                nameRect.pivot = new Vector2(0f, 0.5f);
                nameRect.anchoredPosition = new Vector2(52f, 7f);
                nameRect.sizeDelta = new Vector2(148f, 24f);
            }
        }

        if (_levelText != null)
        {
            var levelRect = _levelText.rectTransform;
            if (levelRect != null)
            {
                levelRect.anchorMin = new Vector2(0f, 0.5f);
                levelRect.anchorMax = new Vector2(0f, 0.5f);
                levelRect.pivot = new Vector2(0f, 0.5f);
                levelRect.anchoredPosition = new Vector2(52f, -12f);
                levelRect.sizeDelta = new Vector2(88f, 18f);
            }
        }

        if (_equippedMark != null)
        {
            var markRect = _equippedMark.GetComponent<RectTransform>();
            if (markRect != null)
            {
                markRect.anchorMin = new Vector2(1f, 0.5f);
                markRect.anchorMax = new Vector2(1f, 0.5f);
                markRect.pivot = new Vector2(1f, 0.5f);
                markRect.anchoredPosition = new Vector2(-10f, 0f);
                markRect.sizeDelta = new Vector2(26f, 18f);
            }
            var markLabel = _equippedMark.GetComponent<TextMeshProUGUI>();
            if (markLabel != null)
            {
                ApplyKoreanFont(markLabel);
                markLabel.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return null;

        var candidates = new List<string>();
        string path = iconPath.Trim();
        candidates.Add(path);

        if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path)).Replace("\\", "/");
            candidates.Add(path);
        }

        if (!path.StartsWith("Icons/", System.StringComparison.OrdinalIgnoreCase))
            candidates.Add($"Icons/{Path.GetFileNameWithoutExtension(path)}");

        foreach (var candidate in candidates)
        {
            var sprite = Resources.Load<Sprite>(candidate);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private Image FindImage(params string[] names)
    {
        foreach (var name in names)
        {
            var tr = transform.Find(name);
            if (tr != null)
            {
                var img = tr.GetComponent<Image>();
                if (img != null) return img;
            }
        }

        var images = GetComponentsInChildren<Image>(true);
        Image smallest = null;
        float smallestArea = float.MaxValue;
        foreach (var img in images)
        {
            if (img == null || img.gameObject == gameObject)
                continue;

            var lower = img.gameObject.name.ToLowerInvariant();
            if (lower.Contains("icon") || lower.Contains("sprite") || lower.Contains("thumb"))
                return img;

            var rect = img.GetComponent<RectTransform>();
            if (rect == null)
                continue;

            var size = rect.rect.size;
            var area = Mathf.Abs(size.x * size.y);
            if (area > 0f && area < smallestArea)
            {
                smallest = img;
                smallestArea = area;
            }
        }

        return smallest;
    }

    private TextMeshProUGUI FindTMP(params string[] names)
    {
        foreach (var name in names)
        {
            var tr = transform.Find(name);
            if (tr != null)
            {
                var tmp = tr.GetComponent<TextMeshProUGUI>();
                if (tmp != null) return tmp;
            }
        }

        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in texts)
        {
            if (tmp != null && tmp.gameObject != gameObject)
                return tmp;
        }

        return null;
    }

    private GameObject FindGameObject(params string[] names)
    {
        foreach (var name in names)
        {
            var tr = transform.Find(name);
            if (tr != null)
                return tr.gameObject;
        }

        return null;
    }

    private static void ApplyKoreanFont(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (_koreanFont == null)
        {
            _koreanFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/NanumGothic SDF");
            if (_koreanFont == null)
                _koreanFont = Resources.Load<TMP_FontAsset>("NanumGothic SDF");
        }

        if (_koreanFont != null)
        {
            text.font = _koreanFont;
            text.fontSharedMaterial = _koreanFont.material;
        }
    }
}

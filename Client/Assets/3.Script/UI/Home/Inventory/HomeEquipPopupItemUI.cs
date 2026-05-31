using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;
using System.IO;

public class HomeEquipPopupItemUI : MonoBehaviour
{
    private static readonly Color ListCard = new Color(0.78f, 0.64f, 0.44f, 0.92f);
    private static readonly Color ListCardSelected = new Color(0.07f, 0.42f, 0.40f, 0.96f);
    private static readonly Color ListText = new Color(0.10f, 0.22f, 0.20f, 1f);
    private static readonly Color ListMutedText = new Color(0.32f, 0.26f, 0.18f, 1f);
    private static readonly Color EquippedMarkText = new Color(1f, 0.88f, 0.36f, 1f);
    private static Sprite _defaultCardSprite;
    private static Sprite _selectedCardSprite;

    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText; // +1, +2
    [SerializeField] private Button _btn;
    [SerializeField] private GameObject _equippedMark;

    private SC_Inventory.Equipments _data;
    private System.Action _onClick;
    private bool _isSelected;
    private bool _forceResourceCardLayout;
    private static TMP_FontAsset _koreanFont;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Setup(SC_Inventory.Equipments data, System.Action onClick, bool isSelected)
    {
        Setup(data, onClick, isSelected, false);
    }

    public void Setup(SC_Inventory.Equipments data, System.Action onClick, bool isSelected, bool useResourceCardLayout)
    {
        _forceResourceCardLayout = useResourceCardLayout;
        ResolveReferences();
        _data = data;
        _onClick = onClick;
        _isSelected = isSelected;

        var tmpl = ItemDataManager.Instance != null ? ItemDataManager.Instance.GetEquipment(data.TemplateId) : null;
        if (tmpl != null)
        {
            var gridCard = UseResourceCardLayout();
            if (_nameText != null)
            {
                _nameText.text = tmpl.name;
                _nameText.gameObject.SetActive(!gridCard);
                ApplyKoreanFont(_nameText);
                _nameText.fontSize = gridCard ? 11f : 16f;
                _nameText.enableAutoSizing = true;
                _nameText.fontSizeMin = gridCard ? 8f : 11f;
                _nameText.fontSizeMax = gridCard ? 11f : 16f;
                _nameText.overflowMode = TextOverflowModes.Ellipsis;
                _nameText.alignment = gridCard ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
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
                    iconRect.anchorMin = gridCard ? new Vector2(0.5f, 1f) : new Vector2(0f, 0.5f);
                    iconRect.anchorMax = gridCard ? new Vector2(0.5f, 1f) : new Vector2(0f, 0.5f);
                    iconRect.pivot = gridCard ? new Vector2(0.5f, 1f) : new Vector2(0f, 0.5f);
                    iconRect.sizeDelta = gridCard ? new Vector2(58f, 58f) : new Vector2(48f, 48f);
                    iconRect.anchoredPosition = gridCard ? new Vector2(0f, -13f) : new Vector2(10f, 0f);
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
            _levelText.gameObject.SetActive(!UseResourceCardLayout() && data.EnhancementLevel > 0);
            ApplyKoreanFont(_levelText);
            _levelText.fontSize = UseResourceCardLayout() ? 10f : 11f;
            _levelText.alignment = UseResourceCardLayout() ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        }

        if (_equippedMark != null)
        {
            ConfigureEquippedMark();
            _equippedMark.SetActive(data.IsEquipped);
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
            if (UseResourceCardLayout())
            {
                bg.sprite = _isSelected ? SelectedCardSprite : DefaultCardSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = _isSelected
                    ? ListCardSelected
                    : ListCard;
            }
        }

        if (_nameText != null)
            _nameText.color = _isSelected ? Color.white : ListText;

        if (_levelText != null)
            _levelText.color = _isSelected ? new Color(0.94f, 0.97f, 0.94f, 1f) : ListMutedText;

        var feedback = GetComponent<HomeUIButtonFeedback>();
        if (feedback == null)
            feedback = gameObject.AddComponent<HomeUIButtonFeedback>();
        feedback.Configure(transform as RectTransform, bg);
    }

    private void ResolveReferences()
    {
        var gridCard = UseResourceCardLayout();

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
        layout.preferredHeight = gridCard ? 85f : 68f;
        layout.minHeight = gridCard ? 85f : 64f;
        layout.preferredWidth = gridCard ? 84f : 300f;

        if (_nameText != null)
        {
            var nameRect = _nameText.rectTransform;
            if (nameRect != null)
            {
                nameRect.anchorMin = gridCard ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
                nameRect.anchorMax = gridCard ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
                nameRect.pivot = gridCard ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
                nameRect.anchoredPosition = gridCard ? new Vector2(0f, 32f) : new Vector2(68f, 10f);
                nameRect.sizeDelta = gridCard ? new Vector2(88f, 20f) : new Vector2(188f, 24f);
            }
        }

        if (_levelText != null)
        {
            var levelRect = _levelText.rectTransform;
            if (levelRect != null)
            {
                levelRect.anchorMin = gridCard ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
                levelRect.anchorMax = gridCard ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
                levelRect.pivot = gridCard ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
                levelRect.anchoredPosition = gridCard ? new Vector2(0f, 12f) : new Vector2(68f, -14f);
                levelRect.sizeDelta = gridCard ? new Vector2(82f, 18f) : new Vector2(150f, 18f);
            }
        }

        ConfigureEquippedMark();

        if (gridCard && _icon != null)
        {
            var iconRect = _icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(58f, 58f);
            iconRect.anchoredPosition = new Vector2(0f, -13f);
        }
    }

    private bool UseResourceCardLayout()
    {
        return _forceResourceCardLayout || IsGridCardLayout();
    }

    private bool IsGridCardLayout()
    {
        return GetComponentInParent<GridLayoutGroup>(true) != null;
    }

    private void ConfigureEquippedMark()
    {
        if (_equippedMark == null)
            return;

        var gridCard = UseResourceCardLayout();
        var markRect = _equippedMark.GetComponent<RectTransform>();
        if (markRect != null)
        {
            markRect.anchorMin = new Vector2(1f, 1f);
            markRect.anchorMax = new Vector2(1f, 1f);
            markRect.pivot = new Vector2(1f, 1f);
            markRect.localScale = Vector3.one;
            markRect.anchoredPosition = gridCard ? new Vector2(-6f, -5f) : new Vector2(-10f, -25f);
            markRect.sizeDelta = gridCard ? new Vector2(20f, 15f) : new Vector2(26f, 18f);
        }

        var markImage = _equippedMark.GetComponent<Image>();
        if (markImage != null)
        {
            markImage.raycastTarget = false;
            markImage.preserveAspect = false;
            if (markImage.sprite == null)
                markImage.enabled = false;
            else
                markImage.color = Color.white;
        }

        var markLabel = _equippedMark.GetComponent<TextMeshProUGUI>()
            ?? _equippedMark.GetComponentInChildren<TextMeshProUGUI>(true);

        if (markLabel == null)
            markLabel = _equippedMark.AddComponent<TextMeshProUGUI>();

        markLabel.text = "E";
        ApplyKoreanFont(markLabel);
        markLabel.fontSize = gridCard ? 10f : 11f;
        markLabel.enableAutoSizing = true;
        markLabel.fontSizeMin = 8f;
        markLabel.fontSizeMax = gridCard ? 10f : 11f;
        markLabel.alignment = TextAlignmentOptions.Center;
        markLabel.color = EquippedMarkText;
        markLabel.raycastTarget = false;

        if (markLabel.gameObject != _equippedMark)
        {
            var labelRect = markLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.localScale = Vector3.one;
        }
    }

    private static Sprite DefaultCardSprite
    {
        get
        {
            if (_defaultCardSprite == null)
                _defaultCardSprite = Resources.Load<Sprite>("UI/UI_Home_Equipment_Detail/UI_01_default");

            return _defaultCardSprite;
        }
    }

    private static Sprite SelectedCardSprite
    {
        get
        {
            if (_selectedCardSprite == null)
                _selectedCardSprite = Resources.Load<Sprite>("UI/UI_Home_Equipment_Detail/UI_01_selected");

            return _selectedCardSprite;
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

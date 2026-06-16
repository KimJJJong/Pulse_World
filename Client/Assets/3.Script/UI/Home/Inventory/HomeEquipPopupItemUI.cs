using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;
using System.IO;

public class HomeEquipPopupItemUI : MonoBehaviour
{
    private const string EquipmentDetailResourceRoot = "UI/UI_EquimentDetail/";
    private const float ResourceCardWidth = 96f;
    private const float ResourceCardHeight = 102f;
    private static readonly Color ListCard = new Color(0.78f, 0.64f, 0.44f, 0.92f);
    private static readonly Color ListCardSelected = new Color(0.07f, 0.42f, 0.40f, 0.96f);
    private static readonly Color ListText = new Color(0.10f, 0.22f, 0.20f, 1f);
    private static readonly Color ListMutedText = new Color(0.32f, 0.26f, 0.18f, 1f);
    private static readonly Color EquippedMarkText = new Color(1f, 0.96f, 0.78f, 1f);
    private static readonly Color EquippedMarkBack = new Color(0.10f, 0.36f, 0.34f, 0.96f);
    private static Sprite _defaultCardSprite;
    private static Sprite _selectedCardSprite;

    [SerializeField] private Image _icon;
    [SerializeField] private Image _iconFrame;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText; // +1, +2
    [SerializeField] private Button _btn;
    [SerializeField] private GameObject _equippedMark;
    [SerializeField] private Image _selectionOutline;

    private SC_Inventory.Equipments _data;
    private System.Action _onClick;
    private bool _isSelected;
    private bool _forceResourceCardLayout;
    [SerializeField] private bool _useManualObjectLayout;
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
        Setup(data, onClick, isSelected, useResourceCardLayout, false);
    }

    public void Setup(SC_Inventory.Equipments data, System.Action onClick, bool isSelected, bool useResourceCardLayout, bool useManualObjectLayout)
    {
        _forceResourceCardLayout = useResourceCardLayout;
        _useManualObjectLayout = useManualObjectLayout;
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
                _nameText.gameObject.SetActive(_useManualObjectLayout || !gridCard);
                ApplyKoreanFont(_nameText);
                if (!_useManualObjectLayout)
                {
                    _nameText.fontSize = gridCard ? 11f : 16f;
                    _nameText.enableAutoSizing = true;
                    _nameText.fontSizeMin = gridCard ? 8f : 11f;
                    _nameText.fontSizeMax = gridCard ? 11f : 16f;
                    _nameText.overflowMode = TextOverflowModes.Ellipsis;
                    _nameText.alignment = gridCard ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
                }
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
                if (!_useManualObjectLayout)
                {
                    var iconRect = _icon.rectTransform;
                    if (iconRect != null)
                    {
                        iconRect.anchorMin = gridCard ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
                        iconRect.anchorMax = gridCard ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
                        iconRect.pivot = gridCard ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
                        iconRect.sizeDelta = gridCard ? new Vector2(64f, 64f) : new Vector2(48f, 48f);
                        iconRect.anchoredPosition = gridCard ? new Vector2(0f, 0f) : new Vector2(10f, 0f);
                    }
                }
            }

            ConfigureIconFrame(gridCard, _icon != null && _icon.enabled);
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

            ConfigureIconFrame(UseResourceCardLayout(), false);
        }

        if (_levelText != null)
        {
            _levelText.text = data.EnhancementLevel > 0 ? $"+{data.EnhancementLevel}" : "";
            _levelText.gameObject.SetActive((_useManualObjectLayout || !UseResourceCardLayout()) && data.EnhancementLevel > 0);
            ApplyKoreanFont(_levelText);
            if (!_useManualObjectLayout)
            {
                _levelText.fontSize = UseResourceCardLayout() ? 10f : 11f;
                _levelText.alignment = UseResourceCardLayout() ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            }
        }

        if (_equippedMark != null)
        {
            if (_useManualObjectLayout)
                ConfigureManualEquippedMark();
            else
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
            if (_useManualObjectLayout)
            {
                bg.raycastTarget = true;
            }
            else if (UseResourceCardLayout())
            {
                bg.sprite = DefaultCardSprite;
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false;
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

        UpdateSelectionOutline();

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

        if (_useManualObjectLayout)
            BindExistingIconFrame();
        else
            EnsureIconFrame(gridCard);

        if (_nameText == null)
            _nameText = FindTMP("NameText", "ItemName", "Title");

        if (_levelText == null)
            _levelText = FindTMP("LevelText", "ItemLevel", "EnhanceText");

        if (_btn == null)
            _btn = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);

        if (_btn != null)
        {
            var graphic = GetComponent<Graphic>();
            if (_btn.targetGraphic == null && graphic != null)
                _btn.targetGraphic = graphic;

            if (_btn.targetGraphic != null)
                _btn.targetGraphic.raycastTarget = true;

            _btn.enabled = true;
            _btn.interactable = true;
        }

        if (_equippedMark == null)
            _equippedMark = FindGameObject("EquippedMark", "Equipped", "Mark");

        if (_useManualObjectLayout)
            BindExistingSelectionOutline();
        else
            EnsureSelectionOutline(gridCard);

        var layout = GetComponent<LayoutElement>();
        if (!_useManualObjectLayout)
        {
            if (layout == null)
                layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = gridCard ? ResourceCardHeight : 68f;
            layout.minHeight = gridCard ? ResourceCardHeight : 64f;
            layout.preferredWidth = gridCard ? ResourceCardWidth : 300f;
        }

        if (!_useManualObjectLayout && _nameText != null)
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

        if (!_useManualObjectLayout && _levelText != null)
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

        if (_useManualObjectLayout)
            ConfigureManualEquippedMark();
        else
            ConfigureEquippedMark();

        if (!_useManualObjectLayout && gridCard && _icon != null)
        {
            var iconRect = _icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            iconRect.anchoredPosition = new Vector2(0f, 0f);
        }

        if (!_useManualObjectLayout)
            ConfigureIconFrame(gridCard, _icon != null && _icon.enabled);
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
            markRect.anchoredPosition = gridCard ? new Vector2(-6f, -6f) : new Vector2(-10f, -25f);
            markRect.sizeDelta = gridCard ? new Vector2(24f, 18f) : new Vector2(26f, 18f);
        }

        var markImage = _equippedMark.GetComponent<Image>();
        if (markImage != null)
        {
            markImage.raycastTarget = false;
            markImage.preserveAspect = false;
            if (markImage.sprite == null)
                markImage.sprite = DefaultCardSprite;
            markImage.type = Image.Type.Simple;
            markImage.color = EquippedMarkBack;
            markImage.enabled = true;
        }

        var markLabel = _equippedMark.GetComponent<TextMeshProUGUI>()
            ?? _equippedMark.GetComponentInChildren<TextMeshProUGUI>(true);

        if (markLabel == null)
            markLabel = _equippedMark.AddComponent<TextMeshProUGUI>();

        markLabel.text = "E";
        ApplyKoreanFont(markLabel);
        markLabel.fontStyle = FontStyles.Bold;
        markLabel.fontSize = gridCard ? 11f : 11f;
        markLabel.enableAutoSizing = true;
        markLabel.fontSizeMin = 8f;
        markLabel.fontSizeMax = gridCard ? 11f : 11f;
        markLabel.alignment = TextAlignmentOptions.Center;
        markLabel.color = EquippedMarkText;
        markLabel.outlineColor = new Color(0.05f, 0.06f, 0.05f, 0.85f);
        markLabel.outlineWidth = 0.12f;
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

    private void ConfigureManualEquippedMark()
    {
        if (_equippedMark == null)
            return;

        var markImage = _equippedMark.GetComponent<Image>();
        if (markImage != null)
        {
            markImage.raycastTarget = false;
            markImage.preserveAspect = false;
            if (markImage.sprite == null)
                markImage.sprite = DefaultCardSprite;
            markImage.type = Image.Type.Simple;
            markImage.color = EquippedMarkBack;
            markImage.enabled = true;
        }

        var markLabel = _equippedMark.GetComponent<TextMeshProUGUI>()
            ?? _equippedMark.GetComponentInChildren<TextMeshProUGUI>(true);

        if (markLabel != null)
        {
            markLabel.text = "E";
            ApplyKoreanFont(markLabel);
            markLabel.color = EquippedMarkText;
            markLabel.raycastTarget = false;
        }
    }

    private void EnsureIconFrame(bool gridCard)
    {
        if (!gridCard)
            return;

        if (_iconFrame == null)
            _iconFrame = FindImageByExactName("IconFrame", "EquipmentIconFrame");

        var parent = _icon != null && _icon.transform.parent != null
            ? _icon.transform.parent
            : transform;

        if (_iconFrame == null)
        {
            var frameGo = new GameObject("IconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.transform.SetParent(parent, false);
            _iconFrame = frameGo.GetComponent<Image>();
        }
    }

    private void BindExistingIconFrame()
    {
        if (_iconFrame == null)
            _iconFrame = FindImageByExactName("IconFrame", "EquipmentIconFrame");

        if (_iconFrame != null)
            _iconFrame.raycastTarget = false;
    }

    private void EnsureSelectionOutline(bool gridCard)
    {
        if (!gridCard)
        {
            if (_selectionOutline != null)
                _selectionOutline.gameObject.SetActive(false);
            return;
        }

        if (_selectionOutline == null)
            _selectionOutline = FindImageByExactName("SelectionOutline", "SelectedOutline", "EquippedOutline");

        if (_selectionOutline == null)
        {
            var outlineGo = new GameObject("SelectionOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            outlineGo.transform.SetParent(transform, false);
            _selectionOutline = outlineGo.GetComponent<Image>();
        }

        _selectionOutline.sprite = SelectedCardSprite;
        _selectionOutline.type = Image.Type.Simple;
        _selectionOutline.preserveAspect = false;
        _selectionOutline.raycastTarget = false;
        _selectionOutline.color = Color.white;

        var outlineRect = _selectionOutline.rectTransform;
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = Vector2.zero;
        outlineRect.offsetMax = Vector2.zero;
        outlineRect.localScale = Vector3.one;

        UpdateSelectionOutline();
    }

    private void BindExistingSelectionOutline()
    {
        if (_selectionOutline == null)
            _selectionOutline = FindImageByExactName("SelectionOutline", "SelectedOutline", "EquippedOutline");

        if (_selectionOutline == null)
            return;

        _selectionOutline.sprite = SelectedCardSprite;
        _selectionOutline.type = Image.Type.Simple;
        _selectionOutline.preserveAspect = false;
        _selectionOutline.raycastTarget = false;
        _selectionOutline.color = Color.white;
        UpdateSelectionOutline();
    }

    private void UpdateSelectionOutline()
    {
        if (_selectionOutline == null)
            return;

        var showOutline = UseResourceCardLayout() && _isSelected;
        _selectionOutline.gameObject.SetActive(showOutline);
        OrderResourceCardLayers();
    }

    private void OrderResourceCardLayers()
    {
        if (!UseResourceCardLayout())
            return;

        if (_icon != null)
            _icon.transform.SetAsLastSibling();
        if (_selectionOutline != null && _selectionOutline.gameObject.activeSelf)
            _selectionOutline.transform.SetAsLastSibling();
        if (_equippedMark != null && _equippedMark.activeSelf)
            _equippedMark.transform.SetAsLastSibling();
    }

    private void ConfigureIconFrame(bool gridCard, bool visible)
    {
        if (!gridCard)
        {
            if (_iconFrame != null)
                _iconFrame.gameObject.SetActive(false);
            return;
        }

        if (_iconFrame != null)
            _iconFrame.gameObject.SetActive(false);

        OrderResourceCardLayers();
    }

    private static Sprite DefaultCardSprite
    {
        get
        {
            if (_defaultCardSprite == null)
                _defaultCardSprite = Resources.Load<Sprite>(EquipmentDetailResourceRoot + "Equipment_Frame");

            return _defaultCardSprite;
        }
    }

    private static Sprite SelectedCardSprite
    {
        get
        {
            if (_selectedCardSprite == null)
                _selectedCardSprite = Resources.Load<Sprite>(EquipmentDetailResourceRoot + "Equipment_Selected");

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

    private Image FindImageByExactName(params string[] names)
    {
        var images = GetComponentsInChildren<Image>(true);
        foreach (var targetName in names)
        {
            foreach (var img in images)
            {
                if (img != null && img.gameObject.name == targetName)
                    return img;
            }
        }

        return null;
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

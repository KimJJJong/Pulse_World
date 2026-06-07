using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Client.Content.Item;

public class HomeEquipSlotUI : MonoBehaviour
{
    private const string SlotIconFrameResourcePath = "UI/UI_Home_Equipment_Detail/UI_equipment_icon_frame_slot";
    private const string SlotDecorationFrameResourcePath = "UI/UI_Home_Equipment_Detail/UI_equipment_icon_frame_detail";
    private static readonly Color FrameBackgroundColor = new Color(0.70f, 0.53f, 0.32f, 0.76f);
    private static Sprite _slotIconFrameSprite;
    private static Sprite _slotDecorationFrameSprite;

    [SerializeField] private EquipmentSlot _targetSlot;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _iconBackground;
    [SerializeField] private Image _iconFrame;
    [SerializeField] private RawImage _slotDecorationIcon;
    [SerializeField] private Image _slotDecorationBackground;
    [SerializeField] private Image _slotDecorationFrame;
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

        EnsureIconFrame();
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
        EnsureIconFrame();
        currentEquips ??= new List<SC_Inventory.Equipments>();

        // Find equipped item in this slot
        var equipped = currentEquips.Find(x => x.IsEquipped && IsMatchSlot(x.TemplateId, _targetSlot));
        
        if (equipped != null)
        {
            SetActive(_emptyVisual, false);
            SetActive(_filledVisual, true);
            if (_filledVisual != null)
                _filledVisual.transform.SetAsLastSibling();
            if (_iconFrame != null)
                _iconFrame.enabled = true;
            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.enabled = false;
            }
            
            // Set Icon
            var tmpl = ItemDataManager.Instance.GetEquipment(equipped.TemplateId);
            if (tmpl != null)
            {
               // _icon.sprite = Resources.Load<Sprite>(tmpl.icon_path); // TODO: Resource Manager
               if (!string.IsNullOrEmpty(tmpl.icon_path))
               {
                   var sprite = Resources.Load<Sprite>(tmpl.icon_path);
                   if (sprite != null && _icon != null)
                   {
                       _icon.sprite = sprite;
                       _icon.enabled = true;
                   }
                   else if (sprite == null) Debug.LogWarning($"[HomeEquipSlot] Sprite not found: {tmpl.icon_path}");
               }
            }
        }
        else
        {
             SetActive(_emptyVisual, true);
             SetActive(_filledVisual, false);
             if (_icon != null)
                 _icon.enabled = false;
             if (_iconFrame != null)
                 _iconFrame.enabled = false;
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

    private void EnsureIconFrame()
    {
        if (_icon == null)
            _icon = FindImage("Icon");

        if (_filledVisual == null && _icon != null)
            _filledVisual = _icon.transform.parent != null ? _icon.transform.parent.gameObject : null;

        if (_iconFrame == null)
            _iconFrame = FindImage("IconFrame", "EquipmentIconFrame");

        if (_iconBackground == null)
            _iconBackground = FindImage("IconBackground", "EquipmentIconBackground");

        var frameParent = _icon != null && _icon.transform.parent != null
            ? _icon.transform.parent
            : (_filledVisual != null ? _filledVisual.transform : transform);

        if (_iconBackground == null && frameParent != null)
        {
            var backgroundGo = new GameObject("IconBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundGo.transform.SetParent(frameParent, false);
            _iconBackground = backgroundGo.GetComponent<Image>();
        }

        if (_iconFrame == null && frameParent != null)
        {
            var frameGo = new GameObject("IconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.transform.SetParent(frameParent, false);
            _iconFrame = frameGo.GetComponent<Image>();
        }

        ConfigureBackgroundImage(_iconBackground);
        ConfigureIconImage(_iconFrame, SlotIconFrameSprite, preserveAspect: false);
        ConfigureSlotIconLayout();
        EnsureSlotDecorationFrame();
    }

    private void ConfigureSlotIconLayout()
    {
        if (_iconBackground != null)
        {
            SetTopLeftRect(_iconBackground.rectTransform, new Vector2(31f, 35f), new Vector2(64f, 64f));
            _iconBackground.transform.SetAsLastSibling();
        }

        if (_iconFrame != null)
        {
            SetTopLeftRect(_iconFrame.rectTransform, new Vector2(24f, 28f), new Vector2(78f, 78f));
            _iconFrame.transform.SetAsLastSibling();
        }

        if (_icon != null)
        {
            SetTopLeftRect(_icon.rectTransform, new Vector2(34f, 38f), new Vector2(58f, 58f));
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            _icon.transform.SetAsLastSibling();
        }

        if (_iconFrame != null)
            _iconFrame.transform.SetAsLastSibling();
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(position.x, -position.y);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureIconImage(Image image, Sprite sprite, bool preserveAspect)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
    }

    private static void ConfigureBackgroundImage(Image image)
    {
        if (image == null)
            return;

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = FrameBackgroundColor;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private void EnsureSlotDecorationFrame()
    {
        if (_slotDecorationIcon == null)
            _slotDecorationIcon = FindRawImage("SlotIcon");

        if (_slotDecorationIcon == null)
            return;

        if (_slotDecorationFrame == null)
            _slotDecorationFrame = FindImage("SlotIconFrame", "SlotDecorationFrame");

        if (_slotDecorationBackground == null)
            _slotDecorationBackground = FindImage("SlotIconBackground", "SlotDecorationBackground");

        if (_slotDecorationBackground == null)
        {
            var backgroundGo = new GameObject("SlotIconBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundGo.transform.SetParent(transform, false);
            _slotDecorationBackground = backgroundGo.GetComponent<Image>();
        }

        if (_slotDecorationFrame == null)
        {
            var frameGo = new GameObject("SlotIconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.transform.SetParent(transform, false);
            _slotDecorationFrame = frameGo.GetComponent<Image>();
        }

        ConfigureBackgroundImage(_slotDecorationBackground);
        ConfigureIconImage(_slotDecorationFrame, SlotDecorationFrameSprite, preserveAspect: false);
        _slotDecorationFrame.enabled = true;

        var framePosition = new Vector2(26f, 28f);
        var frameSize = new Vector2(74f, 74f);
        SetTopLeftRect(_slotDecorationBackground.rectTransform, framePosition + new Vector2(5f, 5f), frameSize - new Vector2(10f, 10f));
        SetTopLeftRect(_slotDecorationFrame.rectTransform, framePosition, frameSize);

        var iconSize = GetAspectFitSize(_slotDecorationIcon.texture, new Vector2(46f, 46f));
        var iconPosition = framePosition + (frameSize - iconSize) * 0.5f;
        SetTopLeftRect(_slotDecorationIcon.rectTransform, iconPosition, iconSize);
        _slotDecorationIcon.raycastTarget = false;

        _slotDecorationBackground.transform.SetAsLastSibling();
        _slotDecorationIcon.transform.SetAsLastSibling();
        _slotDecorationFrame.transform.SetAsLastSibling();
    }

    private Image FindImage(params string[] names)
    {
        var images = GetComponentsInChildren<Image>(true);
        foreach (var targetName in names)
        {
            foreach (var image in images)
            {
                if (image != null && image.gameObject.name == targetName)
                    return image;
            }
        }

        return null;
    }

    private RawImage FindRawImage(params string[] names)
    {
        var images = GetComponentsInChildren<RawImage>(true);
        foreach (var targetName in names)
        {
            foreach (var image in images)
            {
                if (image != null && image.gameObject.name == targetName)
                    return image;
            }
        }

        return null;
    }

    private static Vector2 GetAspectFitSize(Texture texture, Vector2 maxSize)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return maxSize;

        var aspect = texture.width / (float)texture.height;
        if (aspect >= 1f)
            return new Vector2(maxSize.x, maxSize.x / aspect);

        return new Vector2(maxSize.y * aspect, maxSize.y);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private static Sprite SlotIconFrameSprite
    {
        get
        {
            if (_slotIconFrameSprite == null)
                _slotIconFrameSprite = Resources.Load<Sprite>(SlotIconFrameResourcePath);

            return _slotIconFrameSprite;
        }
    }

    private static Sprite SlotDecorationFrameSprite
    {
        get
        {
            if (_slotDecorationFrameSprite == null)
                _slotDecorationFrameSprite = Resources.Load<Sprite>(SlotDecorationFrameResourcePath);

            return _slotDecorationFrameSprite;
        }
    }
}

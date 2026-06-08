using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TownInventoryResourceSkinner
{
    private const string PrefabPath = "Assets/0.MainProject/Prefabs/UI/Town/PF_TownInventory_UI.prefab";
    private const string ResourceRoot = "Assets/Resources/UI/UI_Inventory/";

    private const float PanelWidth = 1550f;
    private const float PanelHeight = 945f;
    private const float SlotSize = 103f;
    private const float SlotGapX = 18f;
    private const float SlotGapY = 20f;

    [MenuItem("RhythmRPG/Editors/UI/Apply Town Inventory Resource Skin")]
    public static void ApplyMenu()
    {
        ApplyToPrefab();
    }

    [MenuItem("RhythmRPG/Editors/UI/Open Town Inventory Preview Instance")]
    public static void OpenPreviewInstance()
    {
        var root = GameObject.Find("TownInventory_UI");
        if (root == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            root = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (root == null)
            {
                Debug.LogError($"[TownInventoryResourceSkinner] Failed to instantiate {PrefabPath}");
                return;
            }

            root.name = "TownInventory_UI";
        }

        var ui = root.GetComponent<TownInventoryUI>();
        if (ui != null)
        {
            var serialized = new SerializedObject(ui);
            serialized.FindProperty("_openOnEnable").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        var panel = root.transform.Find("Panel");
        if (panel != null)
            panel.gameObject.SetActive(true);

        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        if (root.transform is RectTransform rootRect)
            rootRect.anchoredPosition = Vector2.zero;

        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);
        Debug.Log("[TownInventoryResourceSkinner] Town inventory preview instance is open.");
    }

    public static void ApplyToPrefab()
    {
        ImportSprite("Panel_BackGround.png", 4096);
        ImportSprite("Panel_Frame_BackGround.png", 2048);
        ImportSprite("Panel_IteamDetail_Infomation.png", 2048);
        ImportSprite("Button_Category.png", 1024);
        ImportSprite("Frame_Iteam.png", 1024);

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ApplyLayout(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TownInventoryResourceSkinner] Applied UI_Inventory resources to PF_TownInventory_UI.");
    }

    private static void ApplyLayout(GameObject root)
    {
        var outerSprite = LoadSprite("Panel_BackGround.png");
        var paperSprite = LoadSprite("Panel_Frame_BackGround.png");
        var detailSprite = LoadSprite("Panel_IteamDetail_Infomation.png");
        var categorySprite = LoadSprite("Button_Category.png");
        var slotSprite = LoadSprite("Frame_Iteam.png");

        ConfigureCanvas(root);

        var panel = FindOrCreateRect(root.transform, "Panel");
        panel.gameObject.SetActive(true);
        SetCenter(panel, 0f, 0f, PanelWidth, PanelHeight);
        ConfigureImage(panel.gameObject, outerSprite, Color.white, true);

        var categories = FindOrCreateRect(panel, "Categories");
        RemoveComponent<HorizontalLayoutGroup>(categories.gameObject);
        SetTopLeft(categories, 0f, 0f, PanelWidth, 145f);

        var categoryButtons = new List<Button>();
        string[] categoryNames = { "All", "Equipment", "Consumable", "Material" };
        for (int i = 0; i < categoryNames.Length; i++)
        {
            var buttonRect = FindOrCreateRect(categories, $"Btn_{categoryNames[i]}");
            SetTopLeft(buttonRect, 185f + (i * 240f), 44f, 205f, 72f);
            var image = ConfigureImage(buttonRect.gameObject, categorySprite, Color.white, true);
            var button = GetOrAdd<Button>(buttonRect.gameObject);
            button.targetGraphic = image;
            ConfigureButtonColors(button);
            ConfigureLabel(buttonRect, "Text", categoryNames[i], 22f, new Color32(255, 238, 198, 255));
            categoryButtons.Add(button);
        }

        var sortDropdown = ConfigureSortDropdown(panel, categorySprite);
        var gridContent = ConfigureInventoryGrid(panel, paperSprite, slotSprite);
        var details = ConfigureDetailsPanel(panel, detailSprite, categorySprite);
        var quantityPopup = ConfigureQuantityPopup(panel, paperSprite, categorySprite);
        var slotPrefab = ConfigureSlotPrefab(root.transform, slotSprite);

        BindTownInventory(root, gridContent, slotPrefab, details, quantityPopup, sortDropdown, categoryButtons);
    }

    private static void ConfigureCanvas(GameObject root)
    {
        if (root.transform is RectTransform rootRect)
        {
            Stretch(rootRect);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.localPosition = Vector3.zero;
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;
        }
        else
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }

        var canvas = GetOrAdd<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 8000;

        var scaler = GetOrAdd<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAdd<GraphicRaycaster>(root);
    }

    private static TMP_Dropdown ConfigureSortDropdown(RectTransform panel, Sprite categorySprite)
    {
        var sortRect = FindOrCreateRect(panel, "SortDropdown");
        SetTopLeft(sortRect, 1145f, 44f, 205f, 72f);

        var image = ConfigureImage(sortRect.gameObject, categorySprite, Color.white, true);
        var dropdown = GetOrAdd<TMP_Dropdown>(sortRect.gameObject);
        dropdown.targetGraphic = image;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string> { "Recent", "Name", "Grade" });

        var label = ConfigureLabel(sortRect, "Label", "Recent", 22f, new Color32(255, 238, 198, 255));
        label.alignment = TextAlignmentOptions.Center;
        dropdown.captionText = label;

        var template = sortRect.Find("Template") as RectTransform;
        if (template != null)
        {
            SetTopLeft(template, 0f, 76f, 205f, 110f);
            template.gameObject.SetActive(false);
        }

        return dropdown;
    }

    private static Transform ConfigureInventoryGrid(RectTransform panel, Sprite paperSprite, Sprite slotSprite)
    {
        var scrollRectTransform = FindOrCreateRect(panel, "Scroll_Grid");
        SetTopLeft(scrollRectTransform, 105f, 158f, 880f, 642f);
        ConfigureImage(scrollRectTransform.gameObject, paperSprite, Color.white, true);

        var scrollRect = GetOrAdd<ScrollRect>(scrollRectTransform.gameObject);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 32f;

        var viewport = FindOrCreateRect(scrollRectTransform, "Viewport");
        SetStretch(viewport, 46f, 54f, 46f, 42f);
        var viewportImage = ConfigureImage(viewport.gameObject, null, Color.white, false);
        viewportImage.raycastTarget = false;
        GetOrAdd<Mask>(viewport.gameObject).showMaskGraphic = false;

        var slotDecor = FindOrCreateRect(viewport, "SlotFrameDecor");
        Stretch(slotDecor);
        slotDecor.SetAsFirstSibling();
        BuildSlotDecor(slotDecor, slotSprite);

        var content = FindOrCreateRect(viewport, "Content");
        content.SetAsLastSibling();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 520f);

        var grid = GetOrAdd<GridLayoutGroup>(content.gameObject);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.cellSize = new Vector2(SlotSize, SlotSize);
        grid.spacing = new Vector2(SlotGapX, SlotGapY);
        grid.padding = new RectOffset(16, 0, 16, 14);

        var fitter = GetOrAdd<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        return content;
    }

    private static TownInventoryDetailsUI ConfigureDetailsPanel(RectTransform panel, Sprite detailSprite, Sprite buttonSprite)
    {
        var frame = FindOrCreateRect(panel, "DetailsFrame");
        SetTopLeft(frame, 1016f, 158f, 452f, 642f);
        var frameImage = ConfigureImage(frame.gameObject, detailSprite, Color.white, false);
        frameImage.raycastTarget = false;

        var details = FindOrCreateRect(panel, "DetailsPanel");
        SetTopLeft(details, 1016f, 158f, 452f, 642f);
        ConfigureImage(details.gameObject, null, new Color(1f, 1f, 1f, 0f), false);
        details.SetAsLastSibling();
        details.gameObject.SetActive(false);

        var icon = FindOrCreateRect(details, "Icon");
        SetTopCenter(icon, 0f, 82f, 140f, 140f);
        var iconImage = ConfigureImage(icon.gameObject, null, Color.white, false);
        iconImage.preserveAspect = true;

        var name = ConfigureLabel(details, "Name", "", 28f, new Color32(255, 234, 184, 255));
        SetTopLeft(name.rectTransform, 56f, 238f, 340f, 48f);
        name.alignment = TextAlignmentOptions.Center;

        var desc = ConfigureLabel(details, "Desc", "", 19f, new Color32(221, 201, 171, 255));
        SetTopLeft(desc.rectTransform, 56f, 300f, 340f, 178f);
        desc.alignment = TextAlignmentOptions.TopLeft;

        var stats = ConfigureLabel(details, "Stats", "", 18f, new Color32(255, 214, 120, 255));
        SetTopLeft(stats.rectTransform, 56f, 494f, 340f, 78f);
        stats.alignment = TextAlignmentOptions.TopLeft;

        var useButton = ConfigureActionButton(details, "UseBtn", "Use", buttonSprite, 68f, 572f);
        var trashButton = ConfigureActionButton(details, "TrashBtn", "Trash", buttonSprite, 254f, 572f);

        var detailsUi = GetOrAdd<TownInventoryDetailsUI>(details.gameObject);
        var serialized = new SerializedObject(detailsUi);
        serialized.FindProperty("_icon").objectReferenceValue = iconImage;
        serialized.FindProperty("_nameText").objectReferenceValue = name;
        serialized.FindProperty("_descText").objectReferenceValue = desc;
        serialized.FindProperty("_statText").objectReferenceValue = stats;
        serialized.FindProperty("_useBtn").objectReferenceValue = useButton;
        serialized.FindProperty("_trashBtn").objectReferenceValue = trashButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return detailsUi;
    }

    private static ItemQuantityPopupUI ConfigureQuantityPopup(RectTransform panel, Sprite paperSprite, Sprite buttonSprite)
    {
        var popup = FindOrCreateRect(panel, "QuantityPopup");
        popup.gameObject.SetActive(false);
        SetCenter(popup, 0f, -4f, 500f, 340f);
        ConfigureImage(popup.gameObject, paperSprite, Color.white, true);

        var title = ConfigureLabel(popup, "Title", "Discard Item", 25f, new Color32(80, 49, 25, 255));
        SetTopLeft(title.rectTransform, 48f, 36f, 404f, 42f);

        var input = FindOrCreateRect(popup, "InputField");
        SetTopCenter(input, 0f, 118f, 160f, 50f);
        ConfigureImage(input.gameObject, null, new Color32(54, 34, 21, 235), true);
        var inputField = GetOrAdd<TMP_InputField>(input.gameObject);
        var inputText = ConfigureLabel(input, "Text", "1", 24f, new Color32(255, 238, 198, 255));
        inputField.textComponent = inputText;
        inputField.textViewport = inputText.rectTransform;

        var sliderRect = FindOrCreateRect(popup, "Slider");
        SetTopCenter(sliderRect, 0f, 190f, 350f, 32f);
        var slider = GetOrAdd<Slider>(sliderRect.gameObject);
        slider.direction = Slider.Direction.LeftToRight;

        var confirm = ConfigureActionButton(popup, "ConfirmBtn", "Confirm", buttonSprite, 88f, 252f);
        var cancel = ConfigureActionButton(popup, "CancelBtn", "Cancel", buttonSprite, 266f, 252f);

        var popupUi = GetOrAdd<ItemQuantityPopupUI>(popup.gameObject);
        var serialized = new SerializedObject(popupUi);
        serialized.FindProperty("_slider").objectReferenceValue = slider;
        serialized.FindProperty("_inputField").objectReferenceValue = inputField;
        serialized.FindProperty("_titleText").objectReferenceValue = title;
        serialized.FindProperty("_confirmBtn").objectReferenceValue = confirm;
        serialized.FindProperty("_cancelBtn").objectReferenceValue = cancel;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return popupUi;
    }

    private static Button ConfigureActionButton(RectTransform parent, string name, string label, Sprite sprite, float x, float y)
    {
        var rect = FindOrCreateRect(parent, name);
        SetTopLeft(rect, x, y, 130f, 58f);
        var image = ConfigureImage(rect.gameObject, sprite, Color.white, true);
        var button = GetOrAdd<Button>(rect.gameObject);
        button.targetGraphic = image;
        ConfigureButtonColors(button);
        ConfigureLabel(rect, "Text", label, 18f, new Color32(255, 238, 198, 255));
        return button;
    }

    private static TownInventorySlotUI ConfigureSlotPrefab(Transform root, Sprite slotSprite)
    {
        var slot = FindOrCreateRect(root, "Prefab_TownSlot");
        slot.gameObject.SetActive(false);
        slot.anchorMin = new Vector2(0.5f, 0.5f);
        slot.anchorMax = new Vector2(0.5f, 0.5f);
        slot.pivot = new Vector2(0.5f, 0.5f);
        slot.sizeDelta = new Vector2(SlotSize, SlotSize);
        ConfigureImage(slot.gameObject, slotSprite, Color.white, true);

        var button = GetOrAdd<Button>(slot.gameObject);
        button.targetGraphic = slot.GetComponent<Image>();
        ConfigureButtonColors(button);

        var icon = FindOrCreateRect(slot, "Icon");
        SetStretch(icon, 20f, 18f, 20f, 20f);
        var iconImage = ConfigureImage(icon.gameObject, null, Color.white, false);
        iconImage.preserveAspect = true;

        var amount = ConfigureLabel(slot, "Amount", "1", 21f, Color.black);
        SetBottomRight(amount.rectTransform, 9f, 8f, 50f, 28f);
        amount.alignment = TextAlignmentOptions.BottomRight;

        var equipMark = FindOrCreateRect(slot, "EquipMark");
        SetTopRight(equipMark, 8f, 8f, 22f, 18f);
        ConfigureImage(equipMark.gameObject, null, new Color(1f, 0.86f, 0.12f, 0.95f), false);
        equipMark.gameObject.SetActive(false);

        var slotUi = GetOrAdd<TownInventorySlotUI>(slot.gameObject);
        var serialized = new SerializedObject(slotUi);
        serialized.FindProperty("_icon").objectReferenceValue = iconImage;
        serialized.FindProperty("_amountText").objectReferenceValue = amount;
        serialized.FindProperty("_btn").objectReferenceValue = button;
        serialized.FindProperty("_equipMark").objectReferenceValue = equipMark.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return slotUi;
    }

    private static void BindTownInventory(
        GameObject root,
        Transform gridContent,
        TownInventorySlotUI slotPrefab,
        TownInventoryDetailsUI details,
        ItemQuantityPopupUI quantityPopup,
        TMP_Dropdown sortDropdown,
        List<Button> categoryButtons)
    {
        var detailsSerialized = new SerializedObject(details);
        detailsSerialized.FindProperty("_popup").objectReferenceValue = quantityPopup;
        detailsSerialized.ApplyModifiedPropertiesWithoutUndo();

        var ui = GetOrAdd<TownInventoryUI>(root);
        var serialized = new SerializedObject(ui);
        serialized.FindProperty("_gridContent").objectReferenceValue = gridContent;
        serialized.FindProperty("_slotPrefab").objectReferenceValue = slotPrefab;
        serialized.FindProperty("_detailsUI").objectReferenceValue = details;
        serialized.FindProperty("_sortDropdown").objectReferenceValue = sortDropdown;
        serialized.FindProperty("_handleHotkey").boolValue = true;
        serialized.FindProperty("_openOnEnable").boolValue = false;

        var buttons = serialized.FindProperty("_categoryButtons");
        buttons.arraySize = categoryButtons.Count;
        for (int i = 0; i < categoryButtons.Count; i++)
            buttons.GetArrayElementAtIndex(i).objectReferenceValue = categoryButtons[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);
    }

    private static void BuildSlotDecor(RectTransform parent, Sprite slotSprite)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 6; col++)
            {
                var slot = FindOrCreateRect(parent, $"SlotFrame_{row}_{col}");
                SetTopLeft(slot, 16f + col * (SlotSize + SlotGapX), 16f + row * (SlotSize + SlotGapY), SlotSize, SlotSize);
                var image = ConfigureImage(slot.gameObject, slotSprite, Color.white, false);
                image.raycastTarget = false;
            }
        }
    }

    private static TextMeshProUGUI ConfigureLabel(RectTransform parent, string name, string value, float size, Color color)
    {
        var rect = FindOrCreateRect(parent, name);
        Stretch(rect);
        var text = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
        text.text = value;
        text.fontSize = size;
        text.fontSizeMin = Mathf.Max(9f, size - 10f);
        text.fontSizeMax = size;
        text.enableAutoSizing = true;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = new Vector4(3f, 2f, 3f, 2f);
        return text;
    }

    private static Image ConfigureImage(GameObject go, Sprite sprite, Color color, bool raycastTarget)
    {
        var image = GetOrAdd<Image>(go);
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        return image;
    }

    private static void ConfigureButtonColors(Selectable selectable)
    {
        var colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
        colors.pressedColor = new Color(0.82f, 0.68f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
    }

    private static void ImportSprite(string fileName, int maxTextureSize)
    {
        string path = ResourceRoot + fileName;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[TownInventoryResourceSkinner] Missing texture importer: {path}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = maxTextureSize;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Sprite LoadSprite(string fileName)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ResourceRoot + fileName);
        if (sprite == null)
            Debug.LogError($"[TownInventoryResourceSkinner] Missing sprite: {ResourceRoot + fileName}");
        return sprite;
    }

    private static RectTransform FindOrCreateRect(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
        {
            var existingRect = child as RectTransform;
            if (existingRect != null)
                return existingRect;
        }

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static void RemoveComponent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component != null)
            Object.DestroyImmediate(component);
    }

    private static void Stretch(RectTransform rect)
    {
        SetStretch(rect, 0f, 0f, 0f, 0f);
    }

    private static void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetTopRight(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetBottomRight(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetTopCenter(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetCenter(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }
}

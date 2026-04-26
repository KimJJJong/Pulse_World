using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Client.Content.Item;

public class InventoryUIBuilder : EditorWindow
{
    [MenuItem("RhythmRPG/Editors/UI/Create Inventory UI")]
    public static void ShowWindow()
    {
        GetWindow<InventoryUIBuilder>("Inventory UI Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Create Inventory UIs", EditorStyles.boldLabel);

        if (GUILayout.Button("Create Home Inventory (WorldSpace)"))
        {
            CreateHomeInventory();
        }

        if (GUILayout.Button("Create Town Inventory (ScreenSpace)"))
        {
            CreateTownInventory();
        }
    }

    private void CreateHomeInventory()
    {
        GameObject root = new GameObject("HomeInventory_WorldSpace");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1000, 1000); // Reasonable size
        rt.localScale = Vector3.one * 0.001f; 

        HomeInventoryUI mainScript = root.AddComponent<HomeInventoryUI>();

        GameObject slotsContainer = CreateChild(root, "SlotsContainer", true);
        
        List<HomeEquipSlotUI> createdSlots = new List<HomeEquipSlotUI>();

        // Layout for slots
        GridLayoutGroup glg = slotsContainer.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(100, 100);
        glg.spacing = new Vector2(10, 10);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;

        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (slot == EquipmentSlot.None) continue;

            GameObject slotObj = CreateChild(slotsContainer, $"Slot_{slot}");
            Image bg = slotObj.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.5f);
            
            Button btn = slotObj.AddComponent<Button>();
            
            // Visuals
            GameObject emptyVis = CreateChild(slotObj, "Empty", true);
            CreateText(emptyVis, slot.ToString(), 24, Color.gray);
            
            GameObject filledVis = CreateChild(slotObj, "Filled", true);
            filledVis.SetActive(false);
            
            // Fix: Create Icon as a child of Filled
            Image iconImg = CreateImage(filledVis, "Icon"); 
            iconImg.color = Color.white;
            
            // Component
            HomeEquipSlotUI slotUI = slotObj.AddComponent<HomeEquipSlotUI>();
            SerializedObject so = new SerializedObject(slotUI);
            so.FindProperty("_targetSlot").enumValueIndex = (int)slot;
            so.FindProperty("_icon").objectReferenceValue = iconImg;
            so.FindProperty("_btn").objectReferenceValue = btn;
            so.FindProperty("_emptyVisual").objectReferenceValue = emptyVis;
            so.FindProperty("_filledVisual").objectReferenceValue = filledVis;
            so.ApplyModifiedProperties();

            createdSlots.Add(slotUI);
        }

        // Popup
        GameObject popupObj = CreateChild(root, "EquipPopup", true);
        popupObj.SetActive(false);
        Image popupBg = popupObj.AddComponent<Image>();
        popupBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        
        GameObject popupContent = CreateChild(popupObj, "Content"); 
        RectTransform contentRt = popupContent.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 0);
        contentRt.anchorMax = new Vector2(1, 0.8f);
        contentRt.offsetMin = new Vector2(10, 10);
        contentRt.offsetMax = new Vector2(-10, -10);
        
        VerticalLayoutGroup vlg = popupContent.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 5;

        // Close Btn
        GameObject closeBtnObj = CreateChild(popupObj, "CloseBtn");
        RectTransform closeRt = closeBtnObj.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.9f, 0.9f);
        closeRt.anchorMax = new Vector2(1, 1);
        closeRt.offsetMin = Vector2.zero;
        closeRt.offsetMax = Vector2.zero;
        closeBtnObj.AddComponent<Image>().color = Color.red;
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        CreateText(CreateChild(closeBtnObj, "Text", true), "X");

        // Title
        GameObject titleObj = CreateChild(popupObj, "Title");
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.85f);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = new Vector2(-50, 0); // space for close btn
        TextMeshProUGUI titleText = CreateText(titleObj, "Select Item", 30, Color.yellow);

        HomeEquipPopupUI popupUI = popupObj.AddComponent<HomeEquipPopupUI>();
        SerializedObject popupSo = new SerializedObject(popupUI);
        popupSo.FindProperty("_content").objectReferenceValue = popupContent.transform;
        popupSo.FindProperty("_title").objectReferenceValue = titleText;
        popupSo.FindProperty("_closeBtn").objectReferenceValue = closeBtn;
        
        // Item Prefab
        GameObject itemPrefab = CreateChild(root, "Prefab_PopupItem");
        itemPrefab.SetActive(false);
        LayoutElement le = itemPrefab.AddComponent<LayoutElement>();
        le.minHeight = 60;
        le.preferredHeight = 60;
        
        Image itemBg = itemPrefab.AddComponent<Image>();
        itemBg.color = new Color(0.2f, 0.2f, 0.2f);
        itemPrefab.AddComponent<Button>();

        // Icon
        GameObject pIconObj = CreateChild(itemPrefab, "Icon");
        RectTransform pIconRt = pIconObj.GetComponent<RectTransform>();
        pIconRt.anchorMin = new Vector2(0, 0);
        pIconRt.anchorMax = new Vector2(0.2f, 1);
        pIconObj.AddComponent<Image>().color = Color.white;

        // Name
        GameObject pNameObj = CreateChild(itemPrefab, "Name");
        RectTransform pNameRt = pNameObj.GetComponent<RectTransform>();
        pNameRt.anchorMin = new Vector2(0.25f, 0.5f);
        pNameRt.anchorMax = new Vector2(0.8f, 1);
        TextMeshProUGUI pNameText = CreateText(pNameObj, "Item Name", 24, Color.white);
        pNameText.alignment = TextAlignmentOptions.MidlineLeft;

        // Level
        GameObject pLvlObj = CreateChild(itemPrefab, "Level");
        RectTransform pLvlRt = pLvlObj.GetComponent<RectTransform>();
        pLvlRt.anchorMin = new Vector2(0.8f, 0);
        pLvlRt.anchorMax = new Vector2(1, 1);
        TextMeshProUGUI pLvlText = CreateText(pLvlObj, "+0", 24, Color.green);

        // EquipMark
        GameObject pMarkObj = CreateChild(itemPrefab, "EquipMark");
        RectTransform pMarkRt = pMarkObj.GetComponent<RectTransform>();
        pMarkRt.anchorMin = new Vector2(0.25f, 0);
        pMarkRt.anchorMax = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI markText = CreateText(pMarkObj, "[E]", 18, Color.cyan);
        markText.alignment = TextAlignmentOptions.MidlineLeft;

        HomeEquipPopupItemUI itemUI = itemPrefab.AddComponent<HomeEquipPopupItemUI>();
        SerializedObject itemSo = new SerializedObject(itemUI);
        itemSo.FindProperty("_nameText").objectReferenceValue = pNameText;
        itemSo.FindProperty("_levelText").objectReferenceValue = pLvlText;
        itemSo.FindProperty("_btn").objectReferenceValue = itemPrefab.GetComponent<Button>();
        itemSo.FindProperty("_equippedMark").objectReferenceValue = pMarkObj;
        itemSo.ApplyModifiedProperties();

        popupSo.FindProperty("_itemPrefab").objectReferenceValue = itemPrefab;
        popupSo.ApplyModifiedProperties();

        SerializedObject mainSo = new SerializedObject(mainScript);
        SerializedProperty slotsProp = mainSo.FindProperty("_slots");
        slotsProp.arraySize = createdSlots.Count;
        for(int i=0; i<createdSlots.Count; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = createdSlots[i];
        }
        mainSo.FindProperty("_popup").objectReferenceValue = popupUI;
        mainSo.ApplyModifiedProperties();

        Selection.activeGameObject = root;
        Debug.Log("Created Home Inventory");
    }

    private void CreateTownInventory()
    {
        GameObject root = new GameObject("TownInventory_UI");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        GameObject panel = CreateChild(root, "Panel", true);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.1f, 0.1f);
        panelRt.anchorMax = new Vector2(0.9f, 0.9f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        // Categories
        GameObject catContainer = CreateChild(panel, "Categories");
        RectTransform catRt = catContainer.GetComponent<RectTransform>();
        catRt.anchorMin = new Vector2(0, 0.9f);
        catRt.anchorMax = new Vector2(0.8f, 1);
        catRt.offsetMin = new Vector2(10, -10);
        catRt.offsetMax = new Vector2(0, -10);
        
        HorizontalLayoutGroup hlg = catContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 5;
        
        string[] cats = { "All", "Equipment", "Consumable", "Material" };
        List<Button> catBtns = new List<Button>();
        foreach(var c in cats)
        {
            GameObject btnObj = CreateChild(catContainer, $"Btn_{c}");
            btnObj.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
            Button btn = btnObj.AddComponent<Button>();
            CreateText(CreateChild(btnObj, "Text", true), c, 20, Color.white);
            catBtns.Add(btn);
        }

        // Sort
        GameObject sortObj = CreateChild(panel, "SortDropdown");
        RectTransform sortRt = sortObj.GetComponent<RectTransform>();
        sortRt.anchorMin = new Vector2(0.81f, 0.9f);
        sortRt.anchorMax = new Vector2(0.99f, 1);
        sortRt.offsetMin = new Vector2(0, -10);
        sortRt.offsetMax = new Vector2(0, -10);
        
        TMP_Dropdown dropdown = sortObj.AddComponent<TMP_Dropdown>();
        Image sortBg = sortObj.AddComponent<Image>();
        sortBg.color = Color.white;
        
        GameObject labelObj = CreateChild(sortObj, "Label", true);
        TextMeshProUGUI label = CreateText(labelObj, "Sort", 18, Color.black);
        dropdown.captionText = label;
        dropdown.targetGraphic = sortBg;
        
        // Template (Dropdown minimal setup)
        GameObject template = CreateChild(sortObj, "Template", true);
        template.SetActive(false);
        Image tmplBg = template.AddComponent<Image>();
        tmplBg.color = new Color(0.9f, 0.9f, 0.9f);
        ScrollRect tmplSr = template.AddComponent<ScrollRect>();
        
        GameObject vp = CreateChild(template, "Viewport", true);
        vp.AddComponent<Image>().color = Color.white;
        vp.AddComponent<Mask>().showMaskGraphic = false;
        
        GameObject contentInfo = CreateChild(vp, "Content", true);
        RectTransform contentRt = contentInfo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.sizeDelta = new Vector2(0, 28);

        // Item Logic (Must have Toggle)
        GameObject itemObj = CreateChild(contentInfo, "Item");
        RectTransform itemRt = itemObj.GetComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0, 0.5f);
        itemRt.anchorMax = new Vector2(1, 0.5f);
        itemRt.sizeDelta = new Vector2(0, 24);
        
        Toggle itemToggle = itemObj.AddComponent<Toggle>();
        
        // Item Background
        GameObject itemBg = CreateChild(itemObj, "Item Background", true);
        Image itemBgImg = itemBg.AddComponent<Image>();
        itemBgImg.color = new Color(0.2f, 0.2f, 0.5f, 0); // Transparent usually
        itemToggle.targetGraphic = itemBgImg;
        
        // Item Checkmark
        GameObject itemCheck = CreateChild(itemObj, "Item Checkmark");
        RectTransform checkRt = itemCheck.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0, 0.5f);
        checkRt.anchorMax = new Vector2(0, 0.5f);
        checkRt.sizeDelta = new Vector2(20, 20);
        checkRt.anchoredPosition = new Vector2(10, 0);
        Image checkImg = itemCheck.AddComponent<Image>();
        checkImg.color = Color.green;
        itemToggle.graphic = checkImg;

        // Item Label
        GameObject itemLabel = CreateChild(itemObj, "Item Label");
        RectTransform labelRt = itemLabel.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(24, 0); // Offset for checkmark
        labelRt.offsetMax = new Vector2(-10, 0);
        TextMeshProUGUI itemText = CreateText(itemLabel, "Option", 18, Color.black);
        itemText.alignment = TextAlignmentOptions.MidlineLeft;

        dropdown.template = template.GetComponent<RectTransform>();
        dropdown.itemText = itemText;

        // Grid Scroll
        GameObject scrollObj = CreateChild(panel, "Scroll_Grid");
        RectTransform sRt = scrollObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0, 0);
        sRt.anchorMax = new Vector2(0.7f, 0.88f);
        sRt.offsetMin = new Vector2(10, 10);
        sRt.offsetMax = new Vector2(0, 0);
        
        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        Image srBg = scrollObj.AddComponent<Image>();
        srBg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        GameObject sVp = CreateChild(scrollObj, "Viewport", true);
        sVp.AddComponent<Image>().raycastTarget = false;
        sVp.AddComponent<Mask>().showMaskGraphic = false;
        
        GameObject sContent = CreateChild(sVp, "Content");
        RectTransform sContentRt = sContent.GetComponent<RectTransform>();
        sContentRt.anchorMin = new Vector2(0, 1);
        sContentRt.anchorMax = new Vector2(1, 1);
        sContentRt.pivot = new Vector2(0.5f, 1);
        sContentRt.sizeDelta = new Vector2(0, 500); // dynamic
        
        GridLayoutGroup glg = sContent.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(100, 100);
        glg.spacing = new Vector2(10, 10);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
        
        var csf = sContent.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        sr.viewport = sVp.GetComponent<RectTransform>();
        sr.content = sContentRt;

        // Quantity Popup
        GameObject qPopupObj = CreateChild(panel, "QuantityPopup");
        qPopupObj.SetActive(false);
        RectTransform qpRt = qPopupObj.GetComponent<RectTransform>();
        qpRt.anchorMin = new Vector2(0.3f, 0.3f);
        qpRt.anchorMax = new Vector2(0.7f, 0.7f);
        Image qpBg = qPopupObj.AddComponent<Image>();
        qpBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        
        // Popup Components
        GameObject qTitle = CreateChild(qPopupObj, "Title");
        RectTransform qtRt = qTitle.GetComponent<RectTransform>();
        qtRt.anchorMin = new Vector2(0, 0.8f);
        qtRt.anchorMax = new Vector2(1, 1);
        TextMeshProUGUI qTitleText = CreateText(qTitle, "Discard Item", 24, Color.white);
        
        GameObject qInput = CreateChild(qPopupObj, "InputField");
        RectTransform qiRt = qInput.GetComponent<RectTransform>();
        qiRt.anchorMin = new Vector2(0.3f, 0.5f);
        qiRt.anchorMax = new Vector2(0.7f, 0.7f);
        Image qiBg = qInput.AddComponent<Image>();
        qiBg.color = Color.white;
        TMP_InputField qInputField = qInput.AddComponent<TMP_InputField>();
        qInputField.targetGraphic = qiBg;
        GameObject qiTextObj = CreateChild(qInput, "Text", true);
        TextMeshProUGUI qiText = CreateText(qiTextObj, "1", 20, Color.black);
        qInputField.textComponent = qiText;
        qInputField.textViewport = qiTextObj.GetComponent<RectTransform>();
        
        GameObject qSlider = CreateChild(qPopupObj, "Slider");
        RectTransform qsRt = qSlider.GetComponent<RectTransform>();
        qsRt.anchorMin = new Vector2(0.1f, 0.3f);
        qsRt.anchorMax = new Vector2(0.9f, 0.45f);
        Slider slider = qSlider.AddComponent<Slider>();
        // Slider Visuals (Minimal)
        GameObject qsBg = CreateChild(qSlider, "Background", true);
        qsBg.AddComponent<Image>().color = Color.gray;
        GameObject qsFillArea = CreateChild(qSlider, "Fill Area", true);
        GameObject qsFill = CreateChild(qsFillArea, "Fill", true);
        qsFill.AddComponent<Image>().color = Color.green;
        slider.fillRect = qsFill.GetComponent<RectTransform>();
        slider.targetGraphic = qsBg.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;

        GameObject qConfirm = CreateChild(qPopupObj, "ConfirmBtn");
        RectTransform qcRt = qConfirm.GetComponent<RectTransform>();
        qcRt.anchorMin = new Vector2(0.1f, 0.05f);
        qcRt.anchorMax = new Vector2(0.45f, 0.2f);
        Button qConfirmBtn = qConfirm.AddComponent<Button>();
        qConfirm.AddComponent<Image>().color = new Color(0, 0.6f, 0);
        CreateText(CreateChild(qConfirm, "Text", true), "Confirm");

        GameObject qCancel = CreateChild(qPopupObj, "CancelBtn");
        RectTransform qxRt = qCancel.GetComponent<RectTransform>();
        qxRt.anchorMin = new Vector2(0.55f, 0.05f);
        qxRt.anchorMax = new Vector2(0.9f, 0.2f);
        Button qCancelBtn = qCancel.AddComponent<Button>();
        qCancel.AddComponent<Image>().color = new Color(0.6f, 0, 0);
        CreateText(CreateChild(qCancel, "Text", true), "Cancel");

        ItemQuantityPopupUI quantityPopupUI = qPopupObj.AddComponent<ItemQuantityPopupUI>();
        SerializedObject qSo = new SerializedObject(quantityPopupUI);
        qSo.FindProperty("_slider").objectReferenceValue = slider;
        qSo.FindProperty("_inputField").objectReferenceValue = qInputField;
        qSo.FindProperty("_titleText").objectReferenceValue = qTitleText;
        qSo.FindProperty("_confirmBtn").objectReferenceValue = qConfirmBtn;
        qSo.FindProperty("_cancelBtn").objectReferenceValue = qCancelBtn;
        qSo.ApplyModifiedProperties();

        // Details Panel
        GameObject detailsObj = CreateChild(panel, "DetailsPanel");
        RectTransform dRt = detailsObj.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0.71f, 0);
        dRt.anchorMax = new Vector2(1, 0.88f);
        dRt.offsetMin = new Vector2(5, 10);
        dRt.offsetMax = new Vector2(-10, 0);
        
        Image detBg = detailsObj.AddComponent<Image>();
        detBg.color = new Color(0.15f, 0.15f, 0.15f);
        
        GameObject dIconObj = CreateChild(detailsObj, "Icon");
        RectTransform dIconRt = dIconObj.GetComponent<RectTransform>();
        dIconRt.anchorMin = new Vector2(0.5f, 0.8f);
        dIconRt.anchorMax = new Vector2(0.5f, 0.8f);
        dIconRt.sizeDelta = new Vector2(100, 100);
        dIconObj.AddComponent<Image>().color = Color.white;

        GameObject dName = CreateChild(detailsObj, "Name");
        RectTransform dNameRt = dName.GetComponent<RectTransform>();
        dNameRt.anchorMin = new Vector2(0, 0.7f);
        dNameRt.anchorMax = new Vector2(1, 0.78f);
        TextMeshProUGUI dNameText = CreateText(dName, "Item Name", 24, Color.white);
        
        GameObject dDesc = CreateChild(detailsObj, "Desc");
        RectTransform dDescRt = dDesc.GetComponent<RectTransform>();
        dDescRt.anchorMin = new Vector2(0.1f, 0.4f);
        dDescRt.anchorMax = new Vector2(0.9f, 0.65f);
        TextMeshProUGUI dDescText = CreateText(dDesc, "Description...", 18, Color.gray);
        dDescText.alignment = TextAlignmentOptions.TopLeft;
        
        GameObject dStat = CreateChild(detailsObj, "Stats");
        RectTransform dStatRt = dStat.GetComponent<RectTransform>();
        dStatRt.anchorMin = new Vector2(0.1f, 0.2f);
        dStatRt.anchorMax = new Vector2(0.9f, 0.38f);
        TextMeshProUGUI dStatText = CreateText(dStat, "Stats...", 18, Color.yellow);
        dStatText.alignment = TextAlignmentOptions.TopLeft;
        
        GameObject useBtnObj = CreateChild(detailsObj, "UseBtn");
        RectTransform useRt = useBtnObj.GetComponent<RectTransform>();
        useRt.anchorMin = new Vector2(0.1f, 0.05f);
        useRt.anchorMax = new Vector2(0.45f, 0.15f);
        Button useBtn = useBtnObj.AddComponent<Button>();
        useBtnObj.AddComponent<Image>().color = new Color(0, 0.6f, 0);
        CreateText(CreateChild(useBtnObj, "Text", true), "Use");

        GameObject trashBtnObj = CreateChild(detailsObj, "TrashBtn");
        RectTransform trashRt = trashBtnObj.GetComponent<RectTransform>();
        trashRt.anchorMin = new Vector2(0.55f, 0.05f);
        trashRt.anchorMax = new Vector2(0.9f, 0.15f);
        Button trashBtn = trashBtnObj.AddComponent<Button>();
        trashBtnObj.AddComponent<Image>().color = new Color(0.6f, 0, 0);
        CreateText(CreateChild(trashBtnObj, "Text", true), "Trash");

        TownInventoryDetailsUI detailsUI = detailsObj.AddComponent<TownInventoryDetailsUI>();
        SerializedObject detSo = new SerializedObject(detailsUI);
        detSo.FindProperty("_icon").objectReferenceValue = dIconObj.GetComponent<Image>();
        detSo.FindProperty("_nameText").objectReferenceValue = dNameText;
        detSo.FindProperty("_descText").objectReferenceValue = dDescText;
        detSo.FindProperty("_statText").objectReferenceValue = dStatText;
        detSo.FindProperty("_useBtn").objectReferenceValue = useBtn;
        detSo.FindProperty("_trashBtn").objectReferenceValue = trashBtn;
        detSo.FindProperty("_popup").objectReferenceValue = quantityPopupUI;
        detSo.ApplyModifiedProperties();

        // Slot Prefab
        GameObject slotPrefab = CreateChild(root, "Prefab_TownSlot");
        slotPrefab.SetActive(false);
        Image slotImg = slotPrefab.AddComponent<Image>();
        slotImg.color = new Color(0.2f, 0.2f, 0.2f); // BG
        Button slotBtn = slotPrefab.AddComponent<Button>();
        
        GameObject iconObj = CreateChild(slotPrefab, "Icon", true);
        // iconObj margin?
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.1f, 0.1f);
        iconRt.anchorMax = new Vector2(0.9f, 0.9f);
        Image realIcon = iconObj.AddComponent<Image>();
        realIcon.color = Color.white;

        GameObject amtObj = CreateChild(slotPrefab, "Amount");
        RectTransform amtRt = amtObj.GetComponent<RectTransform>();
        amtRt.anchorMin = new Vector2(0.5f, 0);
        amtRt.anchorMax = new Vector2(1, 0.4f);
        TextMeshProUGUI amtText = CreateText(amtObj, "1", 20, Color.white);
        amtText.alignment = TextAlignmentOptions.BottomRight;

        GameObject eqMark = CreateChild(slotPrefab, "EquipMark");
        RectTransform eqRt = eqMark.GetComponent<RectTransform>();
        eqRt.anchorMin = new Vector2(0, 0.7f);
        eqRt.anchorMax = new Vector2(0.3f, 1);
        eqMark.AddComponent<Image>().color = Color.yellow;
        
        TownInventorySlotUI slotUI = slotPrefab.AddComponent<TownInventorySlotUI>();
        SerializedObject slotSo = new SerializedObject(slotUI);
        slotSo.FindProperty("_icon").objectReferenceValue = realIcon;
        slotSo.FindProperty("_amountText").objectReferenceValue = amtText;
        slotSo.FindProperty("_btn").objectReferenceValue = slotBtn;
        slotSo.FindProperty("_equipMark").objectReferenceValue = eqMark;
        slotSo.ApplyModifiedProperties();

        // Main Script Bind
        TownInventoryUI mainUI = root.AddComponent<TownInventoryUI>();
        SerializedObject mainSo = new SerializedObject(mainUI);
        mainSo.FindProperty("_gridContent").objectReferenceValue = sContent.transform;
        mainSo.FindProperty("_slotPrefab").objectReferenceValue = slotUI;
        mainSo.FindProperty("_detailsUI").objectReferenceValue = detailsUI;
        mainSo.FindProperty("_sortDropdown").objectReferenceValue = dropdown;
        
        SerializedProperty catProp = mainSo.FindProperty("_categoryButtons");
        catProp.arraySize = catBtns.Count;
        for(int i=0; i<catBtns.Count; i++) catProp.GetArrayElementAtIndex(i).objectReferenceValue = catBtns[i];
        
        mainSo.ApplyModifiedProperties();

        detailsObj.SetActive(false); // Hide details default

        Selection.activeGameObject = root;
        Debug.Log("Created Town Inventory");
    }

    private GameObject CreateChild(GameObject parent, string name, bool stretch = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        if (stretch)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        return go;
    }

    private TextMeshProUGUI CreateText(GameObject go, string content, int fontSize = 20, Color? color = null)
    {
        TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = content;
        txt.color = color ?? Color.white;
        txt.fontSize = fontSize;
        txt.alignment = TextAlignmentOptions.Center;
        
        // Stretch text rect defaults if no RectTransform yet
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        // Ensure it fills its space usually
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        
        return txt;
    }
    
    // Creates a new child with an Image component
    private Image CreateImage(GameObject parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        
        return go.GetComponent<Image>();
    }
}

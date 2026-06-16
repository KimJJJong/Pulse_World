using System;
using System.Collections.Generic;
using System.IO;
using Client.Content.Item;
using Client.Data;
using GameShared.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomeEquipPopupUI : MonoBehaviour
{
    private static readonly Color ResourceParchmentText = new Color(0.08f, 0.055f, 0.03f, 1f);
    private static readonly Color ResourceParchmentMutedText = new Color(0.24f, 0.16f, 0.08f, 1f);
    private static readonly Color ResourceButtonText = new Color(0.98f, 0.90f, 0.66f, 1f);
    private static readonly Color ResourceSkillCardColor = new Color(0.46f, 0.31f, 0.14f, 0.18f);
    private static readonly Color ResourceSkillCardLineColor = new Color(0.38f, 0.23f, 0.09f, 0.36f);
    private static readonly Color ResourceGridCellColor = new Color(0.44f, 0.30f, 0.15f, 0.20f);
    private static readonly Color ResourceGridTargetColor = new Color(0.86f, 0.54f, 0.18f, 0.92f);
    private static readonly Color ResourceGridCasterColor = new Color(0.07f, 0.42f, 0.40f, 0.96f);
    private static readonly Color ResourceTimelineUseColor = new Color(0.10f, 0.45f, 0.42f, 0.80f);
    private static readonly Color ResourceTimelineEffectColor = new Color(0.88f, 0.58f, 0.18f, 0.86f);
    private static readonly Color ResourceTimelineLockColor = new Color(0.73f, 0.18f, 0.15f, 0.78f);
    private static readonly Color ResourceScrollViewportColor = new Color(0.42f, 0.28f, 0.12f, 0.07f);
    private static readonly Color ResourceScrollRailColor = new Color(0.30f, 0.18f, 0.08f, 0.30f);
    private static readonly Color ResourceScrollHandleColor = new Color(0.10f, 0.42f, 0.40f, 0.82f);
    private static readonly Color ResourceScrollFadeColor = new Color(0.20f, 0.11f, 0.04f, 0.13f);
    private static readonly Color ResourceScrollCueColor = new Color(0.12f, 0.32f, 0.30f, 0.72f);
    private const string EquipmentDetailResourceRoot = "UI/UI_EquimentDetail/";
    private const string DetailPanelResourcePath = EquipmentDetailResourceRoot + "Panel";
    private const string EquipmentPaperResourcePath = EquipmentDetailResourceRoot + "Equipments_Panel_Paper";
    private const string DetailIconFrameResourcePath = EquipmentDetailResourceRoot + "Equipment_Frame";
    private const string SelectButtonResourcePath = EquipmentDetailResourceRoot + "Select_Button";
    private const string BackButtonResourcePath = "UI/BackButton";
    private const string NewSkillResourceRoot = "Data/NewSkills/";
    private const float ResourceCardWidth = 96f;
    private const float ResourceCardHeight = 102f;
    private const float ResourceCardGapX = 8f;
    private const float ResourceCardGapY = 10f;
    private const int ResourceCardColumns = 3;
    private const int TicksPerBeat = 480;
    private const float DetailScrollLeftX = 62f;
    private const float DetailScrollTopY = 262f;
    private const float DetailScrollWidth = 334f;
    private const float DetailScrollBottomY = 530f;
    private const float DetailTextWidth = 310f;
    private const float DetailFooterTopY = 536f;

    [SerializeField] private Transform _content;
    [SerializeField] private GameObject _itemPrefab; // Should have HomeEquipPopupItemUI component
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private Button _closeBtn;
    [SerializeField] private bool _useHomeDetailResourceLayout;
    [SerializeField] private bool _useManualObjectLayout;

    private RectTransform _contentRect;
    private Text _titleLegacy;
    private RectTransform _browserRoot;
    private RectTransform _leftPanel;
    private RectTransform _leftHeaderRoot;
    private RectTransform _leftBodyRoot;
    private RectTransform _listViewport;
    private RectTransform _listContent;
    private Text _ownedItemsTitle;
    private Text _listEmptyText;
    private Text _slotLabel;
    private Text _itemCountLabel;
    private RectTransform _rightPanel;
    private RectTransform _rightHeaderRoot;
    private RectTransform _rightBodyRoot;
    private RectTransform _rightBodyScrollContent;
    private RectTransform _rightFooterRoot;
    private RectTransform _detailRoot;
    private Image _detailIcon;
    private Image _detailIconFrame;
    private Text _detailName;
    private Text _detailMeta;
    private Text _detailStats;
    private Text _detailDescription;
    private Text _detailStatus;
    private RectTransform _detailRhythmInfoRoot;
    private Button _actionButton;
    private Text _actionButtonText;
    private TMP_FontAsset _koreanFont;
    private static Sprite _defaultUiSprite;
    private static Sprite _detailPanelSprite;
    private static Sprite _equipmentPaperSprite;
    private static Sprite _detailIconFrameSprite;
    private static Sprite _selectButtonSprite;
    private static Sprite _backButtonSprite;
    private static Font _koreanLegacyFont;
    private static readonly Dictionary<string, NewSkillSO> SkillDefinitionCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<HomeEquipPopupItemUI> _spawnedItems = new();
    private EquipmentSlot _currentSlot = EquipmentSlot.None;
    private long _selectedInstanceId = -1;
    private bool _uiBuilt;
    private bool _isShowing;
    private GameObject _sectionRootToRestore;

    private void Awake()
    {
        CacheSceneReferences();
        BuildLayout();
        HookInventoryEvents();
        if (!_isShowing)
            gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        HookInventoryEvents();
        if (_uiBuilt)
            RefreshList(preserveSelection: true);
    }

    private void OnDisable()
    {
        UnhookInventoryEvents();
    }

    public void Show(EquipmentSlot slot)
    {
        Show(slot, null);
    }

    public void Show(EquipmentSlot slot, GameObject sectionRootToRestore)
    {
        _isShowing = true;
        _sectionRootToRestore = sectionRootToRestore;
        _currentSlot = slot;
        _selectedInstanceId = FindEquippedInstanceId(slot);
        var titleText = GetSelectionTitle(slot);
        CacheSceneReferences();
        BuildLayout();
        if (_title != null)
            _title.text = titleText;
        if (_titleLegacy != null)
            _titleLegacy.text = titleText;

        gameObject.SetActive(true);
        if (_sectionRootToRestore != null && _sectionRootToRestore.activeSelf)
            _sectionRootToRestore.SetActive(false);

        EnsurePopupOrder();
        RefreshList(preserveSelection: false);
    }

    public void Hide()
    {
        _isShowing = false;
        gameObject.SetActive(false);
        if (_sectionRootToRestore != null)
        {
            _sectionRootToRestore.SetActive(true);
            _sectionRootToRestore = null;
        }
    }

    private void CacheSceneReferences()
    {
        if (_content == null)
        {
            var contentRect = FindRect("Content");
            if (contentRect != null)
                _content = contentRect.transform;
        }

        if (_browserRoot == null)
            _browserRoot = FindRect("EquipmentBrowserRoot");

        if (_title == null)
            _title = FindTextMeshPro("Title");
        if (_titleLegacy == null)
            _titleLegacy = FindText("TitleLegacyText", "TitleLabel", "LegacyTitle");

        if (_closeBtn == null)
            _closeBtn = FindButton("CloseBtn");

        if (_itemPrefab == null)
            _itemPrefab = FindGameObject("Prefab_PopupItem");

        if (_content is RectTransform rect)
            _contentRect = rect;

        EnsureActive(_contentRect != null ? _contentRect.gameObject : null);
        EnsureActive(_title != null ? _title.gameObject : null);
        EnsureActive(_closeBtn != null ? _closeBtn.gameObject : null);

        if (_title != null)
            _title.text = "장비 선택";
        ApplyKoreanFont(_title);

        if (_titleLegacy != null)
            _titleLegacy.gameObject.SetActive(false);

        if (_closeBtn != null)
        {
            _closeBtn.onClick.RemoveAllListeners();
            _closeBtn.onClick.AddListener(Hide);
        }
    }

    private void BuildLayout()
    {
        if (_uiBuilt)
            return;

        if (_contentRect == null && _content != null)
            _contentRect = _content as RectTransform;

        if (_contentRect == null)
        {
            Debug.LogWarning("[HomeEquipPopupUI] Content RectTransform not found.");
            return;
        }

        if (_useHomeDetailResourceLayout && TryBindExistingResourceLayout())
        {
            _uiBuilt = true;
            if (_useManualObjectLayout)
                ApplyManualObjectBindings();
            else
                ApplyHomeDetailResourceLayout();
            NormalizePopupTextColors();
            EnsurePopupOrder();
            return;
        }

        _contentRect.gameObject.SetActive(true);
        _contentRect.sizeDelta = new Vector2(1120f, 580f);
        var contentImage = _contentRect.GetComponent<Image>();
        if (contentImage != null)
        {
            contentImage.color = new Color(0f, 0f, 0f, 0f);
            contentImage.raycastTarget = false;
        }

        _browserRoot = FindRect("EquipmentBrowserRoot") ?? CreatePanel("EquipmentBrowserRoot", _contentRect, new Vector2(1120f, 580f), new Vector2(0f, -64f), new Color(0f, 0f, 0f, 0f));
        _browserRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _browserRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _browserRoot.pivot = new Vector2(0.5f, 0.5f);
        _browserRoot.sizeDelta = new Vector2(1120f, 580f);
        _browserRoot.anchoredPosition = new Vector2(0f, -64f);

        if (_title != null)
        {
            var titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(360f, 32f);
            titleRect.anchoredPosition = new Vector2(0f, -6f);
            _title.alignment = TextAlignmentOptions.Center;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 16f;
            _title.fontSizeMax = 20f;
            _title.fontSize = 20f;
            _title.color = new Color(1f, 0.84f, 0f, 1f);
            ApplyKoreanFont(_title);
        }

        if (_closeBtn != null)
        {
            var closeRect = _closeBtn.GetComponent<RectTransform>();
            if (closeRect != null)
            {
                closeRect.anchorMin = new Vector2(1f, 1f);
                closeRect.anchorMax = new Vector2(1f, 1f);
                closeRect.pivot = new Vector2(1f, 1f);
                closeRect.sizeDelta = new Vector2(40f, 40f);
                closeRect.anchoredPosition = new Vector2(-12f, -10f);
            }
        }

        _leftPanel = FindRect("OwnedItemsPanel") ?? CreatePanel("OwnedItemsPanel", _browserRoot, new Vector2(274f, 414f), new Vector2(-308f, -60f), new Color(0.08f, 0.08f, 0.12f, 0.90f));
        _leftPanel.anchorMin = new Vector2(0.5f, 0.5f);
        _leftPanel.anchorMax = new Vector2(0.5f, 0.5f);
        _leftPanel.pivot = new Vector2(0.5f, 0.5f);
        _leftPanel.sizeDelta = new Vector2(274f, 414f);
        _leftPanel.anchoredPosition = new Vector2(-308f, -60f);

        _leftHeaderRoot = FindRect("OwnedItemsHeader") ?? CreateEmptyRect("OwnedItemsHeader", _leftPanel);
        _leftHeaderRoot.anchorMin = new Vector2(0f, 1f);
        _leftHeaderRoot.anchorMax = new Vector2(1f, 1f);
        _leftHeaderRoot.pivot = new Vector2(0.5f, 1f);
        _leftHeaderRoot.sizeDelta = new Vector2(0f, 66f);
        _leftHeaderRoot.anchoredPosition = new Vector2(0f, -6f);

        _leftBodyRoot = FindRect("OwnedItemsBody") ?? CreateEmptyRect("OwnedItemsBody", _leftPanel);
        _leftBodyRoot.anchorMin = new Vector2(0f, 0f);
        _leftBodyRoot.anchorMax = new Vector2(1f, 1f);
        _leftBodyRoot.pivot = new Vector2(0.5f, 1f);
        _leftBodyRoot.offsetMin = new Vector2(10f, 12f);
        _leftBodyRoot.offsetMax = new Vector2(-10f, -72f);

        _ownedItemsTitle = CreateText("OwnedItemsTitle", _leftHeaderRoot, "보유 장비", 18, TextAnchor.MiddleLeft, new Vector2(10f, -8f), new Vector2(160f, 22f));
        _slotLabel = CreateText("SelectedSlotLabel", _leftHeaderRoot, "부위: -", 13, TextAnchor.MiddleLeft, new Vector2(10f, -32f), new Vector2(126f, 18f));
        _itemCountLabel = CreateText("ItemCountLabel", _leftHeaderRoot, "0개 보유", 13, TextAnchor.MiddleRight, new Vector2(138f, -32f), new Vector2(106f, 18f));

        _listViewport = FindRect("ItemListViewport") ?? CreatePanel("ItemListViewport", _leftBodyRoot, new Vector2(248f, 330f), Vector2.zero, new Color(0.05f, 0.05f, 0.08f, 0.92f));
        _listViewport.anchorMin = new Vector2(0f, 1f);
        _listViewport.anchorMax = new Vector2(0f, 1f);
        _listViewport.pivot = new Vector2(0f, 1f);
        _listViewport.anchoredPosition = new Vector2(0f, -2f);
        _listViewport.sizeDelta = new Vector2(248f, 330f);

        var scrollImage = _listViewport.GetComponent<Image>();
        if (scrollImage == null)
            scrollImage = _listViewport.gameObject.AddComponent<Image>();
        scrollImage.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

        var mask = _listViewport.GetComponent<Mask>();
        if (mask != null)
            RemoveComponent(mask);

        if (_listViewport.GetComponent<RectMask2D>() == null)
            _listViewport.gameObject.AddComponent<RectMask2D>();

        var scrollRect = _listViewport.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = _listViewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        _listContent = FindRect("ItemListContent") ?? CreateEmptyRect("ItemListContent", _listViewport);
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.anchoredPosition = Vector2.zero;
        _listContent.sizeDelta = new Vector2(0f, 0f);
        _listContent.offsetMin = new Vector2(8f, 8f);
        _listContent.offsetMax = new Vector2(-8f, -8f);

        var layout = _listContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 6f;

        var fitter = _listContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = _listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = _listViewport;
        scrollRect.content = _listContent;

        _listEmptyText = CreateText("ItemListEmptyText", _listViewport, "해당 부위의 장비가 없습니다.", 15, TextAnchor.MiddleCenter, new Vector2(8f, -114f), new Vector2(220f, 36f));
        _listEmptyText.color = new Color(0.84f, 0.84f, 0.88f, 0.8f);
        var emptyRect = _listEmptyText.rectTransform;
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(10f, 40f);
        emptyRect.offsetMax = new Vector2(-10f, -40f);
        _listEmptyText.gameObject.SetActive(false);

        _rightPanel = FindRect("DetailPanel") ?? CreatePanel("DetailPanel", _browserRoot, new Vector2(332f, 414f), new Vector2(308f, -60f), new Color(0.08f, 0.08f, 0.12f, 0.92f));
        _rightPanel.anchorMin = new Vector2(0.5f, 0.5f);
        _rightPanel.anchorMax = new Vector2(0.5f, 0.5f);
        _rightPanel.pivot = new Vector2(0.5f, 0.5f);
        _rightPanel.sizeDelta = new Vector2(332f, 414f);
        _rightPanel.anchoredPosition = new Vector2(308f, -60f);

        _detailRoot = _rightPanel;

        _rightHeaderRoot = FindRect("DetailHeader") ?? CreateEmptyRect("DetailHeader", _detailRoot);
        _rightHeaderRoot.anchorMin = new Vector2(0f, 1f);
        _rightHeaderRoot.anchorMax = new Vector2(1f, 1f);
        _rightHeaderRoot.pivot = new Vector2(0.5f, 1f);
        _rightHeaderRoot.sizeDelta = new Vector2(0f, 84f);
        _rightHeaderRoot.anchoredPosition = new Vector2(0f, -6f);

        _rightBodyRoot = FindRect("DetailBody") ?? CreateEmptyRect("DetailBody", _detailRoot);
        _rightBodyRoot.anchorMin = new Vector2(0f, 0f);
        _rightBodyRoot.anchorMax = new Vector2(1f, 1f);
        _rightBodyRoot.pivot = new Vector2(0.5f, 1f);
        _rightBodyRoot.offsetMin = new Vector2(12f, 66f);
        _rightBodyRoot.offsetMax = new Vector2(-12f, -144f);
        ConfigureScrollViewport(_rightBodyRoot, "DetailBodyContent", new Vector2(300f, 390f), ref _rightBodyScrollContent);

        _rightFooterRoot = FindRect("DetailFooter") ?? CreateEmptyRect("DetailFooter", _detailRoot);
        _rightFooterRoot.anchorMin = new Vector2(0f, 0f);
        _rightFooterRoot.anchorMax = new Vector2(1f, 0f);
        _rightFooterRoot.pivot = new Vector2(0.5f, 0f);
        _rightFooterRoot.sizeDelta = new Vector2(0f, 64f);
        _rightFooterRoot.anchoredPosition = new Vector2(0f, 10f);

        CreateText("DetailTitle", _rightHeaderRoot, "상세 정보", 18, TextAnchor.MiddleLeft, new Vector2(10f, -8f), new Vector2(160f, 22f));

        var iconPanel = FindRect("DetailIconPanel") ?? CreatePanel("DetailIconPanel", _rightHeaderRoot, new Vector2(80f, 80f), new Vector2(10f, -30f), new Color(0.16f, 0.16f, 0.22f, 0.96f));
        iconPanel.anchorMin = new Vector2(0f, 1f);
        iconPanel.anchorMax = new Vector2(0f, 1f);
        iconPanel.pivot = new Vector2(0f, 1f);
        iconPanel.anchoredPosition = new Vector2(10f, -30f);
        SetIconPanelTransparent(iconPanel);

        var iconObj = FindGameObject("DetailIcon") ?? CreateImage("DetailIcon", iconPanel, new Vector2(72f, 72f), Vector2.zero, new Color(1f, 1f, 1f, 1f));
        _detailIcon = iconObj.GetComponent<Image>();
        var iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(64f, 64f);
        EnsureDetailIconFrame();

        _detailName = CreateText("DetailName", _rightHeaderRoot, "선택된 장비 없음", 16, TextAnchor.MiddleLeft, new Vector2(96f, -28f), new Vector2(224f, 22f));
        _detailMeta = CreateText("DetailMeta", _rightHeaderRoot, "-", 12, TextAnchor.MiddleLeft, new Vector2(96f, -50f), new Vector2(224f, 16f));
        _detailStats = CreateText("DetailStats", _rightBodyScrollContent, "-", 14, TextAnchor.UpperLeft, new Vector2(4f, -8f), new Vector2(300f, 92f));
        _detailDescription = CreateText("DetailDescription", _rightBodyScrollContent, "장비를 선택하면 상세 정보가 표시됩니다.", 13, TextAnchor.UpperLeft, new Vector2(4f, -104f), new Vector2(300f, 180f));
        _detailStatus = CreateText("DetailStatus", _rightFooterRoot, "대기 중", 13, TextAnchor.MiddleLeft, new Vector2(4f, 22f), new Vector2(192f, 18f));

        _actionButton = FindButton("DetailActionButton") ?? CreateButton(_rightFooterRoot, "DetailActionButton", "장착", new Vector2(198f, 16f), new Vector2(116f, 30f));
        SetButtonLabel(_actionButton, "장착");
        _actionButton.onClick.RemoveAllListeners();
        _actionButton.onClick.AddListener(OnActionButtonClicked);

        _uiBuilt = true;
        if (_useManualObjectLayout && _useHomeDetailResourceLayout)
            ApplyManualObjectBindings();
        else
            ApplyHomeDetailResourceLayout();
        NormalizePopupTextColors();
        EnsurePopupOrder();
    }

    private void ApplyManualObjectBindings()
    {
        ConfigureInputBlockers();
        ConfigureManualBackButton();
        ConfigureSceneScrollRect(_listViewport, _listContent);
        ConfigureManualDetailPanelLayout();

        ConfigureEquipmentGrid();
        BindExistingDetailIconFrame();
        DisableStaleDetailButtons();

        if (_title != null)
            ApplyKoreanFont(_title);

        if (_titleLegacy != null)
            _titleLegacy.gameObject.SetActive(false);

        if (_listEmptyText != null)
            _listEmptyText.gameObject.SetActive(false);
    }

    private void ConfigureManualDetailPanelLayout()
    {
        if (!_useHomeDetailResourceLayout)
        {
            if (_rightBodyRoot != null && _rightBodyScrollContent != null)
                ConfigureSceneScrollRect(_rightBodyRoot, _rightBodyScrollContent);
            return;
        }

        if (_rightHeaderRoot != null)
            SetTopLeftRect(_rightHeaderRoot, new Rect(0f, 0f, 451f, 278f));

        if (_rightBodyRoot != null)
        {
            SetTopLeftRect(_rightBodyRoot, new Rect(DetailScrollLeftX, DetailScrollTopY, DetailScrollWidth, DetailScrollBottomY - DetailScrollTopY));
            ConfigureScrollViewport(_rightBodyRoot, "DetailBodyContent", new Vector2(DetailScrollWidth, 600f), ref _rightBodyScrollContent);
        }

        if (_rightBodyScrollContent != null)
        {
            _rightBodyScrollContent.anchorMin = new Vector2(0f, 1f);
            _rightBodyScrollContent.anchorMax = new Vector2(0f, 1f);
            _rightBodyScrollContent.pivot = new Vector2(0f, 1f);
            _rightBodyScrollContent.anchoredPosition = Vector2.zero;
            _rightBodyScrollContent.sizeDelta = new Vector2(DetailScrollWidth, 600f);
        }

        if (_rightFooterRoot != null)
            SetTopLeftRect(_rightFooterRoot, new Rect(0f, DetailFooterTopY, 451f, 112f));

        if (_detailStats != null)
            SetTextRect(_detailStats, new Rect(0f, 8f, DetailTextWidth, 42f), 14, TextAnchor.UpperLeft, ResourceParchmentText);

        if (_detailDescription != null)
            SetTextRect(_detailDescription, new Rect(0f, 52f, DetailTextWidth, 36f), 12, TextAnchor.UpperLeft, ResourceParchmentMutedText);

        SetTextRect(_detailStatus, new Rect(74f, 2f, 304f, 24f), 12, TextAnchor.MiddleCenter, ResourceParchmentMutedText);

        if (_actionButton != null)
        {
            var actionRect = _actionButton.GetComponent<RectTransform>();
            if (actionRect != null)
                SetTopLeftRect(actionRect, new Rect(124f, 34f, 202f, 66f));

            var buttonText = _actionButton.GetComponentInChildren<Text>(true);
            if (buttonText != null)
                SetTextRect(buttonText, new Rect(0f, 0f, 202f, 66f), 18, TextAnchor.MiddleCenter, ResourceButtonText);
        }
    }

    private void ApplyHomeDetailResourceLayout()
    {
        if (!_useHomeDetailResourceLayout)
            return;

        SetCenteredRect(_contentRect, new Vector2(1280f, 720f), Vector2.zero);
        SetCenteredRect(_browserRoot, new Vector2(1280f, 720f), Vector2.zero);
        SetImageColor(_contentRect, new Color(0f, 0f, 0f, 0f));
        SetImageColor(_browserRoot, new Color(0f, 0f, 0f, 0f));
        SetImageColor(_listViewport, new Color(0f, 0f, 0f, 0f));
        ConfigureInputBlockers();

        if (_title != null)
        {
            var titleRect = _title.rectTransform;
            SetTopLeftRect(titleRect, new Rect(88f, 84f, 330f, 42f));
            _title.alignment = TextAlignmentOptions.Center;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 18f;
            _title.fontSizeMax = 28f;
            _title.fontStyle = FontStyles.Bold;
            _title.color = ResourceParchmentText;
        }

        DisableLegacyResourceChrome();
        ConfigureBackButton();

        SetTopLeftRect(_leftPanel, new Rect(28f, 24f, 451f, 672f));
        SetTopLeftRect(_rightPanel, new Rect(801f, 24f, 451f, 672f));
        ApplyResourceSprite(_leftPanel, DetailPanelSprite, true);
        ApplyResourceSprite(_rightPanel, DetailPanelSprite, true);

        var equipmentPaper = EnsureDecorImage("EquipmentPaper", _leftPanel, EquipmentPaperSprite);
        SetTopLeftRect(equipmentPaper, new Rect(44f, 146f, 363f, 494f));
        if (equipmentPaper != null)
            equipmentPaper.SetAsFirstSibling();

        if (_leftHeaderRoot != null)
            SetTopLeftRect(_leftHeaderRoot, new Rect(54f, 86f, 344f, 58f));

        if (_leftBodyRoot != null)
            SetTopLeftRect(_leftBodyRoot, new Rect(44f, 146f, 363f, 494f));

        if (_ownedItemsTitle != null)
            _ownedItemsTitle.gameObject.SetActive(false);
        if (_slotLabel != null)
            _slotLabel.gameObject.SetActive(false);
        if (_itemCountLabel != null)
        {
            _itemCountLabel.gameObject.SetActive(true);
            SetTextRect(_itemCountLabel, new Rect(224f, 18f, 108f, 22f), 13, TextAnchor.MiddleRight, ResourceParchmentMutedText);
        }

        if (_listViewport != null)
        {
            SetTopLeftRect(_listViewport, new Rect(30f, 42f, 304f, 430f));

            var scrollImage = _listViewport.GetComponent<Image>();
            if (scrollImage != null)
            {
                ApplyDefaultSprite(scrollImage);
                scrollImage.color = new Color(1f, 1f, 1f, 0.01f);
                scrollImage.raycastTarget = true;
            }
        }

        if (_listContent != null)
        {
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(0f, 1f);
            _listContent.pivot = new Vector2(0f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = new Vector2(ResourceCardColumns * ResourceCardWidth + (ResourceCardColumns - 1) * ResourceCardGapX, 0f);

            ConfigureEquipmentGrid();
        }

        if (_rightHeaderRoot != null)
            SetTopLeftRect(_rightHeaderRoot, new Rect(0f, 0f, 451f, 278f));

        if (_rightBodyRoot != null)
        {
            SetTopLeftRect(_rightBodyRoot, new Rect(DetailScrollLeftX, DetailScrollTopY, DetailScrollWidth, DetailScrollBottomY - DetailScrollTopY));
            ConfigureScrollViewport(_rightBodyRoot, "DetailBodyContent", new Vector2(DetailScrollWidth, 600f), ref _rightBodyScrollContent);
        }

        if (_rightBodyScrollContent != null)
        {
            _rightBodyScrollContent.anchorMin = new Vector2(0f, 1f);
            _rightBodyScrollContent.anchorMax = new Vector2(0f, 1f);
            _rightBodyScrollContent.pivot = new Vector2(0f, 1f);
            _rightBodyScrollContent.anchoredPosition = Vector2.zero;
            _rightBodyScrollContent.sizeDelta = new Vector2(DetailScrollWidth, 600f);
        }

        if (_rightFooterRoot != null)
            SetTopLeftRect(_rightFooterRoot, new Rect(0f, DetailFooterTopY, 451f, 112f));

        var detailTitle = FindText("DetailTitle");
        if (detailTitle != null)
            detailTitle.text = "Detail";
        SetTextRect(detailTitle, new Rect(0f, 58f, 451f, 42f), 28, TextAnchor.MiddleCenter, ResourceParchmentText);

        var iconPanel = FindRect("DetailIconPanel");
        if (iconPanel != null)
        {
            SetTopLeftRect(iconPanel, new Rect(70f, 156f, 112f, 112f));
            SetIconPanelTransparent(iconPanel);
        }

        EnsureDetailIconFrame();

        if (_detailIconFrame != null)
            SetCenteredRect(_detailIconFrame.rectTransform, new Vector2(104f, 111f), Vector2.zero);

        if (_detailIcon != null)
            SetCenteredRect(_detailIcon.rectTransform, new Vector2(72f, 72f), Vector2.zero);

        SetTextRect(_detailName, new Rect(204f, 160f, 192f, 38f), 23, TextAnchor.MiddleCenter, ResourceParchmentText);
        SetTextRect(_detailMeta, new Rect(198f, 206f, 206f, 58f), 13, TextAnchor.UpperCenter, ResourceParchmentMutedText);

        if (_detailStats != null)
            SetTextRect(_detailStats, new Rect(0f, 8f, DetailTextWidth, 42f), 14, TextAnchor.UpperLeft, ResourceParchmentText);

        if (_detailDescription != null)
            SetTextRect(_detailDescription, new Rect(0f, 52f, DetailTextWidth, 36f), 12, TextAnchor.UpperLeft, ResourceParchmentMutedText);

        SetTextRect(_detailStatus, new Rect(74f, 2f, 304f, 24f), 12, TextAnchor.MiddleCenter, ResourceParchmentMutedText);

        if (_actionButton != null)
        {
            var actionRect = _actionButton.GetComponent<RectTransform>();
            if (actionRect != null)
                SetTopLeftRect(actionRect, new Rect(124f, 34f, 202f, 66f));

            var buttonImage = _actionButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                ApplyResourceSprite(buttonImage, SelectButtonSprite, true);
            }

            var buttonText = _actionButton.GetComponentInChildren<Text>(true);
            if (buttonText != null)
                SetTextRect(buttonText, new Rect(0f, 0f, 202f, 66f), 18, TextAnchor.MiddleCenter, ResourceButtonText);
        }

        if (_listEmptyText != null)
            SetTextRect(_listEmptyText, new Rect(0f, 174f, 304f, 64f), 15, TextAnchor.MiddleCenter, ResourceParchmentMutedText);

        DisableStaleDetailButtons();
    }

    private bool TryBindExistingResourceLayout()
    {
        _contentRect.gameObject.SetActive(true);

        _browserRoot = FindRect("EquipmentBrowserRoot");
        _leftPanel = FindRect("OwnedItemsPanel");
        _leftHeaderRoot = FindRect("OwnedItemsHeader");
        _leftBodyRoot = FindRect("OwnedItemsBody");
        _listViewport = FindRect("ItemListViewport");
        _listContent = FindRect("ItemListContent");
        _rightPanel = FindRect("DetailPanel");
        _detailRoot = _rightPanel;
        _rightHeaderRoot = FindRect("DetailHeader");
        _rightBodyRoot = FindRect("DetailBody");
        _rightBodyScrollContent = FindRect("DetailBodyContent");
        _rightFooterRoot = FindRect("DetailFooter");

        if (_browserRoot == null || _leftPanel == null || _listViewport == null || _listContent == null ||
            _rightPanel == null || _rightHeaderRoot == null || _rightBodyRoot == null || _rightFooterRoot == null)
        {
            return false;
        }

        _ownedItemsTitle = FindText("OwnedItemsTitle");
        _slotLabel = FindText("SelectedSlotLabel");
        _itemCountLabel = FindText("ItemCountLabel");
        _listEmptyText = FindText("ItemListEmptyText");
        _detailName = FindText("DetailName");
        _detailMeta = FindText("DetailMeta");
        _detailStats = FindText("DetailStats");
        _detailDescription = FindText("DetailDescription");
        _detailStatus = FindText("DetailStatus");

        var detailIconObject = FindGameObject("DetailIcon");
        _detailIcon = detailIconObject != null ? detailIconObject.GetComponent<Image>() : null;
        var detailIconFrameObject = FindGameObject("DetailIconFrame");
        _detailIconFrame = detailIconFrameObject != null ? detailIconFrameObject.GetComponent<Image>() : null;
        _actionButton = FindButton("DetailActionButton");
        _actionButtonText = _actionButton != null ? _actionButton.GetComponentInChildren<Text>(true) : null;

        if (_itemPrefab == null)
            _itemPrefab = FindGameObject("Prefab_PopupItem");

        if (_itemPrefab == null || _actionButton == null)
            return false;

        ConfigureInputBlockers();
        EnsureDetailIconFrame();
        ConfigureSceneScrollRect(_listViewport, _listContent);
        if (_rightBodyScrollContent != null)
            ConfigureSceneScrollRect(_rightBodyRoot, _rightBodyScrollContent);

        ConfigureEquipmentGrid();
        DisableStaleDetailButtons();

        _closeBtn?.onClick.RemoveAllListeners();
        _closeBtn?.onClick.AddListener(Hide);

        _actionButton.onClick.RemoveAllListeners();
        _actionButton.onClick.AddListener(OnActionButtonClicked);
        SetButtonLabel(_actionButton, "장착");

        if (_titleLegacy != null)
            _titleLegacy.gameObject.SetActive(false);

        if (_listEmptyText != null)
            _listEmptyText.gameObject.SetActive(false);

        return true;
    }

    private static void ConfigureSceneScrollRect(RectTransform viewport, RectTransform content)
    {
        if (viewport == null || content == null)
            return;

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        var scrollRect = viewport.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = viewport.gameObject.AddComponent<ScrollRect>();

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 22f;
        scrollRect.viewport = viewport;
        scrollRect.content = content;
    }

    private void ConfigureInputBlockers()
    {
        var dimObject = FindGameObject("DimOverlay") ?? FindGameObject("SceneDim");
        var dim = dimObject != null ? dimObject.GetComponent<Image>() : null;
        if (dim != null)
            dim.raycastTarget = dim.color.a > 0.01f;

        var blockers = new[] { _contentRect, _browserRoot };
        foreach (var blocker in blockers)
        {
            if (blocker == null)
                continue;

            var image = blocker.GetComponent<Image>();
            if (image == null)
                image = blocker.gameObject.AddComponent<Image>();

            ApplyDefaultSprite(image);
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
        }
    }

    private void EnsureDetailIconFrame()
    {
        if (_useManualObjectLayout)
        {
            BindExistingDetailIconFrame();
            return;
        }

        if (_detailIcon == null)
        {
            var detailIconObject = FindGameObject("DetailIcon");
            _detailIcon = detailIconObject != null ? detailIconObject.GetComponent<Image>() : null;
        }

        var iconPanel = FindRect("DetailIconPanel");
        if (iconPanel == null && _detailIcon != null)
            iconPanel = _detailIcon.transform.parent as RectTransform;

        if (iconPanel == null)
            return;

        SetIconPanelTransparent(iconPanel);

        if (_detailIconFrame == null)
        {
            var frameObject = FindGameObject("DetailIconFrame");
            _detailIconFrame = frameObject != null ? frameObject.GetComponent<Image>() : null;
        }

        if (_detailIconFrame == null)
        {
            var frameGo = new GameObject("DetailIconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.transform.SetParent(iconPanel, false);
            _detailIconFrame = frameGo.GetComponent<Image>();
        }
        else if (_detailIconFrame.transform.parent != iconPanel)
        {
            _detailIconFrame.transform.SetParent(iconPanel, false);
        }

        _detailIconFrame.sprite = DetailIconFrameSprite;
        _detailIconFrame.color = Color.white;
        _detailIconFrame.preserveAspect = false;
        _detailIconFrame.raycastTarget = false;
        SetCenteredRect(_detailIconFrame.rectTransform, new Vector2(86f, 86f), Vector2.zero);

        if (_detailIcon != null)
        {
            _detailIcon.preserveAspect = true;
            _detailIcon.raycastTarget = false;
            SetCenteredRect(_detailIcon.rectTransform, new Vector2(64f, 64f), Vector2.zero);
            _detailIcon.transform.SetAsLastSibling();
        }

        _detailIconFrame.transform.SetAsFirstSibling();
        if (_detailIcon != null)
            _detailIcon.transform.SetAsLastSibling();
    }

    private void BindExistingDetailIconFrame()
    {
        if (_detailIcon == null)
        {
            var detailIconObject = FindGameObject("DetailIcon");
            _detailIcon = detailIconObject != null ? detailIconObject.GetComponent<Image>() : null;
        }

        if (_detailIconFrame == null)
        {
            var frameObject = FindGameObject("DetailIconFrame");
            _detailIconFrame = frameObject != null ? frameObject.GetComponent<Image>() : null;
        }

        if (_detailIcon != null)
        {
            _detailIcon.preserveAspect = true;
            _detailIcon.raycastTarget = false;
        }

        if (_detailIconFrame != null)
        {
            _detailIconFrame.sprite = DetailIconFrameSprite;
            _detailIconFrame.color = Color.white;
            _detailIconFrame.preserveAspect = false;
            _detailIconFrame.raycastTarget = false;
        }
    }

    private static void SetIconPanelTransparent(RectTransform iconPanel)
    {
        if (iconPanel == null)
            return;

        var image = iconPanel.GetComponent<Image>();
        if (image == null)
            return;

        ApplyDefaultSprite(image);
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
    }

    private void ConfigureEquipmentGrid()
    {
        if (_listContent == null)
            return;

        var vertical = _listContent.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
            RemoveComponent(vertical);

        var grid = _listContent.GetComponent<GridLayoutGroup>();
        if (grid != null)
            RemoveComponent(grid);

        var fitter = _listContent.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            RemoveComponent(fitter);
    }

    private void DisableStaleDetailButtons()
    {
        var buttons = transform.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button == null || button == _closeBtn || button == _actionButton)
                continue;

            if (button.GetComponentInParent<HomeEquipPopupItemUI>(true) != null)
                continue;

            button.onClick.RemoveAllListeners();
            button.interactable = false;
            button.enabled = false;

            if (button.targetGraphic != null)
                button.targetGraphic.raycastTarget = false;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;
        }
    }

    private void DisableLegacyResourceChrome()
    {
        var resourceChrome = FindRect("ResourceChrome");
        if (resourceChrome != null)
            resourceChrome.gameObject.SetActive(false);
    }

    private void ConfigureBackButton()
    {
        if (_closeBtn == null)
            return;

        var closeRect = _closeBtn.GetComponent<RectTransform>();
        if (closeRect != null)
            SetTopLeftRect(closeRect, new Rect(48f, 34f, 154f, 50f));

        var buttonImage = _closeBtn.GetComponent<Image>();
        if (buttonImage == null)
            buttonImage = _closeBtn.gameObject.AddComponent<Image>();
        ApplyResourceSprite(buttonImage, BackButtonSprite, true);

        _closeBtn.targetGraphic = buttonImage;
        _closeBtn.enabled = true;
        _closeBtn.interactable = true;

        var buttonText = EnsureButtonText(_closeBtn, "뒤로가기");
        if (buttonText != null)
            SetTextRect(buttonText, new Rect(0f, 0f, 154f, 50f), 15, TextAnchor.MiddleCenter, ResourceButtonText);

        var feedback = _closeBtn.GetComponent<HomeUIButtonFeedback>();
        if (feedback == null)
            feedback = _closeBtn.gameObject.AddComponent<HomeUIButtonFeedback>();
        feedback.Configure(closeRect, buttonImage);
    }

    private void ConfigureManualBackButton()
    {
        if (_closeBtn == null)
            return;

        var buttonImage = _closeBtn.GetComponent<Image>();
        if (buttonImage == null)
            buttonImage = _closeBtn.gameObject.AddComponent<Image>();

        ApplyResourceSprite(buttonImage, BackButtonSprite, true);
        _closeBtn.targetGraphic = buttonImage;
        _closeBtn.enabled = true;
        _closeBtn.interactable = true;
        _closeBtn.onClick.RemoveAllListeners();
        _closeBtn.onClick.AddListener(Hide);

        var buttonText = _closeBtn.GetComponentInChildren<Text>(true);
        if (buttonText != null)
        {
            buttonText.text = "뒤로가기";
            ApplyKoreanFont(buttonText);
            buttonText.color = ResourceButtonText;
            buttonText.raycastTarget = false;
        }

        var feedback = _closeBtn.GetComponent<HomeUIButtonFeedback>();
        if (feedback == null)
            feedback = _closeBtn.gameObject.AddComponent<HomeUIButtonFeedback>();
        feedback.Configure(_closeBtn.transform as RectTransform, buttonImage);
    }

    private Text EnsureButtonText(Button button, string label)
    {
        if (button == null)
            return null;

        var labelText = button.GetComponentInChildren<Text>(true);
        if (labelText == null)
        {
            var labelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(button.transform, false);
            labelText = labelGo.GetComponent<Text>();
        }

        labelText.gameObject.SetActive(true);
        labelText.text = label;
        labelText.raycastTarget = false;
        ApplyKoreanFont(labelText);
        return labelText;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void SetImageColor(RectTransform rect, Color color)
    {
        if (rect == null)
            return;

        var image = rect.GetComponent<Image>();
        if (image == null)
            return;

        image.color = color;
        image.raycastTarget = false;
    }

    private static void ApplyResourceSprite(RectTransform rect, Sprite sprite, bool raycastTarget)
    {
        if (rect == null)
            return;

        var image = rect.GetComponent<Image>();
        if (image == null)
            image = rect.gameObject.AddComponent<Image>();

        ApplyResourceSprite(image, sprite, raycastTarget);
    }

    private static void ApplyResourceSprite(Image image, Sprite sprite, bool raycastTarget)
    {
        if (image == null)
            return;

        if (sprite != null)
            image.sprite = sprite;
        else
            ApplyDefaultSprite(image);

        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        image.raycastTarget = raycastTarget;
    }

    private RectTransform EnsureDecorImage(string name, RectTransform parent, Sprite sprite)
    {
        if (parent == null)
            return null;

        var child = parent.Find(name);
        RectTransform rect;
        Image image;
        if (child != null)
        {
            rect = child.GetComponent<RectTransform>();
            image = child.GetComponent<Image>();
            if (image == null)
                image = child.gameObject.AddComponent<Image>();
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            rect = go.GetComponent<RectTransform>();
            image = go.GetComponent<Image>();
        }

        ApplyResourceSprite(image, sprite, false);
        return rect;
    }

    private void ConfigureScrollViewport(RectTransform viewport, string contentName, Vector2 contentSize, ref RectTransform content)
    {
        if (viewport == null)
            return;

        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
            viewportImage = viewport.gameObject.AddComponent<Image>();
        ApplyDefaultSprite(viewportImage);
        viewportImage.color = _useHomeDetailResourceLayout
            ? ResourceScrollViewportColor
            : new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        var scrollRect = viewport.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 22f;
        scrollRect.viewport = viewport;

        if (content == null)
            content = FindRect(contentName);

        if (content == null)
            content = CreateEmptyRect(contentName, viewport);
        else if (content.parent != viewport)
            content.SetParent(viewport, false);

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = contentSize;
        scrollRect.content = content;

        if (_useHomeDetailResourceLayout)
            EnsureDetailScrollAffordance(viewport, content, scrollRect);
    }

    private void EnsureDetailScrollAffordance(RectTransform viewport, RectTransform content, ScrollRect scrollRect)
    {
        if (viewport == null || scrollRect == null)
            return;

        float viewportWidth = Mathf.Max(1f, viewport.sizeDelta.x);
        float viewportHeight = Mathf.Max(1f, viewport.sizeDelta.y);

        var topFade = EnsureChildImage("DetailScrollTopFade", viewport, ResourceScrollFadeColor);
        SetTopLeftRect(topFade.rectTransform, new Rect(0f, 0f, viewportWidth - 18f, 12f));
        topFade.raycastTarget = false;

        var bottomFade = EnsureChildImage("DetailScrollBottomFade", viewport, ResourceScrollFadeColor);
        SetTopLeftRect(bottomFade.rectTransform, new Rect(0f, viewportHeight - 22f, viewportWidth - 18f, 22f));
        bottomFade.raycastTarget = false;

        RemoveChildObject(viewport, "DetailScrollCueLeft");
        RemoveChildObject(viewport, "DetailScrollCueRight");

        var rail = EnsureChildImage("DetailScrollRail", viewport, ResourceScrollRailColor);
        SetTopLeftRect(rail.rectTransform, new Rect(viewportWidth - 15f, 8f, 6f, viewportHeight - 16f));
        rail.raycastTarget = true;

        var handle = EnsureChildImage("Handle", rail.rectTransform, ResourceScrollHandleColor);
        handle.raycastTarget = true;

        var handleRect = handle.rectTransform;
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(1f, 1f);
        handleRect.offsetMax = new Vector2(-1f, -1f);
        handleRect.localEulerAngles = Vector3.zero;

        var scrollbar = rail.GetComponent<Scrollbar>();
        if (scrollbar == null)
            scrollbar = rail.gameObject.AddComponent<Scrollbar>();

        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handle;
        scrollbar.handleRect = handleRect;
        scrollbar.transition = Selectable.Transition.ColorTint;
        scrollbar.interactable = true;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollRect.verticalScrollbarSpacing = -2f;

        topFade.transform.SetAsLastSibling();
        bottomFade.transform.SetAsLastSibling();
        rail.transform.SetAsLastSibling();
    }

    private static Image EnsureChildImage(string name, RectTransform parent, Color color)
    {
        if (parent == null)
            return null;

        var child = parent.Find(name);
        Image image;
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            image = go.GetComponent<Image>();
        }
        else
        {
            image = child.GetComponent<Image>();
            if (image == null)
                image = child.gameObject.AddComponent<Image>();
        }

        ApplyDefaultSprite(image);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
        return image;
    }

    private static void RemoveChildObject(RectTransform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return;

        var child = parent.Find(childName);
        if (child == null)
            return;

        child.gameObject.SetActive(false);
        if (Application.isPlaying)
            Destroy(child.gameObject);
        else
            DestroyImmediate(child.gameObject);
    }

    private static void SetTopLeftRect(RectTransform rect, Rect rectFromTopLeft)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(rectFromTopLeft.x, -rectFromTopLeft.y);
        rect.sizeDelta = new Vector2(rectFromTopLeft.width, rectFromTopLeft.height);
    }

    private static void SetTextRect(Text text, Rect rectFromTopLeft, int fontSize, TextAnchor anchor, Color color)
    {
        if (text == null)
            return;

        SetTopLeftRect(text.rectTransform, rectFromTopLeft);
        ApplyKoreanFont(text);
        text.fontSize = fontSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(8, fontSize - 4);
        text.resizeTextMaxSize = fontSize;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.color = color;
        text.raycastTarget = false;
    }

    private static void RemoveComponent(Component component)
    {
        if (component == null)
            return;

        if (Application.isPlaying)
            Destroy(component);
        else
            DestroyImmediate(component);
    }

    private void HookInventoryEvents()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryUpdated -= OnInventoryUpdated;
            InventoryManager.Instance.OnInventoryUpdated += OnInventoryUpdated;
        }
    }

    private void UnhookInventoryEvents()
    {
        var inventoryManager = InventoryManager.ExistingInstance;
        if (inventoryManager != null)
            inventoryManager.OnInventoryUpdated -= OnInventoryUpdated;
    }

    private void OnInventoryUpdated()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        RefreshList(preserveSelection: true);
    }

    private void RefreshList(bool preserveSelection)
    {
        if (!_uiBuilt)
            BuildLayout();

        if (!_uiBuilt)
            return;

        var inv = InventoryManager.Instance;
        if (inv == null)
        {
            UpdateSlotSummary(0);
            ShowListEmpty(true);
            SetDetailEmpty("InventoryManager가 없습니다.");
            return;
        }

        var filtered = GetFilteredEquipments(inv);
        UpdateSlotSummary(filtered.Count);
        if (filtered.Count == 0)
        {
            ClearItemWidgets();
            ShowListEmpty(true);
            SetDetailEmpty("해당 부위의 장비가 없습니다.");
            return;
        }

        if (!preserveSelection)
            _selectedInstanceId = GetDefaultSelection(filtered);
        else if (_selectedInstanceId < 0 || !ContainsInstance(filtered, _selectedInstanceId))
            _selectedInstanceId = GetDefaultSelection(filtered);

        ClearItemWidgets();
        ShowListEmpty(false);

        for (int i = 0; i < filtered.Count; i++)
        {
            var item = filtered[i];
            var go = Instantiate(_itemPrefab, _listContent);
            go.SetActive(true);
            var itemRect = go.GetComponent<RectTransform>();
            if (itemRect != null)
            {
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = _useHomeDetailResourceLayout ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                itemRect.pivot = _useHomeDetailResourceLayout ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
                itemRect.sizeDelta = _useHomeDetailResourceLayout ? new Vector2(ResourceCardWidth, ResourceCardHeight) : new Vector2(0f, 68f);
                if (_useHomeDetailResourceLayout)
                    PositionResourceItem(itemRect, i);
            }
            var itemUI = go.GetComponent<HomeEquipPopupItemUI>();
            if (itemUI == null)
                itemUI = go.AddComponent<HomeEquipPopupItemUI>();

            bool selected = item.InstanceId == _selectedInstanceId;
            itemUI.Setup(item, () => SelectItem(item.InstanceId), selected, _useHomeDetailResourceLayout, _useManualObjectLayout);
            _spawnedItems.Add(itemUI);
        }

        if (_useHomeDetailResourceLayout && _listContent != null)
        {
            ResizeResourceListContent(filtered.Count);
            Canvas.ForceUpdateCanvases();
        }

        UpdateCategoryHighlights();
        RefreshDetailPanel();
        NormalizePopupTextColors();
    }

    private void SelectItem(long instanceId)
    {
        _selectedInstanceId = instanceId;
        RefreshList(preserveSelection: true);
    }

    private static void PositionResourceItem(RectTransform itemRect, int index)
    {
        if (itemRect == null)
            return;

        int column = index % ResourceCardColumns;
        int row = index / ResourceCardColumns;
        itemRect.anchoredPosition = new Vector2(column * (ResourceCardWidth + ResourceCardGapX), -row * (ResourceCardHeight + ResourceCardGapY));
    }

    private void ResizeResourceListContent(int itemCount)
    {
        if (_listContent == null)
            return;

        int rows = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)ResourceCardColumns));
        float height = rows * ResourceCardHeight + Mathf.Max(0, rows - 1) * ResourceCardGapY;
        float width = ResourceCardColumns * ResourceCardWidth + (ResourceCardColumns - 1) * ResourceCardGapX;
        _listContent.sizeDelta = new Vector2(width, height);
    }

    private void RefreshDetailPanel()
    {
        EnsureDetailIconFrame();

        var item = FindSelectedItem();
        if (item == null)
        {
            SetDetailEmpty("장비를 선택하세요.");
            return;
        }

        var tmpl = ItemDataManager.Instance != null ? ItemDataManager.Instance.GetEquipment(item.TemplateId) : null;
        if (tmpl == null)
        {
            SetDetailEmpty($"템플릿을 찾을 수 없습니다. ({item.TemplateId})");
            return;
        }

        if (_detailIcon != null)
        {
            _detailIcon.sprite = null;
            _detailIcon.enabled = false;
            _detailIcon.preserveAspect = true;
            _detailIcon.raycastTarget = false;
            var icon = RhythmRPG.Managers.GameResourceManager.Instance != null
                ? RhythmRPG.Managers.GameResourceManager.Instance.GetIcon(tmpl.id)
                : null;

            if (icon == null && !string.IsNullOrEmpty(tmpl.icon_path))
            {
                icon = LoadEquipmentSprite(tmpl.icon_path);
            }

            if (icon != null)
            {
                _detailIcon.sprite = icon;
                _detailIcon.enabled = true;
            }
        }

        if (_detailIconFrame != null)
        {
            _detailIconFrame.enabled = true;
            _detailIconFrame.transform.SetAsFirstSibling();
            if (_detailIcon != null)
                _detailIcon.transform.SetAsLastSibling();
        }

        if (_detailName != null)
            _detailName.text = tmpl.name;

        if (_detailMeta != null)
        {
            var equippedText = item.IsEquipped ? "장착 중" : "미장착";
            _detailMeta.text = $"{tmpl.GradeEnum} / {tmpl.SlotEnum} / Lv.{item.EnhancementLevel} / {equippedText}";
        }

        if (_detailStats != null)
        {
            _detailStats.text =
                "능력치\n" +
                $"공격 +{tmpl.base_atk + item.EnhancementLevel}    방어 +{tmpl.base_def}    체력 +{tmpl.base_hp}";
        }

        if (_detailDescription != null)
        {
            _detailDescription.text = string.IsNullOrWhiteSpace(tmpl.description)
                ? "상세 설명이 없습니다."
                : tmpl.description;
        }

        BuildDetailRhythmSections(tmpl);

        if (_detailStatus != null)
        {
            _detailStatus.text = item.IsEquipped
                ? "현재 이 장비는 장착되어 있습니다."
                : "이 장비는 아직 장착되지 않았습니다.";
        }

        if (_actionButtonText == null && _actionButton != null)
            _actionButtonText = _actionButton.GetComponentInChildren<Text>(true);

        if (_actionButton != null)
        {
            _actionButton.interactable = true;
            SetButtonLabel(_actionButton, item.IsEquipped ? "장착 해제" : "장착");
        }
    }

    private void OnActionButtonClicked()
    {
        var item = FindSelectedItem();
        if (item == null)
        {
            SetDetailEmpty("선택된 장비가 없습니다.");
            return;
        }

        InventoryManager.Instance?.EquipItemApi(item.InstanceId, !item.IsEquipped);
    }

    private void SetDetailEmpty(string message)
    {
        EnsureDetailIconFrame();

        if (_detailIcon != null)
        {
            _detailIcon.sprite = null;
            _detailIcon.enabled = false;
        }

        if (_detailIconFrame != null)
        {
            _detailIconFrame.enabled = true;
            _detailIconFrame.transform.SetAsFirstSibling();
        }

        if (_detailName != null)
            _detailName.text = "장비 없음";

        if (_detailMeta != null)
            _detailMeta.text = "-";

        if (_detailStats != null)
            _detailStats.text = "-";

        if (_detailDescription != null)
            _detailDescription.text = message;

        ClearDetailRhythmSections();
        SetDetailBodyContentHeight(GetBaseDetailContentHeight());

        if (_detailStatus != null)
            _detailStatus.text = message;

        if (_actionButton != null)
        {
            _actionButton.interactable = false;
            SetButtonLabel(_actionButton, "장착");
        }

        NormalizePopupTextColors();
    }

    private void BuildDetailRhythmSections(EquipmentTemplate tmpl)
    {
        if (tmpl == null)
            return;

        var root = EnsureDetailRhythmInfoRoot();
        if (root == null)
            return;

        ClearChildren(root);

        float width = GetDetailInnerWidth();
        float y = 0f;
        bool added = false;

        if (!string.IsNullOrWhiteSpace(tmpl.normal_attack_skill_id))
        {
            y = BuildSkillInfoCard(root, y, "기본 공격", tmpl.normal_attack_skill_id);
            added = true;
        }

        if (!string.IsNullOrWhiteSpace(tmpl.skill_id))
        {
            if (added)
                y += 8f;

            y = BuildSkillInfoCard(root, y, "스킬", tmpl.skill_id);
            added = true;
        }

        if (!added)
            y = BuildEmptySkillInfoCard(root, y, "전투 정보", "연결된 공격/스킬 데이터가 없습니다.");

        SetTopLeftRect(root, new Rect(GetDetailInnerX(), GetSkillSectionStartY(), width, Mathf.Max(64f, y)));
        SetDetailBodyContentHeight(GetSkillSectionStartY() + y + 18f);
    }

    private void ClearDetailRhythmSections()
    {
        var root = EnsureDetailRhythmInfoRoot();
        if (root == null)
            return;

        ClearChildren(root);
        root.sizeDelta = Vector2.zero;
    }

    private RectTransform EnsureDetailRhythmInfoRoot()
    {
        if (_rightBodyScrollContent == null)
            return null;

        if (_detailRhythmInfoRoot == null)
            _detailRhythmInfoRoot = FindRect("DetailRhythmInfoRoot");

        if (_detailRhythmInfoRoot == null)
            _detailRhythmInfoRoot = CreateEmptyRect("DetailRhythmInfoRoot", _rightBodyScrollContent);
        else if (_detailRhythmInfoRoot.parent != _rightBodyScrollContent)
            _detailRhythmInfoRoot.SetParent(_rightBodyScrollContent, false);

        _detailRhythmInfoRoot.gameObject.SetActive(true);
        return _detailRhythmInfoRoot;
    }

    private float BuildEmptySkillInfoCard(RectTransform parent, float y, string title, string message)
    {
        float width = GetDetailInnerWidth();
        const float height = 58f;
        var card = CreateRuntimePanel("DetailEmptySkillCard", parent, new Rect(0f, y, width, height), ResourceSkillCardColor);
        AddCardOutline(card);

        var titleText = CreateRuntimeText("Title", card, title, new Rect(12f, 7f, width - 24f, 18f), 13, TextAnchor.MiddleLeft, ResourceParchmentText);
        if (titleText != null)
            titleText.fontStyle = FontStyle.Bold;

        CreateRuntimeText("Message", card, message, new Rect(12f, 29f, width - 24f, 20f), 12, TextAnchor.MiddleLeft, ResourceParchmentMutedText);
        return y + height;
    }

    private float BuildSkillInfoCard(RectTransform parent, float y, string title, string skillId)
    {
        float width = GetDetailInnerWidth();
        var info = CreateSkillVisualInfo(skillId);
        if (info.Definition == null)
            return BuildEmptySkillInfoCard(parent, y, title, $"{SafeText(skillId)} 데이터를 찾을 수 없습니다.");

        const float height = 152f;
        var safeSkillId = SanitizeObjectName(skillId);
        var card = CreateRuntimePanel($"DetailSkillCard_{safeSkillId}", parent, new Rect(0f, y, width, height), ResourceSkillCardColor);
        AddCardOutline(card);

        var titleText = CreateRuntimeText("Title", card, title, new Rect(12f, 7f, 90f, 18f), 13, TextAnchor.MiddleLeft, ResourceParchmentText);
        if (titleText != null)
            titleText.fontStyle = FontStyle.Bold;

        CreateRuntimeText("SkillId", card, skillId, new Rect(102f, 7f, width - 114f, 18f), 11, TextAnchor.MiddleRight, ResourceParchmentMutedText);
        CreateRuntimeText("Summary", card, info.Summary, new Rect(12f, 29f, width - 24f, 28f), 12, TextAnchor.UpperLeft, ResourceParchmentText);
        CreateRuntimeText("RangeLabel", card, "범위", new Rect(12f, 58f, 116f, 16f), 11, TextAnchor.MiddleLeft, ResourceParchmentMutedText);
        CreateRuntimeText("BeatLabel", card, "Beat / InputLock", new Rect(136f, 58f, width - 148f, 16f), 11, TextAnchor.MiddleLeft, ResourceParchmentMutedText);

        var gridRoot = CreateRuntimePanel("RangeGrid", card, new Rect(12f, 76f, 112f, 62f), new Color(0f, 0f, 0f, 0f));
        BuildRangeGrid(gridRoot, info, 112f, 62f);

        var timelineRoot = CreateEmptyRect("BeatTimeline", card);
        SetTopLeftRect(timelineRoot, new Rect(136f, 75f, width - 148f, 64f));
        BuildBeatTimeline(timelineRoot, info, width - 148f);

        return y + height;
    }

    private SkillVisualInfo CreateSkillVisualInfo(string skillId)
    {
        var info = new SkillVisualInfo { SkillId = skillId };
        var skill = LoadSkillDefinition(skillId);
        if (skill == null || skill.Data == null)
        {
            info.Summary = "스킬 데이터 없음";
            return info;
        }

        info.Definition = skill.Data;
        info.TotalTicks = Mathf.Max(TicksPerBeat, skill.Data.TotalDurationTicks);

        if (skill.Data.Tracks != null)
        {
            foreach (var track in skill.Data.Tracks)
            {
                if (track?.Events == null)
                    continue;

                foreach (var evt in track.Events)
                {
                    if (evt == null || evt.Action == null)
                        continue;

                    info.TotalTicks = Mathf.Max(info.TotalTicks, evt.TriggerTick + Mathf.Max(0, evt.DurationTicks));
                    AddSkillEventInfo(info, evt);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(info.Summary))
            info.Summary = BuildSkillSummary(info);

        return info;
    }

    private void AddSkillEventInfo(SkillVisualInfo info, SkillEvent evt)
    {
        switch (evt.Action)
        {
            case InputLockAction _:
                info.InputLockEvents.Add(evt);
                break;
            case WarningAction warning:
                info.WarningEvents.Add(evt);
                AddShapeCells(info, warning.Shape);
                break;
            case DamageAction damage:
                info.EffectEvents.Add(evt);
                AddShapeCells(info, damage.Shape);
                info.SummaryParts.Add(BuildDamageSummary(damage));
                break;
            case MoveAction move:
                info.EffectEvents.Add(evt);
                AddMoveCells(info, move);
                info.SummaryParts.Add($"{move.MoveType} {move.Distance}칸 이동 ({GetDirectionLabel(move.DirectionX, move.DirectionY)})");
                break;
            case SummonDecoyAction summon:
                info.EffectEvents.Add(evt);
                AddCell(info, new Vector2Int(summon.OffsetX, summon.OffsetY));
                info.SummaryParts.Add($"분신 HP {summon.Hp}, 지속 {FormatBeatCount(summon.DurationTicks)} Beat");
                break;
        }
    }

    private string BuildSkillSummary(SkillVisualInfo info)
    {
        if (info.SummaryParts.Count == 0)
        {
            if (info.WarningEvents.Count > 0)
                info.SummaryParts.Add($"예고 범위 {Mathf.Max(1, info.RangeCells.Count)}칸");
            else if (info.InputLockEvents.Count > 0)
                info.SummaryParts.Add("입력 제한 중심 효과");
            else
                info.SummaryParts.Add("효과 정보 없음");
        }

        var summary = string.Join(" · ", info.SummaryParts);
        if (info.RangeCells.Count > 0 && !summary.Contains("범위"))
            summary += $" · 범위 {info.RangeCells.Count}칸";

        summary += $" · 사용 {FormatBeatCount(info.TotalTicks)} Beat";
        return summary;
    }

    private static string BuildDamageSummary(DamageAction damage)
    {
        var summary = $"피해 {damage.Amount}";
        if (damage.KnockbackDistance > 0)
            summary += $", 밀침 {damage.KnockbackDistance}";
        if (damage.StunDurationTicks > 0)
            summary += $", 경직 {FormatBeatCount(damage.StunDurationTicks)} Beat";
        return summary;
    }

    private static NewSkillSO LoadSkillDefinition(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return null;

        if (SkillDefinitionCache.TryGetValue(skillId, out var cached))
            return cached;

        var skill = Resources.Load<NewSkillSO>(NewSkillResourceRoot + skillId);
        if (skill == null)
        {
            var allSkills = Resources.LoadAll<NewSkillSO>(NewSkillResourceRoot);
            foreach (var candidate in allSkills)
            {
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.name, skillId, StringComparison.OrdinalIgnoreCase) ||
                    (candidate.Data != null && string.Equals(candidate.Data.SkillId, skillId, StringComparison.OrdinalIgnoreCase)))
                {
                    skill = candidate;
                    break;
                }
            }
        }

        SkillDefinitionCache[skillId] = skill;
        return skill;
    }

    private static void AddShapeCells(SkillVisualInfo info, IShapeDef shape)
    {
        switch (shape)
        {
            case CustomCellsShape custom when custom.Cells != null:
                foreach (var cell in custom.Cells)
                    AddCell(info, new Vector2Int(cell.X, cell.Y));
                break;
            case RectShape rect:
                int width = Mathf.Max(1, rect.Width);
                int height = Mathf.Max(1, rect.Height);
                int minX = -Mathf.FloorToInt((width - 1) * 0.5f);
                for (int x = minX; x < minX + width; x++)
                {
                    for (int y = -1; y >= -height; y--)
                        AddCell(info, new Vector2Int(x, y));
                }
                break;
            case DiamondShape diamond:
                int radius = Mathf.Max(1, diamond.Radius);
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (Mathf.Abs(x) + Mathf.Abs(y) <= radius && (x != 0 || y != 0))
                            AddCell(info, new Vector2Int(x, y));
                    }
                }
                break;
        }
    }

    private static void AddMoveCells(SkillVisualInfo info, MoveAction move)
    {
        int dx = move.DirectionX;
        int dy = move.DirectionY;
        if (dx == 0 && dy == 0)
            dy = -1;

        int distance = Mathf.Max(1, move.Distance);
        for (int i = 1; i <= distance; i++)
            AddCell(info, new Vector2Int(dx * i, dy * i));
    }

    private static void AddCell(SkillVisualInfo info, Vector2Int cell)
    {
        if (cell == Vector2Int.zero)
            return;

        if (!info.RangeCells.Contains(cell))
            info.RangeCells.Add(cell);
    }

    private void BuildRangeGrid(RectTransform gridRoot, SkillVisualInfo info, float width, float height)
    {
        if (gridRoot == null)
            return;

        ClearChildren(gridRoot);

        int minX = -1;
        int maxX = 1;
        int minY = -2;
        int maxY = 0;

        foreach (var cell in info.RangeCells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        int absX = Mathf.Max(1, Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX)));
        minX = -absX;
        maxX = absX;
        minY = Mathf.Min(minY, -1);
        maxY = Mathf.Max(maxY, 0);

        if (maxY - minY + 1 < 3)
            minY = maxY - 2;

        int columns = Mathf.Max(3, maxX - minX + 1);
        int rows = Mathf.Max(3, maxY - minY + 1);
        float cellSize = Mathf.Floor(Mathf.Min(width / columns, height / rows));
        cellSize = Mathf.Clamp(cellSize, 7f, 16f);
        float totalWidth = cellSize * columns;
        float totalHeight = cellSize * rows;
        float startX = (width - totalWidth) * 0.5f;
        float startY = (height - totalHeight) * 0.5f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int column = x - minX;
                int row = y - minY;
                var color = ResourceGridCellColor;
                bool isCaster = x == 0 && y == 0;
                bool isTarget = ContainsCell(info.RangeCells, x, y);

                if (isTarget)
                    color = ResourceGridTargetColor;
                if (isCaster)
                    color = ResourceGridCasterColor;

                var cell = CreateRuntimeImage(
                    isCaster ? "CasterCell" : isTarget ? "TargetCell" : "GridCell",
                    gridRoot,
                    new Rect(startX + column * cellSize + 1f, startY + row * cellSize + 1f, cellSize - 2f, cellSize - 2f),
                    color);

                if (isCaster)
                {
                    var casterText = CreateRuntimeText("CasterLabel", cell, "P", new Rect(0f, 0f, cellSize - 2f, cellSize - 2f), 9, TextAnchor.MiddleCenter, ResourceButtonText);
                    if (casterText != null)
                        casterText.fontStyle = FontStyle.Bold;
                }
            }
        }

        if (info.RangeCells.Count == 0)
            CreateRuntimeText("NoRange", gridRoot, "범위 없음", new Rect(0f, height * 0.5f - 10f, width, 20f), 10, TextAnchor.MiddleCenter, ResourceParchmentMutedText);
    }

    private void BuildBeatTimeline(RectTransform root, SkillVisualInfo info, float width)
    {
        if (root == null)
            return;

        ClearChildren(root);
        CreateRuntimeText("TimelineSummary", root, $"사용 {FormatBeatCount(info.TotalTicks)} Beat · Lock {FormatLockSummary(info)}", new Rect(0f, 0f, width, 16f), 10, TextAnchor.MiddleLeft, ResourceParchmentText);

        int beatColumns = Mathf.Clamp(Mathf.CeilToInt(info.TotalTicks / (float)TicksPerBeat), 1, 8);
        float visualTicks = beatColumns * TicksPerBeat;
        CreateTimelineLane(root, "사용", 18f, width, beatColumns, visualTicks, ResourceTimelineUseColor, 0, info.TotalTicks);
        CreateTimelineLane(root, "효과", 34f, width, beatColumns, visualTicks, ResourceTimelineEffectColor, info.EffectEvents);
        CreateTimelineLane(root, "Lock", 50f, width, beatColumns, visualTicks, ResourceTimelineLockColor, info.InputLockEvents);
    }

    private void CreateTimelineLane(RectTransform root, string label, float y, float width, int beatColumns, float visualTicks, Color color, List<SkillEvent> events)
    {
        CreateTimelineLaneBackground(root, label, y, width, beatColumns);

        if (events == null)
            return;

        foreach (var evt in events)
            CreateTimelineRange(root, y, width, visualTicks, color, evt.TriggerTick, evt.DurationTicks);
    }

    private void CreateTimelineLane(RectTransform root, string label, float y, float width, int beatColumns, float visualTicks, Color color, int startTick, int durationTicks)
    {
        CreateTimelineLaneBackground(root, label, y, width, beatColumns);
        CreateTimelineRange(root, y, width, visualTicks, color, startTick, durationTicks);
    }

    private void CreateTimelineLaneBackground(RectTransform root, string label, float y, float width, int beatColumns)
    {
        const float labelWidth = 34f;
        float barWidth = Mathf.Max(60f, width - labelWidth - 2f);
        CreateRuntimeText($"{label}Label", root, label, new Rect(0f, y - 1f, labelWidth, 14f), 9, TextAnchor.MiddleLeft, ResourceParchmentMutedText);

        float beatWidth = barWidth / beatColumns;
        for (int i = 0; i < beatColumns; i++)
        {
            CreateRuntimeImage(
                $"{label}Beat{i}",
                root,
                new Rect(labelWidth + i * beatWidth + 1f, y, Mathf.Max(2f, beatWidth - 2f), 12f),
                new Color(0.32f, 0.22f, 0.12f, 0.20f));
        }
    }

    private void CreateTimelineRange(RectTransform root, float y, float width, float visualTicks, Color color, int startTick, int durationTicks)
    {
        if (durationTicks <= 0)
            return;

        const float labelWidth = 34f;
        float barWidth = Mathf.Max(60f, width - labelWidth - 2f);
        float start = Mathf.Clamp01(startTick / visualTicks);
        float end = Mathf.Clamp01((startTick + durationTicks) / visualTicks);
        float x = labelWidth + start * barWidth;
        float rangeWidth = Mathf.Max(3f, (end - start) * barWidth);
        CreateRuntimeImage("TimelineRange", root, new Rect(x, y + 2f, rangeWidth, 8f), color);
    }

    private RectTransform CreateRuntimePanel(string name, RectTransform parent, Rect rect, Color color)
    {
        if (parent == null)
            return null;

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rectTransform = go.GetComponent<RectTransform>();
        SetTopLeftRect(rectTransform, rect);

        var image = go.GetComponent<Image>();
        ApplyDefaultSprite(image);
        image.color = color;
        image.raycastTarget = false;
        return rectTransform;
    }

    private RectTransform CreateRuntimeImage(string name, RectTransform parent, Rect rect, Color color)
    {
        var imageRect = CreateRuntimePanel(name, parent, rect, color);
        if (imageRect != null)
        {
            var image = imageRect.GetComponent<Image>();
            if (image != null)
            {
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
            }
        }

        return imageRect;
    }

    private Text CreateRuntimeText(string name, RectTransform parent, string text, Rect rect, int fontSize, TextAnchor anchor, Color color)
    {
        if (parent == null)
            return null;

        var label = CreateTextObject(name, parent, text, fontSize, anchor, Vector2.zero, new Vector2(rect.width, rect.height));
        SetTextRect(label, rect, fontSize, anchor, color);
        label.resizeTextMinSize = Mathf.Max(7, fontSize - 3);
        return label;
    }

    private static void AddCardOutline(RectTransform card)
    {
        if (card == null)
            return;

        var outline = card.GetComponent<Outline>();
        if (outline == null)
            outline = card.gameObject.AddComponent<Outline>();

        outline.effectColor = ResourceSkillCardLineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private void SetDetailBodyContentHeight(float height)
    {
        if (_rightBodyScrollContent == null)
            return;

        float width = _rightBodyScrollContent.sizeDelta.x > 1f
            ? _rightBodyScrollContent.sizeDelta.x
            : (_useHomeDetailResourceLayout ? 451f : 300f);

        _rightBodyScrollContent.sizeDelta = new Vector2(width, Mathf.Max(GetBaseDetailContentHeight(), height));

        if (_useHomeDetailResourceLayout && _rightBodyRoot != null)
        {
            var scrollRect = _rightBodyRoot.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                EnsureDetailScrollAffordance(_rightBodyRoot, _rightBodyScrollContent, scrollRect);
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }

    private float GetBaseDetailContentHeight()
    {
        return _useHomeDetailResourceLayout ? 280f : 390f;
    }

    private float GetDetailContentWidth()
    {
        if (_rightBodyScrollContent != null && _rightBodyScrollContent.sizeDelta.x > 1f)
            return _rightBodyScrollContent.sizeDelta.x;

        return _useHomeDetailResourceLayout ? 451f : 300f;
    }

    private float GetDetailInnerX()
    {
        if (_useHomeDetailResourceLayout)
            return 0f;

        return GetDetailContentWidth() > 400f ? 42f : 4f;
    }

    private float GetDetailInnerWidth()
    {
        if (_useHomeDetailResourceLayout)
            return DetailTextWidth;

        return Mathf.Max(260f, GetDetailContentWidth() - GetDetailInnerX() * 2f);
    }

    private float GetSkillSectionStartY()
    {
        return _useHomeDetailResourceLayout ? 96f : 292f;
    }

    private static bool ContainsCell(List<Vector2Int> cells, int x, int y)
    {
        var target = new Vector2Int(x, y);
        return cells != null && cells.Contains(target);
    }

    private static string FormatLockSummary(SkillVisualInfo info)
    {
        if (info.InputLockEvents.Count == 0)
            return "없음";

        if (info.InputLockEvents.Count == 1)
        {
            var evt = info.InputLockEvents[0];
            return FormatBeatRange(evt.TriggerTick, evt.DurationTicks);
        }

        return $"{info.InputLockEvents.Count}구간";
    }

    private static string FormatBeatRange(int startTick, int durationTicks)
    {
        return $"{FormatBeatValue(startTick / (float)TicksPerBeat)}-{FormatBeatValue((startTick + durationTicks) / (float)TicksPerBeat)} Beat";
    }

    private static string FormatBeatCount(int ticks)
    {
        return FormatBeatValue(ticks / (float)TicksPerBeat);
    }

    private static string FormatBeatValue(float value)
    {
        float rounded = Mathf.Round(value);
        if (Mathf.Abs(value - rounded) < 0.01f)
            return ((int)rounded).ToString();

        return value.ToString("0.##");
    }

    private static string GetDirectionLabel(int x, int y)
    {
        if (x == 0 && y < 0) return "전방";
        if (x == 0 && y > 0) return "후방";
        if (x < 0 && y == 0) return "좌";
        if (x > 0 && y == 0) return "우";
        if (x < 0 && y < 0) return "좌전방";
        if (x > 0 && y < 0) return "우전방";
        if (x < 0 && y > 0) return "좌후방";
        if (x > 0 && y > 0) return "우후방";
        return "지정 방향";
    }

    private static string SanitizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "None";

        var chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child == null)
                continue;

            child.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private sealed class SkillVisualInfo
    {
        public string SkillId;
        public NewSkillDef Definition;
        public int TotalTicks;
        public string Summary;
        public readonly List<string> SummaryParts = new();
        public readonly List<Vector2Int> RangeCells = new();
        public readonly List<SkillEvent> WarningEvents = new();
        public readonly List<SkillEvent> EffectEvents = new();
        public readonly List<SkillEvent> InputLockEvents = new();
    }

    private List<SC_Inventory.Equipments> GetFilteredEquipments(InventoryManager inv)
    {
        var result = new List<SC_Inventory.Equipments>();
        if (inv == null || inv.Equipments == null)
            return result;

        foreach (var item in inv.Equipments)
        {
            var tmpl = ItemDataManager.Instance != null ? ItemDataManager.Instance.GetEquipment(item.TemplateId) : null;
            if (tmpl == null)
                continue;

            if (_currentSlot != EquipmentSlot.None && tmpl.SlotEnum != _currentSlot)
                continue;

            result.Add(item);
        }

        result.Sort((a, b) =>
        {
            int equippedCompare = b.IsEquipped.CompareTo(a.IsEquipped);
            if (equippedCompare != 0) return equippedCompare;

            var tmplA = ItemDataManager.Instance != null ? ItemDataManager.Instance.GetEquipment(a.TemplateId) : null;
            var tmplB = ItemDataManager.Instance != null ? ItemDataManager.Instance.GetEquipment(b.TemplateId) : null;
            string nameA = tmplA != null ? tmplA.name : a.TemplateId.ToString();
            string nameB = tmplB != null ? tmplB.name : b.TemplateId.ToString();
            int compare = string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            if (compare != 0) return compare;
            return a.InstanceId.CompareTo(b.InstanceId);
        });

        return result;
    }

    private long GetDefaultSelection(List<SC_Inventory.Equipments> items)
    {
        if (items == null || items.Count == 0)
            return -1;

        foreach (var item in items)
        {
            if (item != null && item.IsEquipped)
                return item.InstanceId;
        }

        return items[0].InstanceId;
    }

    private long FindEquippedInstanceId(EquipmentSlot slot)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || inv.Equipments == null)
            return -1;

        foreach (var item in inv.Equipments)
        {
            if (item == null || !item.IsEquipped)
                continue;

            var tmpl = ItemDataManager.Instance != null ? ItemDataManager.Instance.GetEquipment(item.TemplateId) : null;
            if (tmpl != null && (slot == EquipmentSlot.None || tmpl.SlotEnum == slot))
                return item.InstanceId;
        }

        return -1;
    }

    private bool ContainsInstance(List<SC_Inventory.Equipments> items, long instanceId)
    {
        foreach (var item in items)
        {
            if (item.InstanceId == instanceId)
                return true;
        }
        return false;
    }

    private SC_Inventory.Equipments FindSelectedItem()
    {
        var inv = InventoryManager.Instance;
        if (inv == null)
            return null;

        foreach (var item in inv.Equipments)
        {
            if (item.InstanceId == _selectedInstanceId)
                return item;
        }

        return null;
    }

    private void ClearItemWidgets()
    {
        foreach (Transform child in _listContent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        _spawnedItems.Clear();
    }

    private void UpdateCategoryHighlights()
    {
        foreach (var itemUI in _spawnedItems)
        {
            if (itemUI == null)
                continue;

            itemUI.SetSelected(itemUI.InstanceId == _selectedInstanceId);
        }
    }

    private void UpdateSlotSummary(int itemCount)
    {
        var titleText = GetSelectionTitle(_currentSlot);
        if (_title != null)
            _title.text = titleText;
        if (_titleLegacy != null)
            _titleLegacy.text = titleText;

        if (_slotLabel != null)
        {
            var slotText = GetSlotLabel(_currentSlot);
            _slotLabel.text = $"부위: {slotText}";
        }

        if (_itemCountLabel != null)
            _itemCountLabel.text = $"{itemCount}개 보유";
    }

    private void ShowListEmpty(bool show)
    {
        if (_listEmptyText != null)
            _listEmptyText.gameObject.SetActive(show);
    }

    private void EnsurePopupOrder()
    {
        if (transform != null)
            transform.SetAsLastSibling();

        if (_contentRect != null)
        {
            _contentRect.transform.SetAsFirstSibling();

            var resourceChrome = _contentRect.Find("ResourceChrome");
            if (resourceChrome != null)
                resourceChrome.SetAsFirstSibling();
        }

        if (_title != null)
            _title.transform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));

        if (_browserRoot != null)
            _browserRoot.transform.SetAsLastSibling();

        if (_useHomeDetailResourceLayout && _title != null)
            _title.transform.SetAsLastSibling();

        if (_titleLegacy != null)
            _titleLegacy.transform.SetAsLastSibling();

        if (_closeBtn != null)
            _closeBtn.transform.SetAsLastSibling();
    }

    private void NormalizePopupTextColors()
    {
        if (_browserRoot == null && _contentRect == null)
            return;

        var root = _browserRoot != null ? _browserRoot : _contentRect;
        if (root == null)
            return;

        var texts = root.GetComponentsInChildren<Text>(true);
        foreach (var text in texts)
        {
            if (text == null)
                continue;

            ApplyKoreanFont(text);

            if (text == _titleLegacy)
            {
                text.color = new Color(1f, 0.92f, 0.55f, 1f);
                continue;
            }

            if (_useHomeDetailResourceLayout)
            {
                var inActionButton = _actionButton != null && text.transform.IsChildOf(_actionButton.transform);
                var isMutedText = text == _detailMeta ||
                                  text == _detailDescription ||
                                  text == _detailStatus ||
                                  text == _itemCountLabel ||
                                  text == _listEmptyText;
                text.color = inActionButton
                    ? ResourceButtonText
                    : isMutedText
                        ? ResourceParchmentMutedText
                        : ResourceParchmentText;
                text.raycastTarget = false;
                continue;
            }

            text.color = Color.white;
        }

        var tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmps)
        {
            if (tmp == null)
                continue;

            if (tmp == _title)
            {
                tmp.color = _useHomeDetailResourceLayout
                    ? ResourceParchmentText
                    : new Color(1f, 0.84f, 0f, 1f);
                continue;
            }

            if (_useHomeDetailResourceLayout)
            {
                if (tmp.GetComponentInParent<HomeEquipPopupItemUI>(true) != null)
                    continue;

                tmp.color = ResourceParchmentText;
                continue;
            }

            tmp.color = Color.white;
        }

        if (_listEmptyText != null)
            _listEmptyText.color = _useHomeDetailResourceLayout
                ? ResourceParchmentMutedText
                : new Color(0.92f, 0.92f, 0.95f, 0.9f);

        if (_detailStatus != null && !string.IsNullOrWhiteSpace(_detailStatus.text))
            _detailStatus.color = _useHomeDetailResourceLayout
                ? ResourceParchmentMutedText
                : new Color(0.95f, 0.95f, 0.98f, 1f);
    }

    private void ApplyKoreanFont(TextMeshProUGUI text)
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

    private Text CreateText(string name, RectTransform parent, string text, int fontSize, TextAnchor anchor, Vector2 anchoredPosition, Vector2 size)
    {
        var go = FindGameObject(name);
        if (go == null)
            return CreateTextObject(name, parent, text, fontSize, anchor, anchoredPosition, size);

        go.transform.SetParent(parent, false);

        var label = go.GetComponent<Text>();
        if (label == null)
        {
            label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.color = Color.white;
        }

        label.text = text;
        ApplyKoreanFont(label);
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(8, fontSize - 4);
        label.resizeTextMaxSize = fontSize;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.color = Color.white;
        label.raycastTarget = false;

        return label;
    }

    private Text CreateTextObject(string name, RectTransform parent, string text, int fontSize, TextAnchor anchor, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var label = go.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ApplyKoreanFont(label);
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = anchor;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(8, fontSize - 4);
        label.resizeTextMaxSize = fontSize;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;

        return label;
    }

    private Button CreateButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var img = go.GetComponent<Image>();
        ApplyDefaultSprite(img);
        img.color = new Color(0.18f, 0.18f, 0.24f, 0.96f);

        var btn = go.GetComponent<Button>();

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);

        var text = textGo.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ApplyKoreanFont(text);
        text.fontSize = 15;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;

        var feedback = go.AddComponent<HomeUIButtonFeedback>();
        feedback.Configure(rect, img);

        return btn;
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var img = go.GetComponent<Image>();
        ApplyDefaultSprite(img);
        img.color = color;
        return rect;
    }

    private GameObject CreateImage(string name, RectTransform parent, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var img = go.GetComponent<Image>();
        ApplyDefaultSprite(img);
        img.color = color;
        img.preserveAspect = true;
        return go;
    }

    private static void ApplyDefaultSprite(Image image)
    {
        if (image == null || image.sprite != null)
            return;

        if (_defaultUiSprite == null)
        {
            var texture = Texture2D.whiteTexture;
            _defaultUiSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _defaultUiSprite.name = "RuntimeWhiteUISprite";
        }

        if (_defaultUiSprite != null)
            image.sprite = _defaultUiSprite;
    }

    private static Sprite DetailPanelSprite
    {
        get
        {
            if (_detailPanelSprite == null)
                _detailPanelSprite = Resources.Load<Sprite>(DetailPanelResourcePath);

            return _detailPanelSprite;
        }
    }

    private static Sprite EquipmentPaperSprite
    {
        get
        {
            if (_equipmentPaperSprite == null)
                _equipmentPaperSprite = Resources.Load<Sprite>(EquipmentPaperResourcePath);

            return _equipmentPaperSprite;
        }
    }

    private static Sprite DetailIconFrameSprite
    {
        get
        {
            if (_detailIconFrameSprite == null)
                _detailIconFrameSprite = Resources.Load<Sprite>(DetailIconFrameResourcePath);

            return _detailIconFrameSprite;
        }
    }

    private static Sprite SelectButtonSprite
    {
        get
        {
            if (_selectButtonSprite == null)
                _selectButtonSprite = Resources.Load<Sprite>(SelectButtonResourcePath);

            return _selectButtonSprite;
        }
    }

    private static Sprite BackButtonSprite
    {
        get
        {
            if (_backButtonSprite == null)
                _backButtonSprite = Resources.Load<Sprite>(BackButtonResourcePath);

            return _backButtonSprite;
        }
    }

    private static void ApplyKoreanFont(Text text)
    {
        if (text == null)
            return;

        if (_koreanLegacyFont == null)
            _koreanLegacyFont = Resources.Load<Font>("NanumGothic");

        if (_koreanLegacyFont != null)
            text.font = _koreanLegacyFont;
    }

    private RectTransform CreateEmptyRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private RectTransform FindRect(string name)
    {
        var transforms = transform.GetComponentsInChildren<RectTransform>(true);
        foreach (var rect in transforms)
        {
            if (rect != null && rect.name == name)
                return rect;
        }
        return null;
    }

    private Text FindText(string name, params string[] fallbackNames)
    {
        var texts = transform.GetComponentsInChildren<Text>(true);
        foreach (var text in texts)
        {
            if (text != null && text.gameObject.name == name)
                return text;
        }

        if (fallbackNames != null)
        {
            foreach (var fallback in fallbackNames)
            {
                foreach (var text in texts)
                {
                    if (text != null && text.gameObject.name == fallback)
                        return text;
                }
            }
        }

        return null;
    }

    private TextMeshProUGUI FindTextMeshPro(string name)
    {
        var texts = transform.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in texts)
        {
            if (text != null && text.gameObject.name == name)
                return text;
        }
        return null;
    }

    private Button FindButton(string name)
    {
        var buttons = transform.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button != null && button.gameObject.name == name)
                return button;
        }
        return null;
    }

    private GameObject FindGameObject(string name)
    {
        var transforms = transform.GetComponentsInChildren<Transform>(true);
        foreach (var child in transforms)
        {
            if (child != null && child.name == name)
                return child.gameObject;
        }
        return null;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        var labelText = button.GetComponentInChildren<Text>(true);
        if (labelText != null)
        {
            labelText.text = label;
            ApplyKoreanFont(labelText);
            labelText.raycastTarget = false;
        }
    }

    private static string SafeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static Sprite LoadEquipmentSprite(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return null;

        string cleanPath = iconPath.Trim().Replace("\\", "/");
        var candidates = new List<string> { cleanPath };

        if (cleanPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            cleanPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            cleanPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            var noExt = cleanPath.Substring(0, cleanPath.LastIndexOf('.'));
            candidates.Add(noExt);
        }

        var fileName = Path.GetFileNameWithoutExtension(cleanPath);
        if (!string.IsNullOrWhiteSpace(fileName))
            candidates.Add($"Icons/{fileName}");

        foreach (var candidate in candidates)
        {
            var sprite = Resources.Load<Sprite>(candidate);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static string GetSlotLabel(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.None => "전체",
            EquipmentSlot.Weapon => "무기",
            EquipmentSlot.Shoes => "신발",
            EquipmentSlot.Hat => "모자",
            EquipmentSlot.Accessory => "악세서리",
            _ => slot.ToString()
        };
    }

    private static string GetSelectionTitle(EquipmentSlot slot)
    {
        return slot == EquipmentSlot.None
            ? "장비 선택"
            : $"{GetSlotLabel(slot)} 선택";
    }

    private void EnsureActive(GameObject go)
    {
        if (go != null)
            go.SetActive(true);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Client.Content.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomeEquipPopupUI : MonoBehaviour
{
    private static readonly Color ResourceParchmentText = new Color(0.10f, 0.22f, 0.20f, 1f);
    private static readonly Color ResourceParchmentMutedText = new Color(0.32f, 0.26f, 0.18f, 1f);
    private static readonly Color ResourceButtonText = new Color(0.96f, 0.92f, 0.82f, 1f);

    [SerializeField] private Transform _content;
    [SerializeField] private GameObject _itemPrefab; // Should have HomeEquipPopupItemUI component
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private Button _closeBtn;
    [SerializeField] private bool _useHomeDetailResourceLayout;

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
    private Text _detailName;
    private Text _detailMeta;
    private Text _detailStats;
    private Text _detailDescription;
    private Text _detailStatus;
    private Button _actionButton;
    private Text _actionButtonText;
    private TMP_FontAsset _koreanFont;
    private static Sprite _defaultUiSprite;

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
        var titleText = $"{GetSlotLabel(slot)} 장비";
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

        var iconObj = FindGameObject("DetailIcon") ?? CreateImage("DetailIcon", iconPanel, new Vector2(72f, 72f), Vector2.zero, new Color(1f, 1f, 1f, 1f));
        _detailIcon = iconObj.GetComponent<Image>();
        var iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(68f, 68f);

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
        ApplyHomeDetailResourceLayout();
        NormalizePopupTextColors();
        EnsurePopupOrder();
    }

    private void ApplyHomeDetailResourceLayout()
    {
        if (!_useHomeDetailResourceLayout)
            return;

        SetCenteredRect(_contentRect, new Vector2(1280f, 720f), Vector2.zero);
        SetCenteredRect(_browserRoot, new Vector2(1280f, 720f), Vector2.zero);
        SetImageColor(_contentRect, new Color(0f, 0f, 0f, 0f));
        SetImageColor(_browserRoot, new Color(0f, 0f, 0f, 0f));
        SetImageColor(_leftPanel, new Color(0f, 0f, 0f, 0f));
        SetImageColor(_rightPanel, new Color(0f, 0f, 0f, 0f));
        SetImageColor(_listViewport, new Color(0f, 0f, 0f, 0f));
        ConfigureInputBlockers();

        if (_title != null)
        {
            var titleRect = _title.rectTransform;
            SetTopLeftRect(titleRect, new Rect(118f, 54f, 270f, 30f));
            _title.alignment = TextAlignmentOptions.MidlineLeft;
            _title.color = ResourceParchmentText;
        }

        if (_closeBtn != null)
        {
            var closeRect = _closeBtn.GetComponent<RectTransform>();
            if (closeRect != null)
                SetTopLeftRect(closeRect, new Rect(1120f, 14f, 148f, 48f));
        }

        SetTopLeftRect(_leftPanel, new Rect(34f, 108f, 344f, 500f));
        SetTopLeftRect(_rightPanel, new Rect(895f, 80f, 356f, 568f));

        if (_leftHeaderRoot != null)
            SetTopLeftRect(_leftHeaderRoot, new Rect(12f, 8f, 316f, 58f));

        if (_leftBodyRoot != null)
            SetTopLeftRect(_leftBodyRoot, new Rect(10f, 74f, 324f, 410f));

        SetTextRect(_ownedItemsTitle, new Rect(0f, 0f, 156f, 24f), 16, TextAnchor.MiddleLeft, ResourceParchmentText);
        SetTextRect(_slotLabel, new Rect(0f, 27f, 132f, 18f), 12, TextAnchor.MiddleLeft, ResourceParchmentMutedText);
        SetTextRect(_itemCountLabel, new Rect(188f, 27f, 112f, 18f), 12, TextAnchor.MiddleRight, ResourceParchmentMutedText);

        if (_listViewport != null)
        {
            SetTopLeftRect(_listViewport, new Rect(0f, 0f, 324f, 406f));

            var scrollImage = _listViewport.GetComponent<Image>();
            if (scrollImage != null)
            {
                ApplyDefaultSprite(scrollImage);
                scrollImage.color = new Color(1f, 1f, 1f, 0.01f);
                scrollImage.raycastTarget = false;
            }
        }

        if (_listContent != null)
        {
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(0f, 1f);
            _listContent.pivot = new Vector2(0f, 1f);
            _listContent.anchoredPosition = new Vector2(10f, -10f);
            _listContent.sizeDelta = new Vector2(304f, 0f);

            ConfigureEquipmentGrid();
        }

        if (_rightHeaderRoot != null)
            SetTopLeftRect(_rightHeaderRoot, new Rect(0f, 0f, 356f, 150f));

        if (_rightBodyRoot != null)
        {
            SetTopLeftRect(_rightBodyRoot, new Rect(0f, 150f, 356f, 330f));
            ConfigureScrollViewport(_rightBodyRoot, "DetailBodyContent", new Vector2(356f, 480f), ref _rightBodyScrollContent);
        }

        if (_rightBodyScrollContent != null)
        {
            _rightBodyScrollContent.anchorMin = new Vector2(0f, 1f);
            _rightBodyScrollContent.anchorMax = new Vector2(0f, 1f);
            _rightBodyScrollContent.pivot = new Vector2(0f, 1f);
            _rightBodyScrollContent.anchoredPosition = Vector2.zero;
            _rightBodyScrollContent.sizeDelta = new Vector2(356f, 480f);
        }

        if (_rightFooterRoot != null)
            SetTopLeftRect(_rightFooterRoot, new Rect(0f, 566f, 356f, 64f));

        SetTextRect(FindText("DetailTitle"), new Rect(0f, 0f, 150f, 24f), 17, TextAnchor.MiddleLeft, ResourceParchmentText);

        var iconPanel = FindRect("DetailIconPanel");
        if (iconPanel != null)
            SetTopLeftRect(iconPanel, new Rect(20f, 36f, 86f, 86f));

        if (_detailIcon != null)
            SetCenteredRect(_detailIcon.rectTransform, new Vector2(72f, 72f), Vector2.zero);

        SetTextRect(_detailName, new Rect(132f, 38f, 202f, 26f), 16, TextAnchor.MiddleLeft, ResourceParchmentText);
        SetTextRect(_detailMeta, new Rect(132f, 66f, 202f, 42f), 11, TextAnchor.UpperLeft, ResourceParchmentMutedText);

        if (_detailStats != null)
            SetTextRect(_detailStats, new Rect(18f, 10f, 318f, 112f), 13, TextAnchor.UpperLeft, ResourceParchmentText);

        if (_detailDescription != null)
            SetTextRect(_detailDescription, new Rect(18f, 142f, 318f, 260f), 12, TextAnchor.UpperLeft, ResourceParchmentMutedText);

        SetTextRect(_detailStatus, new Rect(18f, -54f, 300f, 42f), 12, TextAnchor.MiddleLeft, ResourceParchmentMutedText);

        if (_actionButton != null)
        {
            var actionRect = _actionButton.GetComponent<RectTransform>();
            if (actionRect != null)
                SetTopLeftRect(actionRect, new Rect(4f, 0f, 160f, 56f));

            var buttonImage = _actionButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                ApplyDefaultSprite(buttonImage);
                buttonImage.color = new Color(0f, 0f, 0f, 0f);
                buttonImage.raycastTarget = true;
            }

            var buttonText = _actionButton.GetComponentInChildren<Text>(true);
            if (buttonText != null)
                SetTextRect(buttonText, new Rect(0f, 0f, 160f, 56f), 16, TextAnchor.MiddleCenter, ResourceButtonText);
        }

        if (_listEmptyText != null)
            SetTextRect(_listEmptyText, new Rect(18f, 120f, 280f, 48f), 14, TextAnchor.MiddleCenter, ResourceParchmentMutedText);

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
        _actionButton = FindButton("DetailActionButton");
        _actionButtonText = _actionButton != null ? _actionButton.GetComponentInChildren<Text>(true) : null;

        if (_itemPrefab == null)
            _itemPrefab = FindGameObject("Prefab_PopupItem");

        if (_itemPrefab == null || _actionButton == null)
            return false;

        ConfigureInputBlockers();
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

    private void ConfigureScrollViewport(RectTransform viewport, string contentName, Vector2 contentSize, ref RectTransform content)
    {
        if (viewport == null)
            return;

        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
            viewportImage = viewport.gameObject.AddComponent<Image>();
        ApplyDefaultSprite(viewportImage);
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
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
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryUpdated -= OnInventoryUpdated;
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
                itemRect.sizeDelta = _useHomeDetailResourceLayout ? new Vector2(84f, 85f) : new Vector2(0f, 68f);
                if (_useHomeDetailResourceLayout)
                    PositionResourceItem(itemRect, i);
            }
            var itemUI = go.GetComponent<HomeEquipPopupItemUI>();
            if (itemUI == null)
                itemUI = go.AddComponent<HomeEquipPopupItemUI>();

            bool selected = item.InstanceId == _selectedInstanceId;
            itemUI.Setup(item, () => SelectItem(item.InstanceId), selected, _useHomeDetailResourceLayout);
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

        const int columns = 3;
        const float cellWidth = 84f;
        const float cellHeight = 85f;
        const float gapX = 8f;
        const float gapY = 10f;

        int column = index % columns;
        int row = index / columns;
        itemRect.anchoredPosition = new Vector2(column * (cellWidth + gapX), -row * (cellHeight + gapY));
    }

    private void ResizeResourceListContent(int itemCount)
    {
        if (_listContent == null)
            return;

        const int columns = 3;
        const float cellHeight = 85f;
        const float gapY = 10f;

        int rows = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)columns));
        float height = rows * cellHeight + Mathf.Max(0, rows - 1) * gapY;
        _listContent.sizeDelta = new Vector2(304f, height);
    }

    private void RefreshDetailPanel()
    {
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
                $"공격력 +{tmpl.base_atk + item.EnhancementLevel}\n" +
                $"방어력 +{tmpl.base_def}\n" +
                $"체력 +{tmpl.base_hp}\n" +
                $"스킬: {SafeText(tmpl.skill_id)}\n" +
                $"기본 공격: {SafeText(tmpl.normal_attack_skill_id)}";
        }

        if (_detailDescription != null)
        {
            _detailDescription.text = string.IsNullOrWhiteSpace(tmpl.description)
                ? "상세 설명이 없습니다."
                : tmpl.description;
        }

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
        if (_detailIcon != null)
        {
            _detailIcon.sprite = null;
            _detailIcon.enabled = false;
        }

        if (_detailName != null)
            _detailName.text = "장비 없음";

        if (_detailMeta != null)
            _detailMeta.text = "-";

        if (_detailStats != null)
            _detailStats.text = "-";

        if (_detailDescription != null)
            _detailDescription.text = message;

        if (_detailStatus != null)
            _detailStatus.text = message;

        if (_actionButton != null)
        {
            _actionButton.interactable = false;
            SetButtonLabel(_actionButton, "장착");
        }

        NormalizePopupTextColors();
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

            if (text == _titleLegacy)
            {
                text.color = new Color(1f, 0.92f, 0.55f, 1f);
                continue;
            }

            if (_useHomeDetailResourceLayout)
            {
                var inActionButton = _actionButton != null && text.transform.IsChildOf(_actionButton.transform);
                text.color = inActionButton ? ResourceButtonText : ResourceParchmentText;
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
            EquipmentSlot.Head => "머리",
            EquipmentSlot.Armor => "갑옷",
            EquipmentSlot.Pants => "하의",
            EquipmentSlot.Shoes => "신발",
            EquipmentSlot.Accessory => "장신구",
            _ => slot.ToString()
        };
    }

    private void EnsureActive(GameObject go)
    {
        if (go != null)
            go.SetActive(true);
    }
}

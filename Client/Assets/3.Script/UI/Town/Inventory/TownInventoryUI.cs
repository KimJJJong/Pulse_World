using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using Client.Content.Item;

public class TownInventoryUI : MonoBehaviour
{
    public enum Category { All, Equipment, Consumable, Material }
    public enum SortType { Recent, Name, Grade }

    [Header("UI References")]
    [SerializeField] private Transform _gridContent;
    [SerializeField] private TownInventorySlotUI _slotPrefab;
    [SerializeField] private TownInventoryDetailsUI _detailsUI;
    
    [Header("Filters")]
    [SerializeField] private TMP_Dropdown _sortDropdown;
    [SerializeField] private Button[] _categoryButtons; // 0=All, 1=Equip, ...
    [SerializeField] private bool _handleHotkey = true;
    [SerializeField] private bool _openOnEnable;
    [SerializeField] private bool _logRefresh;

    private Category _currentCategory = Category.All;
    private SortType _currentSort = SortType.Recent;

    private readonly List<object> _displayItems = new List<object>(); // Mixed Items and Equipments
    private readonly List<object> _filteredItems = new List<object>();
    private readonly List<TownInventorySlotUI> _slotPool = new List<TownInventorySlotUI>();
    private readonly Dictionary<int, ItemTemplate> _templateCache = new Dictionary<int, ItemTemplate>();
    private Transform _panel;
    private bool _openRequested;
    private bool _warnedMissingEventSystem;
    private bool _warnedMissingRaycaster;

    private void OnEnable()
    {
        if (_openOnEnable)
            OpenInventory();
    }

    private void Start()
    {
        if (_openOnEnable)
            _openRequested = true;

        EnsurePanelRef();

        // Setup Category Buttons (Index based mapping for simplicity)
        for(int i=0; _categoryButtons != null && i<_categoryButtons.Length; i++)
        {
            int idx = i;
            if (_categoryButtons[i] != null)
                _categoryButtons[i].onClick.AddListener(() => SetCategory((Category)idx));
        }

        // Setup Sort Dropdown
        if (_sortDropdown != null)
        {
            _sortDropdown.ClearOptions();
            _sortDropdown.AddOptions(new List<string>(System.Enum.GetNames(typeof(SortType))));

            _sortDropdown.onValueChanged.AddListener((val) =>
            {
                _currentSort = (SortType)val;
                RefreshGrid();
            });
        }

        // Listen
        if (InventoryManager.Instance != null)
        {
            if (InventoryManager.Instance.Items.Count == 0 && InventoryManager.Instance.Equipments.Count == 0)
            {
               InventoryManager.Instance.LoadFromApi();
            }
            InventoryManager.Instance.OnInventoryUpdated += RefreshAll;
        }

        RefreshAll();

        if (_panel != null)
            _panel.gameObject.SetActive(_openRequested);
    }

    private void Update()
    {
        if (!_handleHotkey || IsHotkeyOwnedByTownPanel())
            return;

        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    public bool IsOpen => gameObject.activeInHierarchy && EnsurePanelRef() != null && _panel.gameObject.activeSelf;

    public void OpenInventory()
    {
        _openRequested = true;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (EnsurePanelRef() == null)
            return;

        EnsureUiReady();
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        RefreshAll();
    }

    public void CloseInventory()
    {
        _openRequested = false;
        if (EnsurePanelRef() == null)
            return;

        _panel.gameObject.SetActive(false);
    }

    public void ToggleInventory()
    {
        if (IsOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    private Transform EnsurePanelRef()
    {
        if (_panel == null)
            _panel = transform.Find("Panel");
        return _panel;
    }

    private void EnsureUiReady()
    {
        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 8000);
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                if (!_warnedMissingRaycaster)
                {
                    _warnedMissingRaycaster = true;
                    Debug.LogError("[TownInventoryUI] GraphicRaycaster is missing from the inventory Canvas hierarchy object.");
                }
            }
        }

        EnsureEventSystem();
    }

    private void EnsureEventSystem()
    {
        var systems = Resources.FindObjectsOfTypeAll<EventSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            var system = systems[i];
            if (system == null || !system.gameObject.scene.IsValid())
                continue;

            if (!system.gameObject.activeSelf)
                system.gameObject.SetActive(true);
            system.enabled = true;
            EnsureInputModule(system.gameObject);
            return;
        }

        if (!_warnedMissingEventSystem)
        {
            _warnedMissingEventSystem = true;
            Debug.LogError("[TownInventoryUI] EventSystem is missing from the scene hierarchy.");
        }
    }

    private static void EnsureInputModule(GameObject eventSystemGo)
    {
        if (eventSystemGo == null)
            return;

        var inputSystemModule = eventSystemGo.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule != null)
            inputSystemModule.enabled = true;

        var standaloneModule = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null && inputSystemModule != null)
            standaloneModule.enabled = false;
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        var objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null)
                continue;

            if (obj is Component component && component.gameObject.scene.IsValid())
                return obj;

            if (obj is GameObject go && go.scene.IsValid())
                return obj;
        }

        return null;
    }

    private static bool IsHotkeyOwnedByTownPanel()
    {
        var home = FindSceneObject<TownHomeUiController>();
        if (home != null && home.isActiveAndEnabled)
            return true;

        var panel = FindSceneObject<TownExpeditionPanel>();
        return panel != null && panel.isActiveAndEnabled;
    }

    private void OnDestroy()
    {
        var inventoryManager = InventoryManager.ExistingInstance;
        if (inventoryManager != null)
            inventoryManager.OnInventoryUpdated -= RefreshAll;
    }

    public void RefreshAll()
    {
        // Gather all items
        _displayItems.Clear();
        _templateCache.Clear();
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // Add Items
        foreach(var item in inv.Items) _displayItems.Add(item);
        // Add Equipments
        foreach(var equip in inv.Equipments) _displayItems.Add(equip);

        RefreshGrid();
    }

    private void SetCategory(Category cat)
    {
        _currentCategory = cat;
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        // 1. Filter
        _filteredItems.Clear();
        foreach (var item in _displayItems)
        {
            if (CheckCategory(item, _currentCategory))
                _filteredItems.Add(item);
        }

        if (_logRefresh)
            Debug.Log($"[TownInventoryUI] RefreshGrid: Total={_displayItems.Count}, Filtered={_filteredItems.Count} (Category={_currentCategory})");

        // 2. Sort
        _filteredItems.Sort(CompareItems);

        // 3. Render
        for (int i = 0; i < _filteredItems.Count; i++)
        {
            var slot = GetOrCreateSlot(i);
            slot.gameObject.SetActive(true);
            slot.Setup(_filteredItems[i], OnSlotCount);
        }

        for (int i = _filteredItems.Count; i < _slotPool.Count; i++)
        {
            _slotPool[i].gameObject.SetActive(false);
        }
    }

    private bool CheckCategory(object item, Category cat)
    {
        if (cat == Category.All) return true;

        int tid = GetTemplateId(item);
        var tmpl = GetTemplate(tid);
        if (tmpl == null) return false;

        switch(cat)
        {
            case Category.Equipment: return tmpl.TypeEnum == ItemType.Equipment;
            case Category.Consumable: return tmpl.TypeEnum == ItemType.Consumable;
            case Category.Material: return tmpl.TypeEnum == ItemType.Material;
        }
        return false;
    }

    private int CompareItems(object a, object b)
    {
        // SortType: Recent (AcquiredAt?), Name, Grade
        // Currently we don't have AcquiredAt in SC_Inventory locally cached properly or it's difficult to mix.
        // Let's use ID for "Recent" approximation or SlotIndex.
        
        int tidA = GetTemplateId(a);
        int tidB = GetTemplateId(b);
        var tmplA = GetTemplate(tidA);
        var tmplB = GetTemplate(tidB);

        switch(_currentSort)
        {
            case SortType.Name:
                string nameA = tmplA?.name ?? "";
                string nameB = tmplB?.name ?? "";
                return string.Compare(nameA, nameB, System.StringComparison.Ordinal);
            case SortType.Grade:
                int gradeA = tmplA != null ? (int)tmplA.GradeEnum : 0;
                int gradeB = tmplB != null ? (int)tmplB.GradeEnum : 0;
                return gradeB.CompareTo(gradeA); // Descending
            case SortType.Recent:
            default:
                // Use InstanceID descending as proxy for recent
                long idA = GetInstanceId(a);
                long idB = GetInstanceId(b);
                return idB.CompareTo(idA);
        }
    }

    private int GetTemplateId(object obj)
    {
        if (obj is SC_Inventory.Items i) return i.TemplateId;
        if (obj is SC_Inventory.Equipments e) return e.TemplateId;
        return 0;
    }

    private long GetInstanceId(object obj)
    {
         if (obj is SC_Inventory.Items i) return i.InstanceId;
        if (obj is SC_Inventory.Equipments e) return e.InstanceId;
        return 0;
    }

    private void OnSlotCount(object item)
    {
        _detailsUI.Show(item);
    }

    private TownInventorySlotUI GetOrCreateSlot(int index)
    {
        while (_slotPool.Count <= index)
        {
            var slot = Instantiate(_slotPrefab, _gridContent);
            _slotPool.Add(slot);
        }

        return _slotPool[index];
    }

    private ItemTemplate GetTemplate(int templateId)
    {
        if (_templateCache.TryGetValue(templateId, out var cached))
            return cached;

        var manager = ItemDataManager.Instance;
        var template = manager != null ? manager.Get(templateId) : null;
        _templateCache[templateId] = template;
        return template;
    }
}

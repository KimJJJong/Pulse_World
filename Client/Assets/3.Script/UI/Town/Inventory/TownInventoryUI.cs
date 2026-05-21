using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private bool _logRefresh;

    private Category _currentCategory = Category.All;
    private SortType _currentSort = SortType.Recent;

    private readonly List<object> _displayItems = new List<object>(); // Mixed Items and Equipments
    private readonly List<object> _filteredItems = new List<object>();
    private readonly List<TownInventorySlotUI> _slotPool = new List<TownInventorySlotUI>();
    private readonly Dictionary<int, ItemTemplate> _templateCache = new Dictionary<int, ItemTemplate>();
    private Transform _panel;

    private void Start()
    {
        _panel = transform.Find("Panel");

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
            _panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    private void ToggleInventory()
    {
        bool isActive = _gridContent.gameObject.activeInHierarchy; // Using grid as proxy or check root canvas/panel
        // But if this script is on the Root, and we disable Root, Update stops.
        // So we should have a 'Panel' child to toggle, OR use a separate input manager.
        // For simplicity, let's assume the UI Builder made a "Panel" child.
        
        // In UI Builder: GameObject panel = CreateChild(root, "Panel", true);
        // We should explicitly reference the main Panel if possible.
        
        // Let's assume _gridContent parent is the Panel or we can find it.
        // Or finding 'Panel' child.
        
        if (_panel != null)
        {
            bool nextState = !_panel.gameObject.activeSelf;
            _panel.gameObject.SetActive(nextState);
            if (nextState) RefreshAll();
        }
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryUpdated -= RefreshAll;
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

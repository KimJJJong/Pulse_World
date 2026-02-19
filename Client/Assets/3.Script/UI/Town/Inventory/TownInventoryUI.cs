using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Client.Content.Item;
using System.Linq;

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

    private Category _currentCategory = Category.All;
    private SortType _currentSort = SortType.Recent;

    private List<object> _displayItems = new List<object>(); // Mixed Items and Equipments

    private void Start()
    {
        // Setup Category Buttons (Index based mapping for simplicity)
        for(int i=0; i<_categoryButtons.Length; i++)
        {
            int idx = i;
            _categoryButtons[i].onClick.AddListener(() => SetCategory((Category)idx));
        }

        // Setup Sort Dropdown
        _sortDropdown.ClearOptions();
        var sortOptions = System.Enum.GetNames(typeof(SortType)).ToList();
        _sortDropdown.AddOptions(sortOptions);

        _sortDropdown.onValueChanged.AddListener((val) => 
        {
            _currentSort = (SortType)val;
            RefreshGrid();
        });

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

        Transform panel = transform.Find("Panel");
            panel.gameObject.SetActive(false);
      
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
        
        Transform panel = transform.Find("Panel");
        if (panel != null)
        {
            bool nextState = !panel.gameObject.activeSelf;
            panel.gameObject.SetActive(nextState);
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
        var filtered = _displayItems.Where(x => CheckCategory(x, _currentCategory)).ToList();
        Debug.Log($"[TownInventoryUI] RefreshGrid: Total={_displayItems.Count}, Filtered={filtered.Count} (Category={_currentCategory})");

        // 2. Sort
        filtered.Sort((a, b) => CompareItems(a, b));

        // 3. Render
        foreach(Transform t in _gridContent) Destroy(t.gameObject);

        foreach(var item in filtered)
        {
            var go = Instantiate(_slotPrefab, _gridContent);
            go.gameObject.SetActive(true);
            go.Setup(item, OnSlotCount);
        }
    }

    private bool CheckCategory(object item, Category cat)
    {
        if (cat == Category.All) return true;

        int tid = GetTemplateId(item);
        var tmpl = ItemDataManager.Instance.Get(tid);
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
        
        if (Client.Content.Item.ItemDataManager.Instance == null) return 0;

        int tidA = GetTemplateId(a);
        int tidB = GetTemplateId(b);
        var tmplA = Client.Content.Item.ItemDataManager.Instance.Get(tidA);
        var tmplB = Client.Content.Item.ItemDataManager.Instance.Get(tidB);

        switch(_currentSort)
        {
            case SortType.Name:
                string nameA = tmplA?.name ?? "";
                string nameB = tmplB?.name ?? "";
                return nameA.CompareTo(nameB);
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
}

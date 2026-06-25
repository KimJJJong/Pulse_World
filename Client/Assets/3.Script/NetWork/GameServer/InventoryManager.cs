using Client.Content.Item;
using ServerCore;
using System;
using System.Collections.Generic;
using UnityEngine;

using NetClient.Network.Http.Dtos;

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager _instance;
    private static bool _isShuttingDown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _isShuttingDown = false;
        Application.quitting -= MarkShuttingDown;
        Application.quitting += MarkShuttingDown;
    }

    private static void MarkShuttingDown()
    {
        _isShuttingDown = true;
    }

    public static InventoryManager ExistingInstance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<InventoryManager>();
            return _instance;
        }
    }

    public static InventoryManager Instance
    {
        get
        {
            if (_isShuttingDown)
                return null;

            if (_instance == null)
            {
                _instance = FindAnyObjectByType<InventoryManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("InventoryManager");
                    _instance = go.AddComponent<InventoryManager>();
                    
                    // Ensure Item Data is loaded too if needed
                   if (ItemDataManager.Instance == null)
                   {
                        GameObject dataGo = new GameObject("ItemDataManager");
                        dataGo.AddComponent<ItemDataManager>();
                   }
                }
            }
            return _instance;
        }
    }

    // Local Inventory State
    public List<SC_Inventory.Items> Items { get; private set; } = new List<SC_Inventory.Items>();
    public List<SC_Inventory.Equipments> Equipments { get; private set; } = new List<SC_Inventory.Equipments>();

    public event Action OnInventoryUpdated;



    // API Cache for Persistence
    // key: InstanceId
    private Dictionary<long, GameItemDto> _inventoryCache = new Dictionary<long, GameItemDto>();

    [Header("Debug")]
    [SerializeField] private string _testUid = "TestUser";

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnApplicationQuit()
    {
        MarkShuttingDown();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // State Flag
    private bool _isLoaded = false;
    private bool _loadInFlight = false;

    public bool IsLoaded => _isLoaded;
    public bool IsLoadInFlight => _loadInFlight;

    public void OnInventoryReceived(SC_Inventory p)
    {
        // Server (GameServer) sends separated lists via Packet
        UpdateInventory(p.itemss, p.equipmentss);
    }

    private void UpdateFromDto(List<GameItemDto> allItems)
    {
        // Split unified list into Client's Items/Equipments lists
        List<SC_Inventory.Items> newItems = new List<SC_Inventory.Items>();
        List<SC_Inventory.Equipments> newEquips = new List<SC_Inventory.Equipments>();
        
        Debug.Log($"[InventoryManager] UpdateFromDto Called. Count: {allItems?.Count ?? 0}");

        if (allItems != null)
        {
            foreach (var item in allItems)
            {
                // Check Type via ItemDataManager
                bool isEquipment = false;
                
                // Correction: ItemDataManager usually has GetItem (for Consumable) and GetEquipment?
                // Let's try GetEquipment first.
                var equipTmpl = ItemDataManager.Instance.GetEquipment(item.templateId);
                if (equipTmpl != null) isEquipment = true;

                Debug.Log($"[InventoryManager] Item {item.id} TID:{item.templateId} IsEquip:{isEquipment}");

                if (isEquipment)
                {
                    newEquips.Add(new SC_Inventory.Equipments
                    {
                        InstanceId = item.id,
                        TemplateId = item.templateId,
                        SlotIndex = item.slotIndex,
                        EnhancementLevel = item.enhancementLevel,
                        IsEquipped = item.isEquipped,
                        BaseStats = item.baseStats,
                        RandomOptions = item.randomOptions,
                        AcquiredAt = item.acquiredAt // Map AcquiredAt
                    });
                }
                else
                {
                    newItems.Add(new SC_Inventory.Items
                    {
                        InstanceId = item.id,
                        TemplateId = item.templateId,
                        Amount = item.amount,
                        SlotIndex = item.slotIndex,
                        AcquiredAt = item.acquiredAt // Map AcquiredAt
                    });
                }
            }
        }
        
        Debug.Log($"[InventoryManager] Processed DTO. NewItems: {newItems.Count}, NewEquips: {newEquips.Count}");

        UpdateInventory(newItems, newEquips);
    }

    private void UpdateInventory(List<SC_Inventory.Items> newItems, List<SC_Inventory.Equipments> newEquips)
    {
        Items = newItems ?? new List<SC_Inventory.Items>();
        Equipments = newEquips ?? new List<SC_Inventory.Equipments>();

        // Rebuild Unified Cache
        _inventoryCache.Clear();

        foreach (var i in Items)
        {
            _inventoryCache[i.InstanceId] = new GameItemDto
            {
                id = i.InstanceId,
                templateId = i.TemplateId,
                amount = i.Amount,
                slotIndex = i.SlotIndex,
                // Default others
                enhancementLevel = 0,
                isEquipped = false,
                baseStats = "{}",
                randomOptions = "{}",
                acquiredAt = i.AcquiredAt
            };
        }

        foreach (var e in Equipments)
        {
            _inventoryCache[e.InstanceId] = new GameItemDto
            {
                id = e.InstanceId,
                templateId = e.TemplateId,
                slotIndex = e.SlotIndex,
                enhancementLevel = e.EnhancementLevel,
                isEquipped = e.IsEquipped,
                baseStats = e.BaseStats,
                randomOptions = e.RandomOptions,
                acquiredAt = e.AcquiredAt,
                amount = 1 
            };
        }

        _isLoaded = true; // Mark as loaded
        
        Debug.Log($"[InventoryManager] Inventory Updated. Items: {Items.Count}, Equips: {Equipments.Count}.");
        OnInventoryUpdated?.Invoke();
    }
    
    // ...

    public void EquipItemApi(long instanceId, bool toEquip)
    {
        var target = Equipments.Find(x => x.InstanceId == instanceId);
        if (target == null) return;

        var tmpl = ItemDataManager.Instance.GetEquipment(target.TemplateId);
        if (tmpl == null) return;

        if (toEquip)
        {
            foreach (var e in Equipments)
            {
                if (e.InstanceId == instanceId) continue;
                if (!e.IsEquipped) continue;

                var eTmpl = ItemDataManager.Instance.GetEquipment(e.TemplateId);
                // If same slot type, unequip it
                if (eTmpl != null && eTmpl.SlotEnum == tmpl.SlotEnum)
                {
                    e.IsEquipped = false;
                    // Critical: Update Cache too!
                    if (_inventoryCache.ContainsKey(e.InstanceId)) 
                    {
                        _inventoryCache[e.InstanceId].isEquipped = false;
                    }
                }
            }
        }

        target.IsEquipped = toEquip;
        if (_inventoryCache.ContainsKey(instanceId)) 
        {
            _inventoryCache[instanceId].isEquipped = toEquip;
        }

        OnInventoryUpdated?.Invoke();
        SaveToApi();
    }

    public async void LoadFromApi()
    {
        if (_loadInFlight)
            return;

        _loadInFlight = true;
        try
        {
            // 1. Try SessionContext (Socket Session)
            var uid = SessionContext.Instance.Uid;

            // 2. Fallback to Persistent TokenStore (Auth Session)
            if (string.IsNullOrEmpty(uid))
            {
                if (AppBootstrap.Instance != null && AppBootstrap.Instance.Root != null)
                {
                    uid = AppBootstrap.Instance.Root.Tokens.Uid;
                }
            }

            // 3. Keep Test UID logic
            if (string.IsNullOrEmpty(uid))
            {
                if (Application.isEditor && !string.IsNullOrEmpty(_testUid)) uid = _testUid;
                else
                {
                    Debug.LogError("[InventoryManager] LoadFromApi Failed: UID is Empty");
                    return;
                }
            }

            var api = AppBootstrap.Instance?.Root?.Api;
            if (api == null) return;

            Debug.Log($"[InventoryManager] Loading Inventory for UID: {uid}...");
            // API returns { Items: [...], Equipments: [...] }
            var res = await api.GetJsonAsync<InventoryResponse>($"/api/inventory/{uid}", attachAuth: true);

            if (res.Ok && res.Data != null)
            {
                // Merge items + equipments into single list for UpdateFromDto
                var allItems = new List<GameItemDto>();
                if (res.Data.items != null) allItems.AddRange(res.Data.items);
                if (res.Data.equipments != null) allItems.AddRange(res.Data.equipments);
                Debug.Log($"[InventoryManager] API Response: Items={res.Data.items?.Count ?? 0}, Equipments={res.Data.equipments?.Count ?? 0}");
                UpdateFromDto(allItems);
            }
            else
            {
                Debug.LogError($"[InventoryManager] API Load Failed: {res.Error} (Code: {res.StatusCode})");
            }
        }
        finally
        {
            _loadInFlight = false;
        }
    }

    private async void SaveToApi()
    {
        if (!_isLoaded)
        {
            Debug.LogWarning("[InventoryManager] SaveToApi skipped: Inventory not loaded yet.");
            return;
        }

        var uid = SessionContext.Instance.Uid;
        // ...
        if (string.IsNullOrEmpty(uid))
        {
            if (AppBootstrap.Instance != null && AppBootstrap.Instance.Root != null)
            {
                uid = AppBootstrap.Instance.Root.Tokens.Uid;
            }
        }

        if (string.IsNullOrEmpty(uid))
        {
            if (Application.isEditor && !string.IsNullOrEmpty(_testUid)) uid = _testUid;
            else return;
        }

        var api = AppBootstrap.Instance?.Root?.Api;
        if (api == null) return;

        // Ensure OwnerUid is set & Validate AcquiredAt
        foreach (var item in _inventoryCache.Values) 
        {
            item.ownerUid = uid;
            if (string.IsNullOrEmpty(item.acquiredAt))
            {
                item.acquiredAt = System.DateTimeOffset.UtcNow.ToString("O");
            }
        }

        // Create Split Payload (Items vs Equipments)
        var itemsList = new List<GameItemDto>();
        var equipList = new List<GameItemDto>();
        foreach (var item in _inventoryCache.Values)
        {
            // Equipment: TID 100000~399999
            if (item.templateId >= 100000 && item.templateId <= 399999)
                equipList.Add(item);
            else
                itemsList.Add(item);
        }

        var payload = new
        {
            Items = itemsList,
            Equipments = equipList
        };

        var res = await api.PostJsonAsync<string>($"/api/inventory/{uid}", payload, attachAuth: true);
        if (!res.Ok) Debug.LogError($"[InventoryManager] API Save Failed: {res.Error}");
        else Debug.Log("[InventoryManager] API Save Success");
    }
    public void OnEquipResult(SC_EquipResult p)
    {
        if (!p.Success)
        {
            Debug.LogWarning($"[InventoryManager] Equip Failed for ID: {p.InstanceId}");
            return;
        }

        var target = Equipments.Find(x => x.InstanceId == p.InstanceId);
        if (target == null)
        {
             Debug.LogWarning($"[InventoryManager] EquipResult: Item {p.InstanceId} not found in local list.");
             return;
        }

        // Apply State
        target.IsEquipped = p.Equipped;

        // Local Logic: Unique Slot Enforcement (Client-side prediction/sync)
        if (p.Equipped)
        {
             var tmpl = ItemDataManager.Instance.GetEquipment(target.TemplateId);
             if (tmpl != null)
             {
                 foreach(var e in Equipments)
                 {
                     if (e.InstanceId == p.InstanceId) continue;
                     if (!e.IsEquipped) continue;
                     
                     var eTmpl = ItemDataManager.Instance.GetEquipment(e.TemplateId);
                     // If same slot type, unequip it
                     if (eTmpl != null && eTmpl.SlotEnum == tmpl.SlotEnum)
                     {
                         e.IsEquipped = false;
                        if(_inventoryCache.ContainsKey(e.InstanceId)) _inventoryCache[e.InstanceId].isEquipped = false;
                     }
                 }
             }
        }

        // Update Cache
        if (_inventoryCache.ContainsKey(p.InstanceId)) 
            _inventoryCache[p.InstanceId].isEquipped = p.Equipped;

        Debug.Log($"[InventoryManager] EquipResult: ID {p.InstanceId} => Equipped: {p.Equipped}");
        OnInventoryUpdated?.Invoke();
    }
}



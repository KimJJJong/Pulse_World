using ServerCore;
using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // Local Inventory State
    public List<SC_Inventory.Items> Items { get; private set; } = new List<SC_Inventory.Items>();
    public List<SC_Inventory.Equipments> Equipments { get; private set; } = new List<SC_Inventory.Equipments>();

    public event Action OnInventoryUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnInventoryReceived(SC_Inventory p)
    {
        Items = p.itemss;
        Equipments = p.equipmentss;

        Debug.Log($"[InventoryManager] Updated: {Items.Count} Items, {Equipments.Count} Equipments");
        OnInventoryUpdated?.Invoke();
    }

    public void OnEquipResult(SC_EquipResult p)
    {
        if (p.Success)
        {
            var target = Equipments.Find(x => x.InstanceId == p.InstanceId);
            if (target != null)
            {
                target.IsEquipped = p.Equipped;
                Debug.Log($"[InventoryManager] Item {target.InstanceId} equipped: {target.IsEquipped}");
                OnInventoryUpdated?.Invoke();
            }
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] Equip failed for {p.InstanceId}");
        }
    }

    public void RequestEquip(long instanceId, bool equip)
    {
        CS_EquipItem pkt = new CS_EquipItem
        {
            InstanceId = instanceId,
            Equip = equip
        };
        NetworkManager.Instance.Send(pkt.Write());
    }

    public void RequestRefresh()
    {
        CS_GetInventory pkt = new CS_GetInventory();
        NetworkManager.Instance.Send(pkt.Write());
    }

    public void RequestCheat(int code, int p1, int p2)
    {
        CS_Cheat pkt = new CS_Cheat
        {
            CheatCode = code,
            Param1 = p1,
            Param2 = p2
        };
        NetworkManager.Instance.Send(pkt.Write());
    }

    public void RequestDestroyItem(long instanceId, int amount)
    {
        CS_DestroyItem pkt = new CS_DestroyItem
        {
            InstanceId = instanceId,
            Amount = amount
        };
        NetworkManager.Instance.Send(pkt.Write());
    }
}

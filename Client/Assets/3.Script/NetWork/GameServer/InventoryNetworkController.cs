using ServerCore;
using UnityEngine;

public class InventoryNetworkController : MonoBehaviour
{
    private static InventoryNetworkController _instance;
    public static InventoryNetworkController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<InventoryNetworkController>();
                if (_instance == null)
                {
                    var go = new GameObject("InventoryNetworkController");
                    _instance = go.AddComponent<InventoryNetworkController>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

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
            return;
        }
    }

    public void RequestRefresh()
    {
        // 1. API Load (Persistence)
        InventoryManager.Instance?.LoadFromApi();

        // 2. Real-time Packet (If connected to Game/Town Server)
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            // CS_GetInventory (if applicable)
            var pkt = new CS_GetInventory();
            NetworkManager.Instance.Send(pkt.Write());
        }
    }

    public void RequestCheat(int code, int p1, int p2)
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
        {
            Debug.LogWarning("[InventoryNetwork] Cannot send cheat: Network not connected.");
            return;
        }

        var pkt = new CS_Cheat { CheatCode = code, Param1 = p1, Param2 = p2 };
        NetworkManager.Instance.Send(pkt.Write());
    }

    public void RequestDestroyItem(long instanceId, int amount)
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;

        var pkt = new CS_DestroyItem { InstanceId = instanceId, Amount = amount };
        NetworkManager.Instance.Send(pkt.Write());
    }

    public void RequestEquip(long instanceId, bool equip)
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;

        var pkt = new CS_EquipItem { InstanceId = instanceId, Equip = equip };
        NetworkManager.Instance.Send(pkt.Write());
    }
}

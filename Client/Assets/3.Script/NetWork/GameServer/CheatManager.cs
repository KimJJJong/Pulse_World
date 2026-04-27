using UnityEngine;
using ServerCore;

public class CheatManager : MonoBehaviour
{
    public static CheatManager Instance { get; private set; }

    [Header("Add Item Settings")]
    [SerializeField] private int _itemTemplateId;
    [SerializeField] private int _itemAmount = 1;
    [Tooltip("비워두면 현재 로그인한 본인 UID로 지급합니다.")]
    [SerializeField] private string _targetUserId = "";

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

    [ContextMenu("Request Add Item")]
    public void RequestAddItem()
    {
        SendCheatPacket(1, _itemTemplateId, _itemAmount);
        Debug.Log($"[CheatManager] Requested Add Item: ID={_itemTemplateId}, Amount={_itemAmount}");
    }

    public void SendCheatPacket(int code, int p1, int p2)
    {
        if (SessionContext.Instance == null)
        {
            Debug.LogError("[CheatManager] SessionContext is null!");
            return;
        }

        CS_Cheat pkt = new CS_Cheat
        {
            CheatCode = code,
            Param1 = p1,
            Param2 = p2
        };

        NetworkManager.Instance.Send(pkt.Write());
    }
    [ContextMenu("Request Add Item (API)")]
    public async void RequestAddItemApi()
    {
        var localUid = ResolveLocalUid();
        var uid = ResolveTargetUid();

        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogError("[CheatManager] No UID found for API request.");
            return;
        }

        var api = AppBootstrap.Instance?.Root?.Api;
        if (api == null)
        {
            Debug.LogError("[CheatManager] ApiClient not initialized.");
            return;
        }

        string url = $"/api/cheat/additem?uid={System.Uri.EscapeDataString(uid)}&templateId={_itemTemplateId}&amount={_itemAmount}";
        Debug.Log($"[CheatManager] Sending API Request: targetUid={uid}, templateId={_itemTemplateId}, amount={_itemAmount}");

        // Using PostJsonAsync with empty body since params are in QueryString for simplicity in this implementation, 
        // OR change Controller to accept Body. 
        // Controller uses [HttpPost] but arguments are scalar, so they come from Query String by default in ASP.NET Core unless [FromBody] is specified.
        
        var res = await api.PostJsonAsync<object>(url, null, attachAuth: true);
        if (res.Ok)
        {
            Debug.Log("[CheatManager] API Cheat Success.");

            if (!string.IsNullOrWhiteSpace(localUid) && string.Equals(localUid, uid, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[CheatManager] Refreshing local inventory...");
                InventoryManager.Instance?.LoadFromApi();
            }
            else
            {
                Debug.Log($"[CheatManager] Target UID is different from local UID. Skipping local inventory refresh. targetUid={uid}");
            }
        }
        else
        {
            Debug.LogError($"[CheatManager] API Cheat Failed: {res.Error} {res.StatusCode}");
        }
    }

    private string ResolveTargetUid()
    {
        if (!string.IsNullOrWhiteSpace(_targetUserId))
            return _targetUserId.Trim();

        return ResolveLocalUid();
    }

    private string ResolveLocalUid()
    {
        var uid = SessionContext.Instance != null ? SessionContext.Instance.Uid : "";
        if (string.IsNullOrWhiteSpace(uid))
        {
            if (AppBootstrap.Instance != null && AppBootstrap.Instance.Root != null)
                uid = AppBootstrap.Instance.Root.Tokens.Uid;
        }

        return string.IsNullOrWhiteSpace(uid) ? "" : uid.Trim();
    }
}

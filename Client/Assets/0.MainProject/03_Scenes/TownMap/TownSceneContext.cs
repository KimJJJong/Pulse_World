using System.Threading.Tasks;
using UnityEngine;

public sealed class TownSceneContext : BaseSceneContext
{
    [Header("Net enter")]
    [SerializeField] private string _mapId = "town";
    [SerializeField] private bool _wantSnapshot = true;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        TryApplyInitMapIfAlreadyReceived();
    }

    protected override void ResolveSceneRefs()
    {
        base.ResolveSceneRefs();
        // Town은 holdAutoInput 활성화 (null 체크 후)
        if (_inputController != null) _inputController.holdAutoInput = true;
    }

    public void OnInitMap(SC_InitMap p) => ApplyInitMapOnce(p);

    /// <summary>ClientFlow가 TownMap 씬 로드 후 호출.</summary>
    public async Task EnterTownAsync()
    {
        await Task.Yield(); // FindFirstObjectByType 안정화

        if (_entered) return;
        _entered = true;

        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[TownSceneContext] NetworkManager.Instance is null");
            return;
        }

        var req = new CS_MapEnter
        {
            ClientTimeMs = NowLocalMs(),
            MapId = _mapId,
            LastKnownRevision = 0,
            WantSnapshot = _wantSnapshot
        };

        Debug.Log("[TownSceneContext] CS_MapEnter sent");
        NetworkManager.Instance.Send(req.Write());
    }

    private void TryApplyInitMapIfAlreadyReceived()
    {
        if (SessionContext.Instance.LastInitMap is null)
        {
            Debug.LogWarning("[TownSceneContext] InitMap cache가 없습니다. 패킷 수신 후 OnInitMap으로 처리됩니다.");
            return;
        }
        ApplyInitMapOnce(SessionContext.Instance.LastInitMap);
    }
}

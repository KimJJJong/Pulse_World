using System.Threading.Tasks;
using UnityEngine;

public sealed class TownSceneContext : BaseSceneContext
{
    [Header("Net enter")]
    [SerializeField] private string _mapId = "town";
    [SerializeField] private int _maxPlayers = 16;
    [SerializeField] private bool _wantSnapshot = true;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        TownExpeditionPanel.EnsureInScene();
        TryApplyInitMapIfAlreadyReceived();
    }

    protected override void ResolveSceneRefs()
    {
        base.ResolveSceneRefs();
        _inputController?.ConfigureForScene(RhythmInputController.InputChannel.Town, enableHoldAutoInput: true);
    }

    public void OnInitMap(SC_InitMap p) => ApplyInitMapOnce(p);

    public void SetMapId(string mapId)
    {
        if (!string.IsNullOrWhiteSpace(mapId))
            _mapId = mapId;
    }

    public void SetMaxPlayers(int maxPlayers)
    {
        if (maxPlayers > 0)
            _maxPlayers = maxPlayers;
    }

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
            MaxPlayers = _maxPlayers,
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

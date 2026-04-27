using System.Threading.Tasks;
using UnityEngine;

public sealed class GameSceneContext : BaseSceneContext
{
    [Header("Net enter")]
    [SerializeField] private bool _autoEnter = true;
    [SerializeField] private string _mapId = "";

    private int _maxPlayers = 2;

    protected override void Awake()
    {
        base.Awake();
    }

    private async void Start()
    {
        await Task.Yield();
        if (_autoEnter) await EnterGameAsync();
    }

    public void SetMapId(string mapId) => _mapId = mapId;
    public void SetMaxPlayers(int max) => _maxPlayers = max;

    /// <summary>
    /// SC_InitMap 패킷이 씬 로드 전에 먼저 도착했을 때 호출 (패킷 핸들러 경로)
    /// </summary>
    public void OnInitMap(SC_InitMap p) => ApplyInitMapOnce(p);

    public async Task EnterGameAsync()
    {
        if (_entered) return;
        _entered = true;

        // ※ 캐시된 InitMap을 여기서 처리하지 않는다.
        // CS_MapEnter를 보내면 서버가 SC_InitMap을 응답하므로
        // OnInitMap() 경로로 정상 처리된다.
        // 씬 로드 전 패킷이 먼저 도착한 경우는 SessionContext.LastInitMap에 저장되어 있고,
        // 서버 재전송 없이 처리해야 한다면 OnInitMap 호출 시점을
        // 씬 오브젝트 초기화 완료 후로 늦춰야 한다. → 아래 WaitForSceneReady 참고.

        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[GameSceneContext] NetworkManager instance is null!");
            return;
        }

        // 씬 오브젝트(BoardView, 타일 등)의 Awake/Start 완료를 보장하기 위해 대기
        await WaitForSceneReady();

        // 전투 중 스킬/패턴 로딩으로 프레임이 끊기지 않도록 공용 콘텐츠를 미리 워밍업한다.
        P2PCombatContentCache.WarmUpSkills();

        // 캐시된 InitMap이 있으면 여기서 처리 (씬 오브젝트 준비 완료 후)
        TryApplyInitMapIfAlreadyReceived();

        var req = new CS_MapEnter
        {
            ClientTimeMs = NowLocalMs(),
            MapId = _mapId,
            LastKnownRevision = 0,
            WantSnapshot = true,
            MaxPlayers = _maxPlayers
        };

        Debug.Log($"[GameSceneContext] Sending CS_MapEnter... MapId={_mapId}");
        NetworkManager.Instance.Send(req.Write());
    }

    /// <summary>
    /// BoardView와 씬 타일 오브젝트들의 Awake/Start 완료를 보장하는 대기.
    /// Unity는 씬 로드 후 같은 프레임에 Awake, 다음 프레임에 Start가 실행되므로
    /// 최소 2프레임 대기가 필요하다.
    /// </summary>
    private async Task WaitForSceneReady()
    {
        // 프레임 1: Awake 완료 보장
        await Task.Yield();
        // 프레임 2: Start 완료 보장 (타일 오브젝트 포함)
        await Task.Yield();
        // 프레임 3: 안전 마진 (대형 씬에서 자식 오브젝트 수가 많은 경우)
        await Task.Yield();
    }

    private void TryApplyInitMapIfAlreadyReceived()
    {
        var cached = SessionContext.Instance.LastInitMap;
        if (cached is null)
        {
            // 캐시 없음 = 정상 케이스 (CS_MapEnter 응답으로 SC_InitMap이 올 것)
            return;
        }

        if (!string.IsNullOrEmpty(_mapId) &&
            !string.Equals(cached.MapId, _mapId, System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[GameSceneContext] Ignoring cached InitMap. Cached={cached.MapId}, Expected={_mapId}");
            return;
        }

        Debug.Log($"[GameSceneContext] Applying cached InitMap after scene ready. MapId={cached.MapId}");
        ApplyInitMapOnce(cached);
    }
}

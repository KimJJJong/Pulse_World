using NetClient.Room.UI;
using System.Threading.Tasks;
using UnityEngine;

public sealed class TownSceneContext : MonoBehaviour
{
    [Header("Optional scene refs (비워도 자동 탐색)")]
    [SerializeField] private ClientGameState _gs;
    [SerializeField] private ClientHandlers _handlers;
    [SerializeField] private MapRegistry _mapRegistry;
    [SerializeField] private BoardView _boardView;

    [Header("Input/Camera (비워도 자동 탐색)")]
    [SerializeField] private RhythmInputControllerBinder _inputBinder;
    [SerializeField] private RhythmInputController _inputController; // Binder가 못 찾을 때 대비
    [SerializeField] private CameraBinder _cameraBinder;            // 네가 준 CameraBinder
    [SerializeField] private CameraFollow _cameraFollow;            // Camera.main의 CameraFollow

    [Header("Rhythm/BGM (비워도 자동 탐색)")]
    [SerializeField] private RhythmClient _rhythm;
    [SerializeField] private BgmDirector _bgmDirector;
    [SerializeField] private AudioOffsetAutoCalibrator _autoCalib;

    [Header("HUD/Debug (비워도 자동 탐색)")]
    [SerializeField] private HudPresenter _hud;
    [SerializeField] private BeatDebugUI_TMP _beatDebug;

    [Header("RoomListPanel (비워도 자동 탐색)")]
    [SerializeField] private ApiClientProvider _apiClientProvider;


    [Header("Net enter")]
    [SerializeField] private string _mapId = "town";
    [SerializeField] private bool _wantSnapshot = true;

    [Header("Runtime loops")]
    [SerializeField] private bool _ensurePingManager = true;
    [SerializeField] private int _pingIntervalMs = 2000;
    [SerializeField] private int _pingTimeoutMs = 6000;
    [SerializeField] private int _pingMaxMiss = 3;


    bool _entered;
    bool _initMapApplied;


    void Awake()
    {
        // 씬에 1개만 존재 전제
        ResolveSceneRefs();
        ValidateCriticalRefs();
        EnsureCoreBindings();
        EnsureRuntimeLoops();
    }
    private void Start()
    {
        TryApplyInitMapIfAlreadyReceived();

    }
    public void OnInitMap(SC_InitMap p)
    {
        // 패킷이 먼저 와서 여기로 들어온 케이스
        ApplyInitMapOnce(p);
    }
    /// <summary>
    /// ClientFlow가 TownMap 씬 로드 후 호출.
    /// </summary>
    public async Task EnterTownAsync()
    {
        // 씬 로드 직후 1프레임 대기(선택): FindFirstObjectByType 안정화
        await Task.Yield();

        if (_entered) return;
        _entered = true;

        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[TownSceneContext] NetworkManager.Instance is null");
            return;
        }

        long nowMs = NowLocalMs();

        var req = new CS_MapEnter
        {
            ClientTimeMs = nowMs,
            MapId = _mapId,
            LastKnownRevision = 0,
            WantSnapshot = _wantSnapshot
        };

        Debug.Log("[TownSceneContext] CS_MapEnter sent");
        NetworkManager.Instance.Send(req.Write());
    }

    // -----------------------------
    // Setup helpers
    // -----------------------------

    void ResolveSceneRefs()
    {
        if (_gs == null) _gs = FindFirstObjectByType<ClientGameState>();
        if (_handlers == null) _handlers = FindFirstObjectByType<ClientHandlers>();
        if (_mapRegistry == null) _mapRegistry = FindFirstObjectByType<MapRegistry>();
        if (_boardView == null) _boardView = FindFirstObjectByType<BoardView>();

        if (_inputBinder == null) _inputBinder = FindFirstObjectByType<RhythmInputControllerBinder>();
        if (_inputController == null) _inputController = FindFirstObjectByType<RhythmInputController>();
        _inputController.holdAutoInput = true;

        if (_cameraBinder == null) _cameraBinder = FindFirstObjectByType<CameraBinder>();
        if (_cameraFollow == null)
        {
            var cam = Camera.main;
            if (cam != null) _cameraFollow = cam.GetComponent<CameraFollow>();
        }

        if (_rhythm == null) _rhythm = FindFirstObjectByType<RhythmClient>();
        if (_bgmDirector == null) _bgmDirector = FindFirstObjectByType<BgmDirector>();
        if (_autoCalib == null) _autoCalib = FindFirstObjectByType<AudioOffsetAutoCalibrator>();

        if (_hud == null) _hud = FindFirstObjectByType<HudPresenter>();
        if (_beatDebug == null) _beatDebug = FindFirstObjectByType<BeatDebugUI_TMP>();

        if (_apiClientProvider == null ) _apiClientProvider = FindFirstObjectByType<ApiClientProvider>();
    }

    void ValidateCriticalRefs()
    {
        // “없으면 그냥 게임이 못 돈다” 급만 에러 처리
        if (_gs == null) Debug.LogError("[TownSceneContext] ClientGameState not found");
        if (_handlers == null) Debug.LogError("[TownSceneContext] ClientHandlers not found");
        if (_mapRegistry == null) Debug.LogError("[TownSceneContext] MapRegistry not found");
        if (_boardView == null) Debug.LogError("[TownSceneContext] BoardView not found");

        // 입력/카메라/BGM/HUD는 없어도 진행 가능(디버그 로그만)
        if (_cameraBinder == null) Debug.LogWarning("[TownSceneContext] CameraBinder not found (camera follow bind skip)");
        if (_cameraFollow == null) Debug.LogWarning("[TownSceneContext] CameraFollow not found on Camera.main");
        if (_inputBinder == null) Debug.LogWarning("[TownSceneContext] RhythmInputControllerBinder not found");
        if (_inputController == null) Debug.LogWarning("[TownSceneContext] RhythmInputController not found");
        if (_apiClientProvider == null ) Debug.LogWarning("[TownSceneContext] ApiClientProvider not found");
    }

    void EnsureCoreBindings()
    {
        //  정석: BoardView가 GS.WorldView를 잡는 구조라면,
        // 보통 BoardView.Start()에서 해도 되지만, 씬 진입 순서 꼬이면 여기서 보강해두면 안전.
        if (_gs != null && _boardView != null)
        {
            if (_gs.WorldView == null)
                _gs.WorldView = _boardView;
        }

        //  CameraBinder는 네 코드처럼 Awake에서 Camera.main의 CameraFollow를 자동 주입하므로
        // 여기서는 “존재만 보장”하면 충분.
        // 실제 타겟 바인딩은 BoardView.OnSpawnOrUpdateEntity에서 MyActorId 스폰 시 Bind.
    }

    void EnsureRuntimeLoops()
    {
        if (!_ensurePingManager)
            return;

        // PingManager는 네가 SC_InitMap에서 만들고 있는데,
        // "씬 컨텍스트가 보장"하는 쪽이 더 정리된 구조라서 여기서도 한 번 보장해줌.
        if (PingManager.Instance == null)
        {
            Debug.Log("[TownSceneContext] Create PingManager");
            new GameObject("PingManager").AddComponent<PingManager>();
        }

        PingManager.Instance.Configure(
            interval: _pingIntervalMs,
            timeout: _pingTimeoutMs,
            maxMiss: _pingMaxMiss);

        // StartLoop를 중복 호출하면 문제될 수 있으니 PingManager 구현이 안전해야 함.
        // (StartLoop 내부에서 running 플래그로 중복 방지 권장)
        PingManager.Instance.StartLoop();
    }


    void TryApplyInitMapIfAlreadyReceived()
    {
        if (SessionContext.Instance.LastInitMap is null)
        {
            Debug.LogWarning("InitMap chach가 없습니다");
            return;
        }
        //ApplyInitMapOnce(SessionContext.Instance.LastInitMap);
    }

    void ApplyInitMapOnce(SC_InitMap p)
    {
        if (_initMapApplied) return;
        _initMapApplied = true;

        //  여기서 "기존 OnInitMap 처리"를 실행하면 됨
        // 1) (선택) 핑/루프/매니저 시작도 여기서 (네가 정한 정책대로)
        // 2) 실제 맵 생성/엔티티 스폰/리듬 세팅은 ClientHandlers 재사용

        var h = ClientHandlers.Instance;
        if (h == null)
        {
            Debug.LogError("[TownSceneContext] ClientHandlers missing in scene");
            return;
        }

        h.HandleSC_InitMap(p);

        // (추가로 Context가 해야 하는 것들 있으면 여기서)
        // 예: UI 초기화, 특정 연출 트리거 등
    }

    static long NowLocalMs()
        => TimeSync.LocalNowMs();
}

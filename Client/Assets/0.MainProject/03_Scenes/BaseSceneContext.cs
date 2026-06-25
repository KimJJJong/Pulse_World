using NetClient.Room.UI;
using UnityEngine;

/// <summary>
/// TownSceneContext / GameSceneContext 공통 초기화 로직을 담는 추상 베이스.
/// Awake 순서: ResolveSceneRefs -> ValidateCriticalRefs -> EnsureCoreBindings -> EnsureRuntimeLoops
/// </summary>
public abstract class BaseSceneContext : MonoBehaviour
{
    [Header("Optional scene refs (비워도 자동 탐색)")]
    [SerializeField] protected ClientGameState _gs;
    [SerializeField] protected ClientHandlers _handlers;
    [SerializeField] protected MapRegistry _mapRegistry;
    [SerializeField] protected BoardView _boardView;

    [Header("Input/Camera (비워도 자동 탐색)")]
    [SerializeField] protected RhythmInputControllerBinder _inputBinder;
    [SerializeField] protected RhythmInputController _inputController;
    [SerializeField] protected CameraBinder _cameraBinder;
    [SerializeField] protected CameraFollow _cameraFollow;

    [Header("Rhythm/BGM (비워도 자동 탐색)")]
    [SerializeField] protected RhythmClient _rhythm;
    [SerializeField] protected BgmDirector _bgmDirector;
    [SerializeField] protected AudioOffsetAutoCalibrator _autoCalib;

    [Header("HUD/Debug (비워도 자동 탐색)")]
    [SerializeField] protected HudPresenter _hud;
    [SerializeField] protected BeatDebugUI_TMP _beatDebug;

    [Header("Room UI")]
    [SerializeField] protected ApiClientProvider _apiClientProvider;

    [Header("Runtime loops")]
    [SerializeField] protected bool _ensurePingManager = true;
    [SerializeField] protected int _pingIntervalMs = 2000;
    [SerializeField] protected int _pingTimeoutMs = 6000;
    [SerializeField] protected int _pingMaxMiss = 3;

    protected bool _entered;
    protected bool _initMapApplied;

    protected virtual void Awake()
    {
        ResolveSceneRefs();
        ValidateCriticalRefs();
        EnsureCoreBindings();
        EnsureRuntimeLoops();
    }

    /// <summary>서브클래스가 override해 추가 ref를 탐색할 수 있음.</summary>
    protected virtual void ResolveSceneRefs()
    {
        if (_gs == null) _gs = FindFirstObjectByType<ClientGameState>();
        if (_handlers == null) _handlers = FindFirstObjectByType<ClientHandlers>();
        if (_mapRegistry == null) _mapRegistry = MapRegistry.EnsureInstance();
        if (_boardView == null) _boardView = FindFirstObjectByType<BoardView>();

        if (_inputBinder == null) _inputBinder = FindFirstObjectByType<RhythmInputControllerBinder>();
        if (_inputController == null) _inputController = FindFirstObjectByType<RhythmInputController>();

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

        if (_apiClientProvider == null) _apiClientProvider = FindFirstObjectByType<ApiClientProvider>();
    }

    protected virtual void ValidateCriticalRefs()
    {
        var tag = $"[{GetType().Name}]";
        if (_gs == null) Debug.LogError($"{tag} ClientGameState not found");
        if (_handlers == null) Debug.LogError($"{tag} ClientHandlers not found");
        if (_mapRegistry == null) Debug.LogError($"{tag} MapRegistry not found");
        if (_boardView == null) Debug.LogError($"{tag} BoardView not found");

        if (_cameraBinder == null) Debug.LogWarning($"{tag} CameraBinder not found");
        if (_cameraFollow == null) Debug.LogWarning($"{tag} CameraFollow not found on Camera.main");
        if (_inputBinder == null) Debug.LogWarning($"{tag} RhythmInputControllerBinder not found");
        if (_inputController == null) Debug.LogWarning($"{tag} RhythmInputController not found");
        if (_apiClientProvider == null) Debug.LogWarning($"{tag} ApiClientProvider not found");
    }

    protected virtual void EnsureCoreBindings()
    {
        if (_gs != null && _boardView != null && _gs.WorldView == null)
            _gs.WorldView = _boardView;
    }

    protected virtual void EnsureRuntimeLoops()
    {
        if (!_ensurePingManager) return;

        if (PingManager.Instance == null)
        {
            Debug.Log($"[{GetType().Name}] PingManager 생성");
            new GameObject("PingManager").AddComponent<PingManager>();
        }

        PingManager.Instance.Configure(
            interval: _pingIntervalMs,
            timeout: _pingTimeoutMs,
            maxMiss: _pingMaxMiss);

        PingManager.Instance.StartLoop();
    }

    protected void ApplyInitMapOnce(SC_InitMap p)
    {
        if (_initMapApplied) return;
        _initMapApplied = true;

        var h = ClientHandlers.Instance;
        if (h == null)
        {
            Debug.LogError($"[{GetType().Name}] ClientHandlers missing in scene");
            return;
        }
        h.HandleSC_InitMap(p);
    }

    protected static long NowLocalMs() => TimeSync.LocalNowMs();
}

using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AppBootstrap : MonoBehaviour
{
    public static AppBootstrap Instance { get; private set; } = null!;
    public AppCompositionRoot Root { get; private set; } = null!;

    [SerializeField] AppConfig config = null!;
    [SerializeField] bool ignoreToken = false;
    bool _debugUisEnabled = true;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (config == null)
            config = Resources.Load<AppConfig>("AppConfig");

        Root = new AppCompositionRoot(config);
        Root.SteamPlatform.Initialize();

        _debugUisEnabled = config == null || !config.DisableDebugUis;
        ApplyDebugUiConfiguration();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (_debugUisEnabled && (config == null || config.ShowSteamDebugHud))
            SteamP2PDebugHud.Ensure(visibleByDefault: true);

        // 앱 시작 시 토큰이 있으면 바로 월드맵으로, 없으면 안내 연출 후 Login으로
        if (!ignoreToken)
        {
            var startScene = Root.Tokens.HasRefreshToken ? SceneNames.WorldMap : SceneNames.HeadphonesRecommended;
            SceneRouter.Load(startScene);
        }
        else
        {
            var startScene = SceneNames.HeadphonesRecommended;
            SceneRouter.Load(startScene);
        }


    }

    void Update()
    {
        Root?.SteamPlatform?.RunCallbacks();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Root?.SteamPlatform?.Shutdown();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneDebugUiState();
    }

    void ApplyDebugUiConfiguration()
    {
        P2PDebugConfig.SetDebugUiEnabled(_debugUisEnabled);
        P2PDebugViewConfig.SetDebugUiEnabled(_debugUisEnabled);
        SteamP2PDebugHud.SetDebugUiEnabled(_debugUisEnabled);
        ApplySceneDebugUiState();
    }

    void ApplySceneDebugUiState()
    {
        if (_debugUisEnabled)
            return;

        foreach (var debugUi in Resources.FindObjectsOfTypeAll<BeatDebugUI_TMP>())
        {
            if (debugUi == null || !debugUi.gameObject.scene.IsValid())
                continue;

            debugUi.gameObject.SetActive(false);
        }

        foreach (var visualDebugger in Resources.FindObjectsOfTypeAll<RhythmVisualDebugger>())
        {
            if (visualDebugger == null || !visualDebugger.gameObject.scene.IsValid())
                continue;

            visualDebugger.enabled = false;
        }
    }
}

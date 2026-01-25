using UnityEngine;

public sealed class AppBootstrap : MonoBehaviour
{
    public static AppBootstrap Instance { get; private set; } = null!;
    public AppCompositionRoot Root { get; private set; } = null!;

    [SerializeField] AppConfig config = null!;
    [SerializeField] bool ignoreToken = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (config == null)
            config = Resources.Load<AppConfig>("AppConfig");

        Root = new AppCompositionRoot(config);

        // 앱 시작 시 토큰이 있으면 바로 Town으로, 없으면 Login으로
        if (!ignoreToken)
        {
            var startScene = Root.Tokens.HasRefreshToken ? SceneNames.Home : SceneNames.Login;
            SceneRouter.Load(startScene);
        }
        else
        {
            var startScene = SceneNames.Login;
            SceneRouter.Load(startScene);
        }


    }
}

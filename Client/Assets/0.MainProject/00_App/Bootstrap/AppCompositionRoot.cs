public sealed class AppCompositionRoot
{
    public AppConfig Config { get; }
    public TokenStore Tokens { get; }
    public ClientIdentity Identity { get; }
    public ApiClient Api { get; }
    public AuthApi AuthApi { get; }
    public SessionApi SessionApi { get; }
    public PlayerStateApi PlayerStateApi { get; }

    public AppCompositionRoot(AppConfig config)
    {
        Config = config;

        Tokens = new TokenStore();
        Identity = new ClientIdentity(config.ClientVersion);

        Api = new ApiClient(config.BaseUrl, config.TimeoutSeconds, Tokens, Identity);

        AuthApi = new AuthApi(Api, Tokens, Identity);
        SessionApi = new SessionApi(Api);
        PlayerStateApi = new PlayerStateApi(Api);
    }
}

public sealed class AppCompositionRoot
{
    public AppConfig Config { get; }
    public TokenStore Tokens { get; }
    public ClientIdentity Identity { get; }
    public ApiClient Api { get; }
    public AuthApi AuthApi { get; }
    public SessionApi SessionApi { get; }
    public PlayerStateApi PlayerStateApi { get; }
    public NetClient.Town.TownRoomApiClient TownRoomApi { get; }
    public ISteamPlatformService SteamPlatform { get; }

    public AppCompositionRoot(AppConfig config)
    {
        Config = config;

        Tokens = new TokenStore();
        Identity = new ClientIdentity(config.ClientVersion);
        SteamPlatform = SteamPlatformServiceFactory.Create(config);

        Api = new ApiClient(config.BaseUrl, config.TimeoutSeconds, Tokens, Identity);

        AuthApi = new AuthApi(Api, Tokens, Identity);
        SessionApi = new SessionApi(Api);
        PlayerStateApi = new PlayerStateApi(Api);
        TownRoomApi = new NetClient.Town.TownRoomApiClient(Api);
    }
}

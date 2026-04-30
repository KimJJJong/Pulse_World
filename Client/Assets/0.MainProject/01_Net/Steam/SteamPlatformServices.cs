using UnityEngine;
using System.Threading.Tasks;

public interface ISteamPlatformService
{
    bool Enabled { get; }
    bool IsInitialized { get; }
    string SteamId64 { get; }
    string DisplayName { get; }
    string LastError { get; }
    string CurrentLobbyId { get; }
    bool HasJoinedLobby { get; }
    bool IsLobbyOwner { get; }

    void Initialize();
    void RunCallbacks();
    void Shutdown();
    Task<SteamWebApiTicketResult> CreateWebApiTicketAsync(string identity, double timeoutSeconds = 10d);
    Task<string> CreateLobbyAsync(string roomId, string title, string mapId, int maxMembers);
    Task<bool> JoinLobbyAsync(string lobbyId, string roomId = "");
    bool UpdateLobbyMetadata(string roomId, string title, string mapId, int maxMembers, string ownerUid = "");
    void LeaveLobby();
}

public sealed class SteamWebApiTicketResult
{
    public bool Success { get; set; }
    public string SteamId64 { get; set; } = "";
    public string Identity { get; set; } = "";
    public string TicketHex { get; set; } = "";
    public string Error { get; set; } = "";
}

public static class SteamPlatformServiceFactory
{
    public static ISteamPlatformService Create(AppConfig config)
    {
#if RHYTHM_USE_FACEPUNCH_STEAMWORKS
        return new FacepunchSteamPlatformService(config);
#else
        return new NullSteamPlatformService(config);
#endif
    }
}

public sealed class NullSteamPlatformService : ISteamPlatformService
{
    private readonly AppConfig _config;

    public NullSteamPlatformService(AppConfig config)
    {
        _config = config;
    }

    public bool Enabled => _config != null && _config.EnableSteam;
    public bool IsInitialized { get; private set; }
    public string SteamId64 => _config != null ? _config.SteamId64Override ?? "" : "";
    public string DisplayName => "";
    public string LastError { get; private set; } = "";
    public string CurrentLobbyId => "";
    public bool HasJoinedLobby => false;
    public bool IsLobbyOwner => false;

    public void Initialize()
    {
        if (!Enabled)
            return;

        LastError = "Facepunch.Steamworks plugin is not installed or RHYTHM_USE_FACEPUNCH_STEAMWORKS is not defined.";
        IsInitialized = false;
        Debug.LogWarning($"[Steam] {LastError}");
    }

    public void RunCallbacks()
    {
    }

    public void Shutdown()
    {
        IsInitialized = false;
    }

    public Task<SteamWebApiTicketResult> CreateWebApiTicketAsync(string identity, double timeoutSeconds = 10d)
    {
        return Task.FromResult(new SteamWebApiTicketResult
        {
            Success = false,
            Identity = identity ?? "",
            Error = LastError
        });
    }

    public Task<string> CreateLobbyAsync(string roomId, string title, string mapId, int maxMembers)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<bool> JoinLobbyAsync(string lobbyId, string roomId = "")
    {
        return Task.FromResult(false);
    }

    public bool UpdateLobbyMetadata(string roomId, string title, string mapId, int maxMembers, string ownerUid = "")
    {
        return false;
    }

    public void LeaveLobby()
    {
    }
}

#if RHYTHM_USE_FACEPUNCH_STEAMWORKS
using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

public sealed class FacepunchSteamPlatformService : ISteamPlatformService
{
    private readonly AppConfig _config;
    private AuthTicket _activeWebTicket;
    private Lobby? _currentLobby;

    public FacepunchSteamPlatformService(AppConfig config)
    {
        _config = config;
    }

    public bool Enabled => _config != null && _config.EnableSteam;
    public bool IsInitialized { get; private set; }
    public string SteamId64 { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string LastError { get; private set; } = "";
    public string CurrentLobbyId => _currentLobby.HasValue ? _currentLobby.Value.Id.ToString() : "";
    public bool HasJoinedLobby => _currentLobby.HasValue && _currentLobby.Value.Id.IsValid;
    public bool IsLobbyOwner => _currentLobby.HasValue && _currentLobby.Value.IsOwnedBy(SteamClient.SteamId);

    public void Initialize()
    {
        if (!Enabled || IsInitialized)
            return;

        if (_config == null || !uint.TryParse(_config.SteamAppId, out var appId) || appId == 0)
        {
            LastError = $"Invalid Steam AppID: '{_config?.SteamAppId ?? ""}'";
            Debug.LogWarning($"[Steam] {LastError}");
            return;
        }

        try
        {
            SteamClient.Init(appId, false);
            IsInitialized = SteamClient.IsValid;
            if (!IsInitialized)
            {
                LastError = "SteamClient.Init completed but SteamClient.IsValid is false.";
                Debug.LogWarning($"[Steam] {LastError}");
                return;
            }

            SteamId64 = SteamClient.SteamId.ToString();
            DisplayName = SteamClient.Name ?? "";
            LastError = "";
            Debug.Log($"[Steam] Initialized. SteamId64={SteamId64}, Name={DisplayName}");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning($"[Steam] Init failed: {ex.Message}");
            IsInitialized = false;
        }
    }

    public void RunCallbacks()
    {
        if (!IsInitialized)
            return;

        SteamClient.RunCallbacks();
    }

    public void Shutdown()
    {
        LeaveLobby();
        CancelActiveWebTicket();
        if (!IsInitialized)
            return;

        SteamClient.Shutdown();
        IsInitialized = false;
        SteamId64 = "";
        DisplayName = "";
    }

    public async Task<SteamWebApiTicketResult> CreateWebApiTicketAsync(string identity, double timeoutSeconds = 10d)
    {
        if (!IsInitialized)
        {
            return new SteamWebApiTicketResult
            {
                Success = false,
                Identity = identity ?? "",
                Error = "Steam is not initialized."
            };
        }

        try
        {
            CancelActiveWebTicket();

            var ticket = await SteamUser.GetAuthTicketForWebApiAsync(identity ?? "", timeoutSeconds);
            if (ticket.Data == null || ticket.Data.Length == 0)
            {
                return new SteamWebApiTicketResult
                {
                    Success = false,
                    Identity = identity ?? "",
                    Error = "Steam returned an empty Web API auth ticket."
                };
            }

            _activeWebTicket = ticket;

            return new SteamWebApiTicketResult
            {
                Success = true,
                SteamId64 = SteamId64,
                Identity = identity ?? "",
                TicketHex = ToHexString(ticket.Data)
            };
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning($"[Steam] Failed to create Web API ticket: {ex.Message}");
            return new SteamWebApiTicketResult
            {
                Success = false,
                SteamId64 = SteamId64,
                Identity = identity ?? "",
                Error = ex.Message
            };
        }
    }

    public async Task<string> CreateLobbyAsync(string roomId, string title, string mapId, int maxMembers)
    {
        if (!IsInitialized)
        {
            LastError = "Steam is not initialized.";
            return "";
        }

        try
        {
            LeaveLobby();

            var created = await SteamMatchmaking.CreateLobbyAsync(Mathf.Clamp(maxMembers, 2, 250));
            if (!created.HasValue)
            {
                LastError = "SteamMatchmaking.CreateLobbyAsync returned null.";
                return "";
            }

            _currentLobby = created.Value;
            ApplyLobbyMetadata(_currentLobby.Value, roomId, title, mapId, maxMembers, SessionContext.Instance != null ? SessionContext.Instance.Uid : "");
            _currentLobby.Value.SetJoinable(true);

            return _currentLobby.Value.Id.ToString();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning($"[Steam] Failed to create lobby: {ex.Message}");
            return "";
        }
    }

    public async Task<bool> JoinLobbyAsync(string lobbyId, string roomId = "")
    {
        if (!IsInitialized)
        {
            LastError = "Steam is not initialized.";
            return false;
        }

        if (!ulong.TryParse(lobbyId, out var parsedLobbyId) || parsedLobbyId == 0)
        {
            LastError = $"Invalid Steam lobby id '{lobbyId}'.";
            return false;
        }

        if (HasJoinedLobby && string.Equals(CurrentLobbyId, lobbyId, StringComparison.Ordinal))
            return true;

        try
        {
            LeaveLobby();

            var joined = await SteamMatchmaking.JoinLobbyAsync((SteamId)parsedLobbyId);
            if (!joined.HasValue)
            {
                LastError = $"SteamMatchmaking.JoinLobbyAsync failed for {lobbyId}.";
                return false;
            }

            _currentLobby = joined.Value;
            _currentLobby.Value.SetMemberData("roomId", roomId ?? "");
            _currentLobby.Value.SetMemberData("clientVersion", _config?.ClientVersion ?? "");
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning($"[Steam] Failed to join lobby {lobbyId}: {ex.Message}");
            return false;
        }
    }

    public bool UpdateLobbyMetadata(string roomId, string title, string mapId, int maxMembers, string ownerUid = "")
    {
        if (!_currentLobby.HasValue)
            return false;

        try
        {
            return ApplyLobbyMetadata(_currentLobby.Value, roomId, title, mapId, maxMembers, ownerUid);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning($"[Steam] Failed to update lobby metadata: {ex.Message}");
            return false;
        }
    }

    public void LeaveLobby()
    {
        if (!_currentLobby.HasValue)
            return;

        try
        {
            _currentLobby.Value.Leave();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Steam] Failed to leave lobby: {ex.Message}");
        }
        finally
        {
            _currentLobby = null;
        }
    }

    private bool ApplyLobbyMetadata(Lobby lobby, string roomId, string title, string mapId, int maxMembers, string ownerUid)
    {
        bool ok = true;
        ok &= lobby.SetData("roomId", roomId ?? "");
        ok &= lobby.SetData("title", title ?? "");
        ok &= lobby.SetData("mapId", mapId ?? "");
        ok &= lobby.SetData("ownerUid", ownerUid ?? "");
        ok &= lobby.SetData("hostSteamId64", SteamId64 ?? "");
        ok &= lobby.SetData("clientVersion", _config?.ClientVersion ?? "");
        lobby.MaxMembers = Mathf.Clamp(maxMembers, 2, 250);
        lobby.SetMemberData("clientVersion", _config?.ClientVersion ?? "");
        if (!string.IsNullOrWhiteSpace(roomId))
            lobby.SetMemberData("roomId", roomId);
        return ok;
    }

    private void CancelActiveWebTicket()
    {
        if (_activeWebTicket == null)
            return;

        try
        {
            _activeWebTicket.Cancel();
            _activeWebTicket.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Steam] Failed to cancel auth ticket: {ex.Message}");
        }
        finally
        {
            _activeWebTicket = null;
        }
    }

    private static string ToHexString(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return "";

        char[] chars = new char[bytes.Length * 2];
        const string alphabet = "0123456789ABCDEF";
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            chars[i * 2] = alphabet[b >> 4];
            chars[i * 2 + 1] = alphabet[b & 0xF];
        }

        return new string(chars);
    }
}
#endif

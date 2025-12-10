/*using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Contracts.Packet;

public class TokenManager
{
    private static TokenManager _instance;
    public static TokenManager Instance => _instance ??= new TokenManager();

    private const string KeyAccessToken = "AccessToken";
    private const string KeyRefreshToken = "RefreshToken";
    private const string KeyDeviceId = "DeviceId";

    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }
    public string DeviceId { get; private set; }

    private string _baseUrl = "http://localhost:5000";

    private TokenManager()
    {
        AccessToken = PlayerPrefs.GetString(KeyAccessToken, "");
        RefreshToken = PlayerPrefs.GetString(KeyRefreshToken, "");
        DeviceId = PlayerPrefs.GetString(KeyDeviceId, SystemInfo.deviceUniqueIdentifier);
        PlayerPrefs.SetString(KeyDeviceId, DeviceId);
    }

    public void InitBaseUrl(string baseUrl)
    {
        if (!string.IsNullOrEmpty(baseUrl))
            _baseUrl = baseUrl;
    }

    /// <summary>
    /// 게스트 로그인 실행 (처음 로그인 or 만료 시)
    /// </summary>
    public async Task<bool> LoginGuestAsync()
    {
        var url = $"{_baseUrl}/login/guest";
        var payload = JsonUtility.ToJson(new GuestLoginReq { deviceId =DeviceId});
        Debug.Log($"Device Id: [{DeviceId}]");

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Client-Version", "1.0.0");

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"❌ LoginGuest failed: {req.responseCode} {req.error}");
            return false;
        }

        try
        {
            var json = req.downloadHandler.text;
            var dto = JsonUtility.FromJson<LoginGuestRes>(json);
            AccessToken = dto.accessToken;
            RefreshToken = dto.refreshToken;

*//*            Debug.Log(json);*//*

            PlayerPrefs.SetString(KeyAccessToken, AccessToken);
            PlayerPrefs.SetString(KeyRefreshToken, RefreshToken);
            PlayerPrefs.Save();
*//*
            Debug.Log($"✅ Login success (uid={dto.uid})");
            Debug.Log($"✅ Login success (uid={dto.accessToken})");
            Debug.Log($"✅ Login success (uid={dto.refreshToken})");*//*
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ LoginGuest parse error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 토큰 만료 시 자동 재발급 (RefreshToken 사용)
    /// </summary>
    public async Task<bool> RefreshAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken))
            return await LoginGuestAsync(); // Refresh 없으면 새 로그인

        var url = $"{_baseUrl}/login/refresh";
        var payload = JsonUtility.ToJson(new { refreshToken = RefreshToken });

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Client-Version", "1.0.0");

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"⚠ Refresh failed: {req.responseCode}, fallback to LoginGuest");
            return await LoginGuestAsync();
        }

        var json = req.downloadHandler.text;
        var dto = JsonUtility.FromJson<LoginGuestRes>(json);
        AccessToken = dto.accessToken;
        RefreshToken = dto.refreshToken;

        PlayerPrefs.SetString(KeyAccessToken, AccessToken);
        PlayerPrefs.SetString(KeyRefreshToken, RefreshToken);
        PlayerPrefs.Save();

        Debug.Log("✅ Token refreshed");
        return true;
    }

    public void ClearTokens()
    {
        PlayerPrefs.DeleteKey(KeyAccessToken);
        PlayerPrefs.DeleteKey(KeyRefreshToken);
        AccessToken = "";
        RefreshToken = "";
    }
}

/// 서버 응답 DTO
[Serializable]
public class LoginGuestRes
{
    public string uid;
    public string accessToken;
    public string refreshToken;
    public long expiresIn;
}
*/
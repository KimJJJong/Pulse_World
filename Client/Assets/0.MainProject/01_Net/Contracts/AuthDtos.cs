using Newtonsoft.Json;

public static class AuthDtos
{
    public sealed class LoginGuestRequest
    {
        [JsonProperty("deviceId")] public string DeviceId;
        [JsonProperty("clientVersion")] public string ClientVersion;

        public LoginGuestRequest(string deviceId, string clientVersion)
        {
            DeviceId = deviceId;
            ClientVersion = clientVersion;
        }
    }

    public sealed class RefreshRequest
    {
        [JsonProperty("refreshToken")] public string RefreshToken;
        [JsonProperty("deviceId")] public string DeviceId;
        [JsonProperty("clientVersion")] public string ClientVersion;

        public RefreshRequest(string refreshToken, string deviceId, string clientVersion)
        {
            RefreshToken = refreshToken;
            DeviceId = deviceId;
            ClientVersion = clientVersion;
        }
    }

    public sealed class LogoutRequest
    {
        [JsonProperty("refreshToken")] public string RefreshToken;
        [JsonProperty("allDevices")] public bool AllDevices;

        public LogoutRequest(string refreshToken, bool allDevices)
        {
            RefreshToken = refreshToken;
            AllDevices = allDevices;
        }
    }

    // server: AuthResponse(Uid, AccessToken, AccessExpMs, RefreshToken, RefreshExpMs)
    public sealed class AuthResponse
    {
        [JsonProperty("uid")] public string Uid;
        [JsonProperty("accessToken")] public string AccessToken;
        [JsonProperty("accessExpMs")] public long AccessExpMs;
        [JsonProperty("refreshToken")] public string RefreshToken;
        [JsonProperty("refreshExpMs")] public long RefreshExpMs;
    }

    // server: RefreshResponse(AccessToken, AccessExpMs, RefreshToken, RefreshExpMs)
    public sealed class RefreshResponse
    {
        [JsonProperty("accessToken")] public string AccessToken;
        [JsonProperty("accessExpMs")] public long AccessExpMs;
        [JsonProperty("refreshToken")] public string RefreshToken;
        [JsonProperty("refreshExpMs")] public long RefreshExpMs;
    }
}

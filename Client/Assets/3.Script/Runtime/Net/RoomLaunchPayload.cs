public class RoomLaunchPayload
{
    public string WsUrl { get; private set; }
    public string Token { get; private set; }

    public string ClientVersion { get; private set; }

    public RoomLaunchPayload(string wsUrl, string token, string clientVersion)
    {
        WsUrl = wsUrl;
        Token = token;
        ClientVersion = clientVersion;
    }

    // 소비 후 민감정보 파기용(간단버전)
    public void Wipe()
    {
        WsUrl = null;
        Token = null;
    }
}

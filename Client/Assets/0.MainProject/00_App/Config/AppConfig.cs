using UnityEngine;

[CreateAssetMenu(menuName = "Project/AppConfig", fileName = "AppConfig")]
public sealed class AppConfig : ScriptableObject
{
    [Header("API")]
    public string BaseUrl = "http://127.0.0.1:5001"; // 네 API 서버
    public int TimeoutSeconds = 15;

    [Header("Client")]
    public string ClientVersion = "0.1.0";

    [Header("Steam")]
    public bool EnableSteam = true;
    public string SteamAppId = "480";
    public bool PreferSteamP2PInGame = true;
    public bool PreferSteamLogin = true;
    public string SteamId64Override = "";

    [Header("Debug")]
    public bool DisableDebugUis = false;
    public bool ShowSteamDebugHud = true;
}

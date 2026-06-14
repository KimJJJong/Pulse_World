using UnityEngine;

namespace NetClient.Room.UI
{
    public sealed class ApiClientProvider : MonoBehaviour
    {
        public ApiClient Api => AppBootstrap.Instance.Root.Api;
        public string Uid => SessionContext.Instance.Uid;   
        public string Name
        {
            get
            {
                var steamName = AppBootstrap.Instance.Root.SteamPlatform.DisplayName;
                if (!string.IsNullOrWhiteSpace(steamName))
                    return steamName;

                return "Guest";
            }
        }
        public string AccessToken => AppBootstrap.Instance.Root.Tokens.AccessToken;
        public string SteamId64 => AppBootstrap.Instance.Root.SteamPlatform.SteamId64;

        public string BuildRoomWsUrl(string roomId)
        {
            // Force IPv4 if localhost
            var baseHttp = Api.BaseUrl;
            var mkWs = baseHttp.Replace("http://", "ws://").Replace("https://", "wss://");
            
            var safeBase = mkWs.Replace("localhost", "127.0.0.1").TrimEnd('/');
            
            // Encode params
            var encUid = System.Uri.EscapeDataString(Uid);
            var encName = System.Uri.EscapeDataString(Name ?? "Unknown");
            var encToken = System.Uri.EscapeDataString(AccessToken);
            var encSteamId = System.Uri.EscapeDataString(SteamId64 ?? "");
            
            var url = $"{safeBase}/hub/room?roomId={roomId}&uid={encUid}&name={encName}&steamId64={encSteamId}&access_token={encToken}";
            
            Debug.Log($"[ApiClientProvider] Generated WS URL: {url}");
            return url;
        }
    }
}

using Contracts.Packet; // gen-contracts.bat 로 생성된 DTO들 (WireJson 사용)
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NetClient.Lobby
{
    /// <summary>
    /// AccessToken 기반 로비 HTTP 클라이언트 (자동 Refresh 지원)
    /// </summary>
    public sealed class LobbyApiClient
    {
        private readonly string _baseUrl;
        private readonly string _clientVersion;
        private string _etag; // GET /rooms 캐시용

        private string AccessToken
        {
            get => PlayerPrefs.GetString("ACCESS_TOKEN", "");
            set { PlayerPrefs.SetString("ACCESS_TOKEN", value); PlayerPrefs.Save(); }
        }
        public void SetAccessToken(string token)=> AccessToken = token;
        public string GetAccestToekn()
        {
            return AccessToken;
        }
        private string RefreshToken
        {
            get => PlayerPrefs.GetString("REFRESH_TOKEN", "");
            set { PlayerPrefs.SetString("REFRESH_TOKEN", value); PlayerPrefs.Save(); }
        }



        public LobbyApiClient(string baseUrl, string clientVersion)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _clientVersion = clientVersion;
        }

        // --------------------------------------------------------------
        // POST /auth/google    : login || callback
        // --------------------------------------------------------------
        public async Task<(bool ok, string Url, string resaon)> GoogleUrlRequest(/*string state*/)
        {
            var url = $"{_baseUrl}/auth/google/login";

            using var req = new UnityWebRequest(url, "GET");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Client-Version", _clientVersion);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            Debug.Log($"[GoogleLogin] responseCode={req.responseCode} body={req.downloadHandler.text}");

            if (req.result == UnityWebRequest.Result.Success)
            {
                var text = req.downloadHandler.text;
                if (string.IsNullOrEmpty(text))
                    return (false, null, "empty_response");

                var json = JsonUtility.FromJson<GoogleLoginRes>(text);

                string googleLoginPageUrl = json.googleUrl;
                
                return ( true ,googleLoginPageUrl, null );
            }

            return ( false, null, $"{req.responseCode}:{req.error}");
        }

        // --------------------------------------------------------------
        // POST /login/guest
        // --------------------------------------------------------------
        public async Task<(bool ok, string accessToken, string refreshToken, string err)> GuestLoginAsync(string deviceId = null)
        {
            Debug.Log($"[GuestLoginAsync] baseUrl = {_baseUrl}");

            var url = $"{_baseUrl}/auth/login/guest";
            //var body = WireJson.Serialize(new GuestLoginReq{ deviceId = deviceId ?? SystemInfo.deviceUniqueIdentifier });
            if (deviceId == null || deviceId.Length <= 2)
                deviceId = SystemInfo.deviceUniqueIdentifier;
            var reqDTO = new GuestLoginReq { deviceId = deviceId };
            var body = WireJson.Serialize(reqDTO);

            Debug.Log($"[GuestLoginReq JSON] {body}");

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Client-Version", _clientVersion);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var text = req.downloadHandler.text;
                if (string.IsNullOrEmpty(text))
                    return (false, null, null, "empty_response");

                var json = JsonUtility.FromJson<LoginRes>(text);

                AccessToken = json.accessToken;
                RefreshToken = json.refreshToken;
                Debug.Log($"[GuestLogin] ✅ OK — access=||| {AccessToken} |||");
                return (true, json.accessToken, json.refreshToken, null);
            }

            return (false, null, null, $"{req.responseCode}:{req.error}");
        }

        // --------------------------------------------------------------
        // POST /login/refresh
        // --------------------------------------------------------------
        private async Task<bool> TryRefreshAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                Debug.LogWarning("[Token] ❌ No refresh token, fallback to guest login");
                return await LoginAsGuestFallback();
            }

            var url = $"{_baseUrl}/login/refresh";
            var body = WireJson.Serialize(new { refreshToken = RefreshToken });

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Client-Version", _clientVersion);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var json = JsonUtility.FromJson<LoginRes>(req.downloadHandler.text);
                AccessToken = json.accessToken;
                RefreshToken = json.refreshToken;
                Debug.Log("[Token] 🔄 AccessToken refreshed");
                return true;
            }

            Debug.LogWarning($"[Token] ⚠ Refresh failed ({req.responseCode}), fallback to guest login");
            return await LoginAsGuestFallback();
        }

        private async Task<bool> LoginAsGuestFallback()
        {
            var (ok, access, refresh, _) = await GuestLoginAsync();
            return ok;
        }

        // --------------------------------------------------------------
        // GET /rooms
        // --------------------------------------------------------------
        public async Task<(bool ok, GetRoomsRes data, bool notModified, string err)> GetRoomsAsync(int pageSize = 50)
        {
            var url = $"{_baseUrl}/rooms?pageSize={Mathf.Clamp(pageSize, 1, 100)}";

            async Task<(bool ok, GetRoomsRes data, bool notModified, string err)> DoRequest()
            {
                using var req = UnityWebRequest.Get(url);
                req.SetRequestHeader("X-Client-Version", _clientVersion);
                if (!string.IsNullOrEmpty(_etag))
                    req.SetRequestHeader("If-None-Match", _etag);
                if (!string.IsNullOrEmpty(AccessToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if ((int)req.responseCode == 304)
                    return (true, null, true, null);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    _etag = req.GetResponseHeader("ETag") ?? _etag;
                    var data = WireJson.Deserialize<GetRoomsRes>(req.downloadHandler.text);
                    return (true, data, false, null);
                }

                if ((int)req.responseCode == 401)
                {
                    // 🔁 자동 토큰 재발급 후 재시도
                    if (await TryRefreshAsync())
                        return await DoRequest();
                }

                return (false, null, false, $"{req.responseCode}:{req.downloadHandler.text}");
            }

            return await DoRequest();
        }
        // --------------------------------------------------------------
        // Post /session/ticket/town
        // --------------------------------------------------------------
        public async Task<TownTicketRes?> TryGetTownTicketAsync()
        {
            if (string.IsNullOrEmpty(AccessToken))
            {
                Debug.LogWarning("[TownTicket] ❌ No access token");
                return null;
            }

            var url = $"http://localhost:5290/session/ticket/town";

            // 서버가 body를 안 받는다면 {}로 보내면 됨
            var body = WireJson.Serialize(new { });

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Client-Version", _clientVersion);

            // Bearer
            req.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // JsonUtility는 top-level object만 잘 먹음(배열/딕셔너리 주의)
                var jsonText = req.downloadHandler.text;
                var res = JsonUtility.FromJson<TownTicketRes>(jsonText);

                if (res == null || string.IsNullOrEmpty(res.ticketId))
                {
                    Debug.LogWarning($"[TownTicket] ⚠ Invalid response: {jsonText}");
                    return null;
                }

                Debug.Log($"[TownTicket] ✅ ticketId={res.ticketId} exp={res.expireAtMs} town={res.host}:{res.port}");
                return res;
            }

            // 인증 문제면 refresh 시도 → 재시도 패턴을 여기서 넣을 수도 있음
            Debug.LogWarning($"[TownTicket] ⚠ Failed ({req.responseCode}) {req.error} body={req.downloadHandler.text}");
            return null;
        }
        [Serializable]
        public class TownTicketRes
        {
            public string ticketId;
            public long expireAtMs;
            public string host;
            public int port;
        }


        // --------------------------------------------------------------
        // POST /rooms
        // --------------------------------------------------------------
        public async Task<(bool ok, CreateRoomRes data, string err)> CreateRoomAsync(string title, string map = "forest01", int max = 2, string visibility = "Public")
        {
            Debug.Log($"[CreateRoomAsync] baseUrl = {_baseUrl}");

            var url = $"{_baseUrl}/rooms";
            var body = WireJson.Serialize(new CreateRoomReq
            {
                title = title,
                map = map,
                max = max,
                visibility = visibility
            });

            async Task<(bool ok, CreateRoomRes data, string err)> DoRequest()
            {
                using var req = new UnityWebRequest(url, "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Client-Version", _clientVersion);
                if (!string.IsNullOrEmpty(AccessToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = WireJson.Deserialize<CreateRoomRes>(req.downloadHandler.text);
                    return (true, res, null);
                }

                if ((int)req.responseCode == 401)
                {
                    if (await TryRefreshAsync())
                        return await DoRequest();
                }

                return (false, null,
                    $"result={req.result}, code={req.responseCode}, err={req.error}, body={req.downloadHandler.text}");
            }

            return await DoRequest();
        }

        // --------------------------------------------------------------
        // POST /rooms/{id}/join
        // --------------------------------------------------------------
        public async Task<(bool ok, JoinRoomRes data, string err)> JoinRoomAsync(string roomId)
        {
            var url = $"{_baseUrl}/rooms/{roomId}/join";

            async Task<(bool ok, JoinRoomRes data, string err)> DoRequest()
            {
                using var req = new UnityWebRequest(url, "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Client-Version", _clientVersion);
                if (!string.IsNullOrEmpty(AccessToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = WireJson.Deserialize<JoinRoomRes>(req.downloadHandler.text);
                    return (true, res, null);
                }

                if ((int)req.responseCode == 401)
                {
                    if (await TryRefreshAsync())
                        return await DoRequest();
                }

                return (false, null, $"{req.responseCode}:{req.downloadHandler.text}");
            }

            return await DoRequest();
        }

        // --------------------------------------------------------------
        // 내부 DTO
        // --------------------------------------------------------------
        /*        [Serializable]
                private sealed class LoginResJson
                {
                    public string accessToken;
                    public string refreshToken;
                    public string refreshExpiresAt;
                }*/
        [Serializable]
        public sealed class IssueTownTicketResponse {
            string TicketId;
            long ExpireAtMs;
            //EndpointDto Endpoint;
        }
        //public sealed record EndpointDto(string Host, int Port);

    }
}

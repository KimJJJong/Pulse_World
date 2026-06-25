using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class ApiClient
{
    public static bool VerboseLogging { get; set; } = false;

    readonly string _baseUrl;
    readonly int _timeoutSeconds;
    readonly TokenStore _tokens;
    readonly ClientIdentity _identity;   // DeviceId/ClientVersion 공급
    bool _refreshing;
    static string NewIdemKey() => System.Guid.NewGuid().ToString("N");

    public ApiClient(string baseUrl, int timeoutSeconds, TokenStore tokens, ClientIdentity identity)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _timeoutSeconds = Mathf.Max(3, timeoutSeconds);
        _tokens = tokens;
        _identity = identity;
    }

    public string BaseUrl => _baseUrl;

    public Task<ApiResult<T>> GetJsonAsync<T>(string path, bool attachAuth, string etag = null)
    {
        return SendWithAuthRetryAsync<T>("GET", path, null, attachAuth, null, etag);
    }

    public Task<ApiResult<T>> PostJsonAsync<T>(string path, object body, bool attachAuth)
    {
        // /session/ticket/game 에만 적용 (서버 미들웨어와 동일 규칙)
        string? idemKey = string.Equals(path, "/session/ticket/game", StringComparison.OrdinalIgnoreCase)
            ? NewIdemKey()
            : null;

        return SendWithAuthRetryAsync<T>("POST", path, body, attachAuth, idemKey);
    }

    async Task<ApiResult<T>> SendWithAuthRetryAsync<T>(
     string method, string path, object body, bool attachAuth, string? idempotencyKey, string? etag = null)
    {
        var first = await SendOnceAsync<T>(method, path, body, attachAuth, idempotencyKey, etag);

        if (attachAuth && first.StatusCode == 401)
        {
            var ok = await TryRefreshOnceAsync();
            if (ok)
                return await SendOnceAsync<T>(method, path, body, attachAuth, idempotencyKey, etag);
        }

        return first;
    }


    async Task<ApiResult<T>> SendOnceAsync<T>(
        string method, string path, object body, bool attachAuth, string? idempotencyKey =null, string? etag = null)
    {
        var url = _baseUrl + path;

        var req = new UnityWebRequest(url, method);
        
        if (body != null)
        {
            var json = JsonConvert.SerializeObject(body);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.SetRequestHeader("Content-Type", "application/json");
            LogVerbose($"[HTTP][REQ] {method} {url}\nBody: {json}\nIdemKey: {idempotencyKey}");
        }
        else 
        {
            LogVerbose($"[HTTP][REQ] {method} {url}\nIdemKey: {idempotencyKey}");
        }

        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = _timeoutSeconds;

        if (attachAuth && _tokens.HasAccessToken)
            req.SetRequestHeader("Authorization", $"Bearer {_tokens.AccessToken}");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            req.SetRequestHeader("Idempotency-Key", idempotencyKey);
            
        if (!string.IsNullOrWhiteSpace(etag))
            req.SetRequestHeader("If-None-Match", etag);

        if (!await WaitForRequestAsync(req, _timeoutSeconds))
        {
            var msg = $"요청 시간이 초과되었습니다. ({_timeoutSeconds}초)";
            Debug.LogWarning($"[HTTP][TIMEOUT] {method} {url} -> {msg}");
            return new ApiResult<T>(false, 0, msg, default);
        }

        var code = (int)req.responseCode;
        var respText = req.downloadHandler?.text ?? "";
        
        // 304 Handling
        if (code == 304)
        {
            LogVerbose($"[HTTP][RESP] {method} {url} -> 304 Not Modified");
            return new ApiResult<T>(true, code, "Not Modified", default);
        }

        if (req.result == UnityWebRequest.Result.Success && code >= 200 && code < 300)
            LogVerbose($"[HTTP][RESP] {method} {url} -> {code} result={req.result}\nBody: {respText}");
        else
            Debug.LogWarning($"[HTTP][RESP] {method} {url} -> {code} result={req.result}\nBody: {respText}");

        if (req.result != UnityWebRequest.Result.Success)
        {
            var msg = req.result switch
            {
                UnityWebRequest.Result.ConnectionError => "서버에 연결할 수 없어요. 인터넷/서버 상태를 확인해주세요.",
                UnityWebRequest.Result.ProtocolError => $"요청 실패 (HTTP {code})\n{respText}",
                UnityWebRequest.Result.DataProcessingError => "응답 처리 중 오류가 발생했어요.",
                _ => "알 수 없는 네트워크 오류가 발생했어요."
            };
            return new ApiResult<T>(false, code, msg, default);
        }

        if (code == 204)
            return new ApiResult<T>(true, code, "", default);

        if (code < 200 || code >= 300)
            return new ApiResult<T>(false, code, $"요청 실패 (HTTP {code})\n{respText}", default);

        try
        {
            var data = JsonConvert.DeserializeObject<T>(respText);
            // ETag capture if needed? ApiResult doesn't have ETag field. 
            // We might need to expose headers or just return data.
            // For now, RoomListApiClient needs ETag to save it. 
            // BUT ApiResult <T> wrapper hides headers. 
            // I should assume the caller only needs data for now, OR I need to extend ApiResult.
            // Wait, RoomListApiClient needs to SAVE the ETag from response.
            // ApiClient currently doesn't return headers.
            // I should update ApiResult to include ETag or Headers.
            return new ApiResult<T>(true, code, "", data) { ETag = req.GetResponseHeader("ETag") }; 
        }
        catch
        {
            return new ApiResult<T>(false, code, "서버 응답 형식이 예상과 달라요.", default);
        }
    }

    static void LogVerbose(string message)
    {
        if (VerboseLogging)
            Debug.Log(message);
    }

    static async Task<bool> WaitForRequestAsync(UnityWebRequest req, int timeoutSeconds)
    {
        var operation = req.SendWebRequest();
        var deadline = Time.realtimeSinceStartup + Mathf.Max(3, timeoutSeconds);

        while (!operation.isDone)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                req.Abort();
                return false;
            }

            await Task.Yield();
        }

        return true;
    }



    async Task<bool> TryRefreshOnceAsync()
    {
        if (!_tokens.HasRefreshToken)
            return false;

        // 동시 refresh 방지
        if (_refreshing) return true;
        _refreshing = true;

        try
        {
            var req = new AuthDtos.RefreshRequest(
                _tokens.RefreshToken,
                _identity.DeviceId,
                _identity.ClientVersion
            );

            var r = await SendOnceAsync<AuthDtos.RefreshResponse>("POST", "/auth/refresh", req, attachAuth: false);
            if (!r.Ok)
            {
                _tokens.Clear();
                return false;
            }

            _tokens.SaveFromRefresh(
                r.Data.AccessToken, r.Data.AccessExpMs,
                r.Data.RefreshToken, r.Data.RefreshExpMs
            );

            return true;
        }
        finally
        {
            _refreshing = false;
        }
    }
}

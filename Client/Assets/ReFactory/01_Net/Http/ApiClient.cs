using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public sealed class ApiClient
{
    readonly string _baseUrl;
    readonly int _timeoutSeconds;
    readonly TokenStore _tokens;
    readonly ClientIdentity _identity;   // DeviceId/ClientVersion 공급
    bool _refreshing;

    public ApiClient(string baseUrl, int timeoutSeconds, TokenStore tokens, ClientIdentity identity)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _timeoutSeconds = Mathf.Max(3, timeoutSeconds);
        _tokens = tokens;
        _identity = identity;
    }

    public Task<ApiResult<T>> PostJsonAsync<T>(string path, object body, bool attachAuth)
        => SendWithAuthRetryAsync<T>("POST", path, body, attachAuth);

    async Task<ApiResult<T>> SendWithAuthRetryAsync<T>(string method, string path, object body, bool attachAuth)
    {
        var first = await SendOnceAsync<T>(method, path, body, attachAuth);

        if (attachAuth && first.StatusCode == 401)
        {
            var ok = await TryRefreshOnceAsync();
            if (ok)
                return await SendOnceAsync<T>(method, path, body, attachAuth);
        }

        return first;
    }

    async Task<ApiResult<T>> SendOnceAsync<T>(string method, string path, object body, bool attachAuth)
    {
        var url = _baseUrl + path;

        var json = JsonConvert.SerializeObject(body);
        var req = new UnityWebRequest(url, method);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = _timeoutSeconds;

        req.SetRequestHeader("Content-Type", "application/json");

        if (attachAuth && _tokens.HasAccessToken)
            req.SetRequestHeader("Authorization", $"Bearer {_tokens.AccessToken}");

        await req.SendWebRequest();

        var code = (int)req.responseCode;

        if (req.result != UnityWebRequest.Result.Success)
        {
            var msg = req.result switch
            {
                UnityWebRequest.Result.ConnectionError => "서버에 연결할 수 없어요. 인터넷/서버 상태를 확인해주세요.",
                UnityWebRequest.Result.ProtocolError => $"요청 실패 (HTTP {code})",
                UnityWebRequest.Result.DataProcessingError => "응답 처리 중 오류가 발생했어요.",
                _ => "알 수 없는 네트워크 오류가 발생했어요."
            };
            return new ApiResult<T>(false, code, msg, default);
        }

        // 204 NoContent 처리
        if (code == 204)
            return new ApiResult<T>(true, code, "", default);

        var text = req.downloadHandler.text ?? "";
        if (code < 200 || code >= 300)
            return new ApiResult<T>(false, code, $"요청 실패 (HTTP {code})", default);

        try
        {
            var data = JsonConvert.DeserializeObject<T>(text);
            return new ApiResult<T>(true, code, "", data);
        }
        catch
        {
            return new ApiResult<T>(false, code, "서버 응답 형식이 예상과 달라요.", default);
        }
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

using Google.Apis.Auth;
using Lobby.Api.Config;
using Contracts.Packet;


using Lobby.Domain.Auth.Interface;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;


namespace Lobby.Domain.Auth.Services;

    public class GoogleAuthService : IGoogleAuthService
    {
       private readonly HttpClient _http; 
       private readonly GoogleAuthOptions _opt;
       
        public GoogleAuthService(HttpClient http, IOptions<GoogleAuthOptions> opt)
        {
            _http = http;
            _opt = opt.Value;       
        }

        public async Task<GoogleUserInfo?> VerifyAsync(string idToken)
        {
            GoogleJsonWebSignature.Payload payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
            return new GoogleUserInfo
            {
                Sub = payload.Subject,
                Email = payload.Email,
                Name = payload.Name
            };
        }

        // 로그인 페이지 URL ( Authorization URL )
        public string GetAuthUrl(string? state = null)  // state 구현은 Dev이후 진행 : 보안 관련
        {
            var qs = new Dictionary<string, string?>
            {
                ["client_id"] = _opt.ClientId,
                ["redirect_uri"] = _opt.RedirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["access_type"] = "offline",
                ["include_granted_scopes"] = "true",
                ["prompt"] = "consent",
                ["state"] = state
            };
        //Console.WriteLine("Send URL Requ");
        return QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", qs);
        }

        // Authorzation Code -> Token Exchange
        public async Task<GoogleTokenResponse> ExchangeCodeAsync(string code)
        {
        
        var data = new Dictionary<string, string?>
            {
                ["code"] = code,
                ["client_id"] = _opt.ClientId,
                ["client_secret"] = _opt.ClientSecret,
                ["redirect_uri"] = _opt.RedirectUri,
                ["grant_type"] = "authorization_code"
            };
        var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(data)
        };

        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opt.ClientId}:{_opt.ClientSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        //Console.WriteLine($"[GoogleAuth] Token 교환 원문: {body}");

        if (!res.IsSuccessStatusCode)
        {
            Console.Write($"[GoogleAuth] Token 교환 실패: {res.StatusCode} {body}");
            throw new HttpRequestException($"Google token exchange failed ({res.StatusCode}): {body}");
        }

        var json = await res.Content.ReadAsStringAsync();
        //Console.WriteLine("[RAW JSON] " + json);

        var settings = new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                NamingStrategy = new Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy()
            }
        };

        var token = JsonConvert.DeserializeObject<GoogleTokenResponse>(json, settings);
        //Console.WriteLine($"[DEBUG] AccessToken={token?.AccessToken}, RefreshToken={token?.RefreshToken}, IdToken={token?.IdToken}");
        return token!;

    }

        // AccessToekn(Google Provide) -> Google User Info : user만들때 참조 값
 public async Task<GoogleUserInfo> GetUserInfoAsync(string googleAccessToken)
{
    var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", googleAccessToken);

    var res = await _http.SendAsync(req);
    var body = await res.Content.ReadAsStringAsync();

    if (!res.IsSuccessStatusCode)
    {
            Console.WriteLine("[GoogleAuth] UserInfo 요청 실패: {res.StatusCode} {body}");
        throw new HttpRequestException($"UserInfo failed ({res.StatusCode}): {body}");
    }

    return JsonConvert.DeserializeObject<GoogleUserInfo>(body)!;
}



    }




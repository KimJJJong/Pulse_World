using Lobby.Domain.Auth.Services;
using Lobby.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Mvc;
using Lobby.Domain.Auth.Interface;
using Google.Apis.Auth;
using Contracts.Packet;
using System.Text.Json;


namespace Lobby.Api.Controllers;

[ApiController]
[Route("auth/google")]
public class GoogleAuthController : ControllerBase
{
    IJwtService _jwt;
    private readonly IGoogleAuthService _googleAuth;
    private readonly IRefreshTokenService _refresh;
    private readonly IUserRepository _users;
    private readonly ILogger<GoogleAuthController> _logger;

    public GoogleAuthController( IJwtService jwt, IGoogleAuthService googleAuth, IUserRepository users, IRefreshTokenService refresh, ILogger<GoogleAuthController> logger)
    {
        _jwt = jwt;
        _googleAuth = googleAuth;
        _refresh = refresh;
        _users = users;
        _logger = logger;
    }

    // 클라 호출 -> Lobby -> GoogleAuth -AuthURL> Lobby -AuthURL> 클라
    [HttpGet("login")]
    public IActionResult Login()
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["endpoint"] = "google",
            ["ip"] = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ["ua"] = Request.Headers.UserAgent.ToString(),
        });

        var state = "";//Guid.NewGuid().ToString("N"); : 일단 사용 X

        var url = _googleAuth.GetAuthUrl(state);

        _logger.LogInformation("GoogleAuth URL 생성 완료: {Url}", url);

        return Ok(new GoogleLoginRes { GoogleUrl = url });
    }


    // 클라에 내려준 AuthURL redirection CallBack(로그인 완료 후 code 수신)
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["endpoint"] = "google_callback",
            ["ip"] = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ["ua"] = Request.Headers.UserAgent.ToString(),
        });

        _logger.LogInformation("GoogleAuth Callback 수신: code={Code}", code/*, state*/);
        try
        {

            var googleToken = await _googleAuth.ExchangeCodeAsync(code);

            var info = await _googleAuth.GetUserInfoAsync(googleToken.AccessToken);

            await _users.UpsertGoogleAsync(info.Sub, $"Google_{info.Sub[..6]}");

            var (accesstoken, expireIn, jti) = _jwt.IssueAccessToken(info.Sub, new Dictionary<string, object>(), TimeSpan.FromMinutes(30));

            var (refreshToken, familyId, reFreshExpiresAt) = await _refresh.IssueAsync(info.Sub, null,
                 HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.ToString());   // 구조 변경 accessToken, reFreshToken 각각 Issue? or 통합
            return Ok(new LoginRes
            {
                AccessToken = accesstoken,
                AccessExpiresAt = new DateTimeOffset(expireIn).ToUnixTimeSeconds(),
                RefreshToken =refreshToken,
                RefreshExpiresAt = reFreshExpiresAt.ToUnixTimeSeconds(),
                User = new GoogleUserInfo { Sub = info.Sub, Email = info.Email, Name =info.Name }
                
            });

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleAuth] Callback Error: {ex}");
            return BadRequest(new { error = "google_auth_failed", message = ex.Message });
        }


    }
}


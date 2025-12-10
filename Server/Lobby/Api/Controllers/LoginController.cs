using Lobby.Domain.Auth.Interface;
using Microsoft.AspNetCore.Mvc;

using Contracts.Packet;

namespace Lobby.Api.Http;

[ApiController]
[Route("login")]
public sealed class LoginController : ControllerBase
{
    private readonly IGoogleAuthService _googleAuth;
    private readonly IJwtService _jwt;
    private readonly IRefreshTokenService _refresh;
    private readonly IUserRepository _users;
    private readonly ILogger<LoginController> _logger;

    public LoginController( IGoogleAuthService googleAuth, IJwtService jwt, IRefreshTokenService refresh, IUserRepository users, ILogger<LoginController> logger)
    {
        _googleAuth = googleAuth;
        _jwt = jwt;
        _refresh = refresh;
        _users = users;
        _logger = logger;
    }

    // -------------------------
    // Guest 로그인 
    // -------------------------
    [HttpPost("guest")]
    public async Task<IActionResult> GuestLogin([FromBody] GuestLoginReq req)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["endpoint"] = "guest",
            ["ip"] = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ["ua"] = Request.Headers.UserAgent.ToString(),
        });

        string userId = req.DeviceId ?? Guid.NewGuid().ToString();
        string userName = $"Guest_{userId[..6]}";

        _logger.LogInformation("GuestLogin 요청 수신: userId={User}", userId);

        try
        {
            await _users.UpsertGuestAsync(userId, userName);

            // AccessToken 발급
            var (accesstoken, expireIn, jti) = _jwt.IssueAccessToken(userId, new Dictionary<string, object>(), TimeSpan.FromMinutes(30));

            // RefreshToken 발급 (userId = string)
            var (refreshToken, familyId, reFreshExpiresAt) =
                await _refresh.IssueAsync(userId, null,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.ToString());

            _logger.LogInformation("GuestLogin 성공: userId={User}, family={Family}, exp={Exp}", userId, familyId, expireIn);

            return Ok(new LoginRes
            {
                AccessToken = accesstoken,
                AccessExpiresAt = new DateTimeOffset(expireIn).ToUnixTimeSeconds(),
                RefreshToken = refreshToken,
                RefreshExpiresAt = reFreshExpiresAt.ToUnixTimeSeconds(),
                User = new GoogleUserInfo { Sub = "Guest", Email = "Guest", Name = "Guest" }

            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GuestLogin 처리 중 예외 발생: userId={User}", userId);
            return StatusCode(500, new { code = "server_error", message = "Guest login failed" });
        }
    }

    // -------------------------
    // Google 로그인
    // -------------------------
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginReq req)
    {
        if (string.IsNullOrWhiteSpace(req.GoogleSub))
            return BadRequest(new { code = "empty_google_sub" });

        var info = await _googleAuth.VerifyAsync(req.GoogleSub);
        if (info == null)
            return Unauthorized("invalid_google_token");

        _logger.LogInformation("GoogleLogin 요청: sub={Sub}", req.GoogleSub);

        try
        {
            await _users.UpsertGoogleAsync(req.GoogleSub, $"Google_{req.GoogleSub[..6]}");

            var (accesstoken, expireIn, jti) = _jwt.IssueAccessToken(req.GoogleSub, new Dictionary<string, object>(), TimeSpan.FromMinutes(30));

            var (refreshToken, familyId, reFreshExpiresAt) =
                await _refresh.IssueAsync(req.GoogleSub, null,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.ToString());

            _logger.LogInformation("GoogleLogin 성공: sub={Sub}, family={Family}", req.GoogleSub, familyId);

            return Ok(new LoginRes
            {
                AccessToken = accesstoken,
                AccessExpiresAt = new DateTimeOffset(expireIn).ToUnixTimeSeconds(),
                RefreshToken = refreshToken,
                RefreshExpiresAt = reFreshExpiresAt.ToUnixTimeSeconds(),
                User = new GoogleUserInfo { Sub = info.Sub, Email = info.Email, Name = info.Name }

            });

 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoogleLogin 처리 중 예외 발생: sub={Sub}", req.GoogleSub);
            return StatusCode(500, new { code = "server_error", message = "Google login failed" });
        }
    }

    // -------------------------
    // 토큰 재발급
    // -------------------------
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        _logger.LogInformation("RefreshToken 요청 수신");

        try
        {
            var valid = await _refresh.ValidateAsync(req.RefreshToken);
            if (valid is null)
            {
                _logger.LogWarning("RefreshToken 검증 실패");
                return Unauthorized(new { code = "invalid_refresh" });
            }

            var (_, userId, tokenId, familyId, _) = valid.Value;
            _logger.LogInformation("RefreshToken 유효: user={User}, family={Family}", userId, familyId);

            var access = _jwt.IssueAccessToken(userId, new Dictionary<string, object>(), TimeSpan.FromMinutes(30));

            var (newRefresh, _, expiresAt) =
                await _refresh.RotateAsync(userId, tokenId, familyId,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.ToString());

            return Ok(new
            {
                accessToken = access.token,
                refreshToken = newRefresh,
                refreshExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh 처리 중 예외 발생");
            return StatusCode(500, new { code = "server_error", message = "Refresh failed" });
        }
    }

    // -------------------------
    // 로그아웃
    // -------------------------
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest req)
    {
        try
        {
            await _refresh.RevokeAsync(req.RefreshToken, "user_logout");
            _logger.LogInformation("Logout 성공: RefreshToken 폐기 완료");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout 처리 중 예외 발생");
            return StatusCode(500, new { code = "server_error", message = "Logout failed" });
        }
    }
}

// ---------------- DTO ----------------
//public record GuestLoginRequest(string? DeviceId);
//public record GoogleLoginRequest(string GoogleSub);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

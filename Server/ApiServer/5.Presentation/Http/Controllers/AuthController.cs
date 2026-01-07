using ApiServer.Application.Auth.LoginGuest;
using ApiServer.Application.Auth.Logout;
using ApiServer.Application.Auth.Refresh;
using ApiServer.Presentation.Http.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("login/guest")]
    public async Task<ActionResult<AuthDtos.AuthResponse>> LoginGuest(
        [FromServices] LoginGuestHandler handler,
        [FromBody] AuthDtos.LoginGuestRequest req,
        CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();

        var result = await handler.HandleAsync(
            new LoginGuestCommand(req.DeviceId, req.ClientVersion, ip, ua),
            ct);

        return Ok(new AuthDtos.AuthResponse(
            Uid: result.Uid,
            AccessToken: result.AccessToken,
            AccessExpMs: result.AccessExp.ToUnixTimeMilliseconds(),
            RefreshToken: result.RefreshToken,
            RefreshExpMs: result.RefreshExp.ToUnixTimeMilliseconds()
        ));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthDtos.RefreshResponse>> Refresh(
        [FromServices] RefreshHandler handler,
        [FromBody] AuthDtos.RefreshRequest req,
        CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();

        var result = await handler.HandleAsync(
            new RefreshCommand(req.RefreshToken, req.DeviceId, req.ClientVersion, ip, ua),
            ct);

        return Ok(new AuthDtos.RefreshResponse(
            AccessToken: result.AccessToken,
            AccessExpMs: result.AccessExp.ToUnixTimeMilliseconds(),
            RefreshToken: result.RefreshToken,
            RefreshExpMs: result.RefreshExp.ToUnixTimeMilliseconds()
        ));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromServices] LogoutHandler handler,
        [FromBody] AuthDtos.LogoutRequest req,
        CancellationToken ct)
    {
        await handler.HandleAsync(new LogoutCommand(req.RefreshToken, req.AllDevices), ct);
        return NoContent();
    }
}

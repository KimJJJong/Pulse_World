using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ControlPlane.Grpc.V1;
using Shared;
[ApiController]
[Route("session/ticket")]
public sealed class SessionTicketController : ControllerBase
{
    private readonly ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient _cp;
    private readonly ITownEndpointResolver _townResolver;

    // 운영값: 짧게(예: 10~30초) 추천
    private const int DefaultTtlSeconds = 15;
    private const int MaxTtlSeconds = 60;

    public SessionTicketController(
        ControlPlane.Grpc.V1.ControlPlane.ControlPlaneClient cp,
        ITownEndpointResolver townResolver)
    {
        _cp = cp;
        _townResolver = townResolver;
    }

    [HttpPost("town")]
    //[Authorize] // AccessToken 필요 // DTO 생성기에서 관리 필요
    public async Task<ActionResult<PostTownTicketResponse>> PostTownTicket(
        [FromBody] PostTownTicketRequest? body,
        CancellationToken ct)
    {
        // 1) uid 추출 (너 프로젝트의 claim 키에 맞춰 바꿔)
        var uid = GetUidOrThrow(User);

        // 2) TTL 정규화
        int ttl = body?.TtlSeconds ?? DefaultTtlSeconds;
        if (ttl <= 0) ttl = DefaultTtlSeconds;
        if (ttl > MaxTtlSeconds) ttl = MaxTtlSeconds;

        // 3) API가 Town endpoint 결정 (CP는 endpoint 모름)
        var (pickedServerId, host, port) = _townResolver.Resolve(body?.PreferredServerId);

        // 4) CP에 Ticket 발급 요청
        //    - target=TOWN
        //    - key="" (Town은 roomId 같은 key가 없으면 공백)
        //    - preferred_server_id = pickedServerId (Town이 여러대면 pinning 목적)
        var resp = await _cp.IssueTicketAsync(new IssueTicketRequest
        {
            Uid = uid,
            Target = TicketTarget.Town,
            Key = "", // town은 보통 key 없음
            PreferredServerId = pickedServerId ?? "",
            TtlSeconds = ttl
        }, cancellationToken: ct);

        // 5) 응답 구성 (endpoint는 API가 내려줌)
        var outResp = new PostTownTicketResponse(
            TicketId: resp.TicketId,
            ExpireAtMs: resp.ExpireAtMs,
            TownHost: host,
            TownPort: port,
            ServerId: string.IsNullOrWhiteSpace(resp.ServerId) ? pickedServerId : resp.ServerId,
            Key: resp.Key ?? ""
        );

        return Ok(outResp);
    }

    private static string GetUidOrThrow(ClaimsPrincipal user)
    {
        // 예시:
        // - ClaimTypes.NameIdentifier
        // - "uid"
        // - "sub"
        // 너 토큰 발급 코드에 맞춰 고정해야 함.
        var uid =
            user.FindFirstValue("uid") ??
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(uid))
            throw new UnauthorizedAccessException("uid claim missing in access token.");

        return uid;
    }
}

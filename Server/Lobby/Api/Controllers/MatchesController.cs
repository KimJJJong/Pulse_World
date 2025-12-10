using Lobby.Api.Config;
using Lobby.Infrastructure.Persistence.Redis;   // GS 디렉터리/매치 스토어
using Lobby.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Lobby.Api.Http;

[ApiController]
[Route("api/[controller]")]
public sealed class MatchesController : ControllerBase
{
    private readonly RedisGameServerDirectory _dir;
    private readonly RedisMatchStore _store;
    private readonly TicketIssuer _issuer;
    private readonly AppOptions _opt; //  protoVer/TTL 읽기용

    public MatchesController(
        RedisGameServerDirectory dir,
        RedisMatchStore store,
        TicketIssuer issuer,
        IOptions<AppOptions> opt)
    {
        _dir = dir;
        _store = store;
        _issuer = issuer;
        _opt = opt.Value;
    }

    [HttpPost("issue")]
    public async Task<IActionResult> Issue([FromBody] IssueReq req)
    {
        // 1) GS 선택/예약
        var gs = await _dir.PickAndReserveAsync();

        // 2) 매치 메타 저장 (옵션: protoVer/host/port 기록)
        var tag ="{"+req.MatchId+"}";
        await _store.CreateAsync(req.MatchId, gs.Id, req.RoomId, req.UidA, req.UidB);
        await _store.SetFieldsAsync(req.MatchId, new Dictionary<string, string>
        {
            ["gsHost"] = gs.Host,
            ["gsPort"] = gs.Port.ToString(),
            ["tickRate"] = gs.TickRate.ToString(),
            ["protoVer"] = _opt.Versioning.ProtoVer.ToString(), // 기록(관측/검증용)
            ["status"] = "issued"
        });

        // 3) 티켓 발급 
        var ttl = TimeSpan.FromSeconds(_opt.Ticket.TtlSeconds);
        var (toA, toB) = _issuer.IssueStartTickets(
            req.MatchId, req.RoomId, (req.UidA, "A"), (req.UidB, "B"),
            gs.Host, gs.Port, gs.TickRate, ttl,
            proto: _opt.Versioning.ProtoVer  
        );

        // 4) 응답 (protoVer 포함 → 클라 즉시 사용 가능)
        return Ok(new
        {
            gs = new
            {
                gs.Host,
                gs.Port,
                gs.TickRate,
                protoVer = _opt.Versioning.ProtoVer,         
            },
            toA, // { uid, token }
            toB  // { uid, token }
        });
    }
}

public record IssueReq(string MatchId, string RoomId, string UidA, string UidB);

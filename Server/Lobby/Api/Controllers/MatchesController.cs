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
    private readonly TicketIssuer _issuer;   // 실제 구현체 (ITicketIssuer 구현)
    private readonly AppOptions _opt;        // protoVer/TTL 읽기용

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
        // 1) GameServer 선택 + 예약
        //    - RedisGameServerDirectory가 현재 사용 가능한 GS 중 하나를 골라서
        //      "이 매치는 여기서 돌릴게요" 라고 예약하는 단계
        var gs = await _dir.PickAndReserveAsync();

        // 2) 매치 메타 저장 (RedisMatchStore)
        //    - match:{matchId} 키에 GS, Room, 참가자 정보 기록
        //    - 여기서는 아직 A/B 이름을 쓰지만, 논리적으로는 slot 0/1
        await _store.CreateAsync(req.MatchId, gs.Id, req.RoomId, req.UidA, req.UidB);
        await _store.SetFieldsAsync(req.MatchId, new Dictionary<string, string>
        {
            ["gsHost"] = gs.Host,
            ["gsPort"] = gs.Port.ToString(),
            ["tickRate"] = gs.TickRate.ToString(),
            ["protoVer"] = _opt.Versioning.ProtoVer.ToString(), // 관측/검증용
            ["status"] = "issued"
        });

        // 3) 티켓 발급 (slot 기반, 다인 지원 TicketIssuer 사용)
        //    - 지금은 2인 매치라서 slot 0/1만 사용
        var players = new List<(string uid, int slot)>
        {
            (req.UidA, 0),
            (req.UidB, 1)
        };

        var ttl = TimeSpan.FromSeconds(_opt.Ticket.TtlSeconds);

        // TicketIssuer.IssueStartTickets(
        //     matchId, roomId, players( uid + slot ),
        //     gsHost, gsPort, tickRate, ttl, protoVer )
        var tickets = _issuer.IssueStartTickets(
            req.MatchId,
            req.RoomId,
            players,
            gs.Host,
            gs.Port,
            gs.TickRate,
            ttl,
            proto: _opt.Versioning.ProtoVer
        );

        // slot 0/1 기준으로 다시 꺼내기
        var t0 = tickets.First(t => t.slot == 0);
        var t1 = tickets.First(t => t.slot == 1);

        // 4) HTTP 응답
        //    - gs: 접속해야 할 GameServer 정보
        //    - toA/toB: 각 플레이어가 사용할 start ticket
        //      (uid, slot, token 을 함께 내려줘서 클라에서 slot 그대로 사용 가능)
        return Ok(new
        {
            gs = new
            {
                gs.Host,
                gs.Port,
                gs.TickRate,
                protoVer = _opt.Versioning.ProtoVer,
            },
            toA = new
            {
                uid = t0.uid,
                slot = t0.slot,
                token = t0.ticket.token
            },
            toB = new
            {
                uid = t1.uid,
                slot = t1.slot,
                token = t1.ticket.token
            }
        });
    }
}

// 현재는 2인 매치 전용 Request
// UidA -> slot 0, UidB -> slot 1 로 사용
public record IssueReq(string MatchId, string RoomId, string UidA, string UidB);

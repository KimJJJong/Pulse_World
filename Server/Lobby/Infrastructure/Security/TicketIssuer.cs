using Lobby.Domain.Auth.Interface;

namespace Lobby.Infrastructure.Security;

public sealed class TicketIssuer : ITicketIssuer
{
    private readonly IJwtService _jwt;
    public TicketIssuer(IJwtService jwt) => _jwt = jwt;

    public IReadOnlyList<(string uid, int slot, GameStartTicket ticket)> IssueStartTickets(
        string matchId, string roomId,
        IReadOnlyList<(string uid, int slot)> players,
        string gsHost, int gsPort,
        int tickRate, TimeSpan ttl,
        int proto)
    {
        if (players is null || players.Count == 0)
            throw new ArgumentException("players must not be empty", nameof(players));

        // 한 번 배열로 고정해서 opponents 계산에 사용
        var arr = players.ToArray();
        var result = new List<(string uid, int slot, GameStartTicket ticket)>(arr.Length);

        for (int i = 0; i < arr.Length; i++)
        {
            var p = arr[i];

            // 나를 제외한 나머지 uid = opponents
            var opponentUids = arr
                .Where((_, idx) => idx != i)
                .Select(x => x.uid)
                .ToArray();

            var payload = BasePayload(
                matchId: matchId,
                roomId: roomId,
                uid: p.uid,
                slot: p.slot,
                opponentUids: opponentUids,
                gsHost: gsHost,
                gsPort: gsPort,
                tickRate: tickRate,
                proto: proto
            );

            var (token, jti, nonce) = _jwt.IssueTicket(payload, ttl);
            result.Add((p.uid, p.slot, new GameStartTicket(token, jti, nonce)));
        }

        return result;
    }

    /// <summary>
    /// JWT 클레임 기본값 빌더 (slot + opponents 배열 기반)
    /// </summary>
    private static Dictionary<string, object> BasePayload(
        string matchId,
        string roomId,
        string uid,
        int slot,
        string[] opponentUids,
        string gsHost,
        int gsPort,
        int tickRate,
        int proto
    )
    {
        return new Dictionary<string, object>
        {
            ["matchId"] = matchId,
            ["roomId"] = roomId,
            ["uid"] = uid,
            ["slot"] = slot,                      
            ["opponentUids"] = opponentUids,      // opponentUid 배열
            ["gsHost"] = gsHost,
            ["gsPort"] = gsPort,
            ["tickRate"] = tickRate,
            ["startAtTick"] = 0,                  // 필요 시 나중에 채워 넣기
            ["protoVer"] = proto
        };
    }



}

public readonly record struct GameStartTicket(string token, string jti, string nonce);

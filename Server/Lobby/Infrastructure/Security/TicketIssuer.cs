using Lobby.Domain.Auth.Interface;

namespace Lobby.Infrastructure.Security;

public sealed class TicketIssuer : ITicketIssuer
{
    private readonly IJwtService _jwt;
    public TicketIssuer(IJwtService jwt) => _jwt = jwt;

    public (GameStartTicket toA, GameStartTicket toB) IssueStartTickets(
        string matchId, string roomId,
        (string uid, string side) a,
        (string uid, string side) b,
        string gsHost, int gsPort,
        int tickRate, TimeSpan ttl,
        int proto)
    {
        Dictionary<string, object> Base(string uid, string side, string opponentUid) => new()
        {
            ["matchId"]=matchId, ["roomId"]=roomId,
            ["uid"]=uid, ["side"]=side, 
            ["opponentUid"]=opponentUid,
            ["gsHost"]=gsHost, ["gsPort"]=gsPort, 
            ["tickRate"]=tickRate,  ["startAtTick"]=0,
            ["protoVer"] = proto
        };
        var (tokA, jtiA, nonceA) = _jwt.IssueTicket(Base(a.uid, a.side, b.uid), ttl);
        var (tokB, jtiB, nonceB) = _jwt.IssueTicket(Base(b.uid, b.side, a.uid), ttl);

        return (new GameStartTicket(tokA, jtiA, nonceA), new GameStartTicket(tokB, jtiB, nonceB));
    }

    // 하위호환(필요?시 유지)
 /*   public string IssueGameTicket(string roomId, IEnumerable<string> userIds, TimeSpan ttl)
        => throw new NotSupportedException("Use IssueStartTickets for per-player tokens.");*/
}

public readonly record struct GameStartTicket(string token, string jti, string nonce);

using Microsoft.Extensions.Options;

public interface ITownEndpointResolver
{
    (string serverId, string host, int port) Resolve(string? preferredServerId);
}

public sealed class TownEndpointResolver : ITownEndpointResolver
{
    private readonly TownRoutingOptions _opt;

    public TownEndpointResolver(IOptions<TownRoutingOptions> opt)
        => _opt = opt.Value;

    public (string serverId, string host, int port) Resolve(string? preferredServerId)
    {
        if (_opt.Servers.Count == 0)
            throw new InvalidOperationException("TownRoutingOptions.Servers is empty.");

        if (!string.IsNullOrWhiteSpace(preferredServerId))
        {
            var found = _opt.Servers.Find(s => s.ServerId == preferredServerId);
            if (found != null)
                return (found.ServerId, found.Host, found.Port);
        }

        // 최소 구현: 첫 번째 서버
        // 운영: 여기서 로드/헬스/지역/가중치/라운드로빈 등을 적용
        var pick = _opt.Servers[0];
        return (pick.ServerId, pick.Host, pick.Port);
    }
}
public sealed record PostTownTicketRequest(
    int? TtlSeconds = null,
    string? PreferredServerId = null
);

public sealed record PostTownTicketResponse(
    string TicketId,
    long ExpireAtMs,
    string TownHost,
    int TownPort,
    string ServerId,
    string Key // town은 ""일 가능성이 큼
);

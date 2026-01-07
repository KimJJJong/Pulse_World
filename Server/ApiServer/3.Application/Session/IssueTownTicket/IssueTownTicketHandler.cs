using ApiServer.Application.Ports;
using ApiServer.Application.Ports.Models;
using ApiServer.Infrastructure.Options;
using ApiServer.Shared.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiServer.Application.Session.IssueTownTicket;

public sealed class IssueTownTicketHandler
{
    private readonly IControlPlanePort _cp;
    private readonly ITimeProvider _time;
    private readonly TownEndpointOptions _town;

    public IssueTownTicketHandler(
        IControlPlanePort cp,
        ITimeProvider time,
        IOptions<TownEndpointOptions> town)
    {
        _cp = cp;
        _time = time;
        _town = town.Value;
    }

    public async Task<IssueTownTicketResult> HandleAsync(IssueTownTicketCommand cmd, CancellationToken ct)
    {
        var nowMs = _time.UtcNow.ToUnixTimeMilliseconds();

        //  Town endpoint는 ApiServer가 config로 소유(현재 proto 기준 정석)
        var endpoint = new Ports.Models.Endpoint(_town.Host, _town.Port);
        Console.WriteLine(endpoint);
        // Town ticket 발급
        var (tid, expAt, _, _) = await _cp.IssueTicketAsync(
            uid: cmd.Uid,
            target: "TOWN",
            key: "",
            preferredServerId: "",
            ttlSeconds: 30,
            ct: ct);

        return new IssueTownTicketResult(tid, expAt, endpoint);
    }
}

using ApiServer.Application.Ports;
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
        //var endpoint = new Ports.Models.Endpoint(_town.Host, _town.Port);
        // Town ticket 발급
        Console.WriteLine("[IN] HandleAsync");

        try
        {
            var (tid, expAt, _, _, endPoint) = await _cp.IssueTicketAsync(
                uid: cmd.Uid, 
                target: "TOWN",
                key: "",
                preferredServerId: "ts1",   //TODO : loadBalance할라면 Allocate로 수정
                ttlSeconds: 30,
                ct: ct);

            Console.WriteLine($"[AFTER] IssueTicketAsync ok endpoint={endPoint}");
            return new IssueTownTicketResult(tid, expAt, endPoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EX] {ex.GetType().FullName}: {ex.Message}");
            Console.WriteLine(ex.ToString());
            throw;
        }
    }
}

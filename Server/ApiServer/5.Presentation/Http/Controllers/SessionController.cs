using ApiServer.Application.Session.IssueGameTicket;
using ApiServer.Application.Session.IssueTownTicket;
using ApiServer.Presentation.Http.Contracts;
using ApiServer.Presentation.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers;

[ApiController]
[Route("session")]
public sealed class SessionController : ControllerBase
{
    [HttpPost("ticket/town")]
    public async Task<ActionResult<SessionDtos.IssueTownTicketResponse>> IssueTownTicket(
        [FromServices] IssueTownTicketHandler handler,
        [FromBody] SessionDtos.IssueTownTicketRequest req,
        CancellationToken ct)
    {
        Console.WriteLine("[IN] /session/ticket/town");
        var uid = HttpContext.RequireUid();

        var result = await handler.HandleAsync(
            new IssueTownTicketCommand(uid, req.PreferredRegion),
            ct);

        Console.WriteLine($"endPoint :{result.Endpoint.Host} : {result.Endpoint.Port}");
        return Ok(new SessionDtos.IssueTownTicketResponse(
            TicketId: result.TicketId,
            ExpireAtMs: result.ExpireAtMs,
            Endpoint: new SessionDtos.EndpointDto(result.Endpoint.Host, result.Endpoint.Port)
        ));
    }

    [HttpPost("ticket/game")]
    public async Task<ActionResult<SessionDtos.IssueGameTicketResponse>> IssueGameTicket(
        [FromServices] IssueGameTicketHandler handler,
        [FromBody] SessionDtos.IssueGameTicketRequest req,
        CancellationToken ct)
    {
        var uid = HttpContext.RequireUid();
        //Console.WriteLine($" roomID : {req.RoomId} || Map :{req.Map} || Player : {req.MaxPlayers} || Uid : {uid}");

        var result = await handler.HandleAsync(
            new IssueGameTicketCommand(uid, req.RoomId, req.Map, req.MaxPlayers, req.PreferredRegion),
            ct);

        return Ok(new SessionDtos.IssueGameTicketResponse(
            TransitionId: result.TransitionId,
            TicketId: result.TicketId,
            ExpireAtMs: result.ExpireAtMs,
            ServerId: result.ServerId,
            Endpoint: new SessionDtos.EndpointDto(result.Endpoint.Host, result.Endpoint.Port),
            Key: result.Key
        ));
    }
}

using ApiServer.Application.Session.IssueGameTicket;
using ApiServer.Application.Session.IssueTownTicket;
using ApiServer.Domain.Town;
using ApiServer.Presentation.Http;
using ApiServer.Presentation.Http.Contracts;
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
        var uid = HttpContext.RequireUid();

        var result = await handler.HandleAsync(
            new IssueTownTicketCommand(
                uid,
                req.PreferredRegion,
                req.TownRoomId,
                req.MapId,
                req.MaxPlayers,
                req.SteamId64,
                req.ClientVersion),
            ct);

        return Ok(new SessionDtos.IssueTownTicketResponse(
            TicketId: result.TicketId,
            ExpireAtMs: result.ExpireAtMs,
            Endpoint: new SessionDtos.EndpointDto(result.Endpoint.Host, result.Endpoint.Port),
            Key: result.Key,
            TownRoomId: result.TownRoomId,
            MapId: result.MapId,
            MaxPlayers: result.MaxPlayers,
            MatchManifest: ToMatchManifestDto(result.MatchManifest)
        ));
    }

    [HttpPost("ticket/game")]
    public async Task<ActionResult<SessionDtos.IssueGameTicketResponse>> IssueGameTicket(
        [FromServices] IssueGameTicketHandler handler,
        [FromBody] SessionDtos.IssueGameTicketRequest req,
        CancellationToken ct)
    {
        var uid = HttpContext.RequireUid();

        var result = await handler.HandleAsync(
            new IssueGameTicketCommand(uid, req.RoomId, req.Map, req.MaxPlayers, req.PreferredRegion, req.UseP2PRelay),
            ct);

        return Ok(new SessionDtos.IssueGameTicketResponse(
            TransitionId: result.TransitionId,
            TicketId: result.TicketId,
            ExpireAtMs: result.ExpireAtMs,
            ServerId: result.ServerId,
            Endpoint: new SessionDtos.EndpointDto(result.Endpoint.Host, result.Endpoint.Port),
            Key: result.Key,
            MapId: result.MapId,
            MaxPlayers: result.MaxPlayers
        ));
    }

    private static SessionDtos.MatchManifestDto? ToMatchManifestDto(TownMatchManifest? manifest)
    {
        if (manifest == null)
            return null;

        return new SessionDtos.MatchManifestDto(
            MatchId: manifest.MatchId,
            RoomId: manifest.RoomId,
            NetworkMode: manifest.NetworkMode,
            ProtocolVersion: manifest.ProtocolVersion,
            MapId: manifest.MapId,
            StageSeed: manifest.StageSeed,
            SongStartDelayMs: manifest.SongStartDelayMs,
            HostUid: manifest.HostUid,
            HostSteamId64: manifest.HostSteamId64,
            HostEpoch: manifest.HostEpoch,
            PreferredHostRttMs: manifest.PreferredHostRttMs,
            HostSelectionMode: manifest.HostSelectionMode,
            HostSelectionMetricVersion: manifest.HostSelectionMetricVersion,
            HostSelectionEpoch: manifest.HostSelectionEpoch,
            HostSelectionScore: manifest.HostSelectionScore,
            HostSelectionUpdatedAtMs: manifest.HostSelectionUpdatedAtMs,
            HostCandidateOrder: manifest.HostCandidateOrder,
            CreatedAtMs: manifest.CreatedAtMs,
            Participants: manifest.Participants.Select(x => new SessionDtos.MatchParticipantDto(
                Uid: x.Uid,
                DisplayName: x.DisplayName,
                SteamId64: x.SteamId64,
                ActorId: x.ActorId,
                LoadoutHash: x.LoadoutHash)).ToList());
    }
}

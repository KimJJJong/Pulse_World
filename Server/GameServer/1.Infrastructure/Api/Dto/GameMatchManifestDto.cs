using System.Collections.Generic;

namespace GameServer.Infrastructure.Api.Dto;

public sealed class GameMatchManifestResponse
{
    public string MatchId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public string NetworkMode { get; set; } = "";
    public string ProtocolVersion { get; set; } = "";
    public string MapId { get; set; } = "";
    public int StageSeed { get; set; }
    public int SongStartDelayMs { get; set; }
    public string HostUid { get; set; } = "";
    public string HostSteamId64 { get; set; } = "";
    public int HostEpoch { get; set; }
    public int PreferredHostRttMs { get; set; } = -1;
    public long CreatedAtMs { get; set; }
    public List<GameMatchParticipantResponse> Participants { get; set; } = new();
}

public sealed class GameMatchParticipantResponse
{
    public string Uid { get; set; } = "";
    public string SteamId64 { get; set; } = "";
    public int ActorId { get; set; }
    public string LoadoutHash { get; set; } = "";
}

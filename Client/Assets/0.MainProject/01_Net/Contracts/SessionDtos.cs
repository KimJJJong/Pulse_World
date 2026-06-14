using Newtonsoft.Json;
using System;
using System.Collections.Generic;

public static class SessionDtos
{
    [Serializable]
    public sealed class EndpointDto
    {
        [JsonProperty("host")] public string Host;
        [JsonProperty("port")] public int Port;
    }

    [Serializable]
    public sealed class MatchParticipantDto
    {
        [JsonProperty("uid")] public string Uid;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("steamId64")] public string SteamId64;
        [JsonProperty("actorId")] public int ActorId;
        [JsonProperty("loadoutHash")] public string LoadoutHash;
    }

    [Serializable]
    public sealed class MatchManifestDto
    {
        [JsonProperty("matchId")] public string MatchId;
        [JsonProperty("roomId")] public string RoomId;
        [JsonProperty("networkMode")] public string NetworkMode;
        [JsonProperty("protocolVersion")] public string ProtocolVersion;
        [JsonProperty("mapId")] public string MapId;
        [JsonProperty("stageSeed")] public int StageSeed;
        [JsonProperty("songStartDelayMs")] public int SongStartDelayMs;
        [JsonProperty("hostUid")] public string HostUid;
        [JsonProperty("hostSteamId64")] public string HostSteamId64;
        [JsonProperty("hostEpoch")] public int HostEpoch;
        [JsonProperty("preferredHostRttMs")] public int PreferredHostRttMs;
        [JsonProperty("hostSelectionMode")] public string HostSelectionMode;
        [JsonProperty("hostSelectionMetricVersion")] public string HostSelectionMetricVersion;
        [JsonProperty("hostSelectionEpoch")] public int HostSelectionEpoch;
        [JsonProperty("hostSelectionScore")] public float HostSelectionScore;
        [JsonProperty("hostSelectionUpdatedAtMs")] public long HostSelectionUpdatedAtMs;
        [JsonProperty("hostCandidateOrder")] public List<string> HostCandidateOrder;
        [JsonProperty("createdAtMs")] public long CreatedAtMs;
        [JsonProperty("participants")] public List<MatchParticipantDto> Participants;
    }

    public sealed class IssueTownTicketRequest
    {
        [JsonProperty("preferredRegion")] public string PreferredRegion;
        [JsonProperty("townRoomId")] public string TownRoomId;
        [JsonProperty("mapId")] public string MapId;
        [JsonProperty("maxPlayers")] public int MaxPlayers;
        [JsonProperty("steamId64")] public string SteamId64;
        [JsonProperty("clientVersion")] public string ClientVersion;

        public IssueTownTicketRequest(
            string preferredRegion,
            string townRoomId = "",
            string mapId = "",
            int maxPlayers = 16,
            string steamId64 = "",
            string clientVersion = "")
        {
            PreferredRegion = preferredRegion;
            TownRoomId = townRoomId;
            MapId = mapId;
            MaxPlayers = maxPlayers;
            SteamId64 = steamId64;
            ClientVersion = clientVersion;
        }
    }

    public sealed class IssueTownTicketResponse
    {
        [JsonProperty("ticketId")] public string TicketId;
        [JsonProperty("expireAtMs")] public long ExpireAtMs;
        [JsonProperty("endpoint")] public EndpointDto Endpoint;
        [JsonProperty("key")] public string Key;
        [JsonProperty("townRoomId")] public string TownRoomId;
        [JsonProperty("mapId")] public string MapId;
        [JsonProperty("maxPlayers")] public int MaxPlayers;
        [JsonProperty("matchManifest")] public MatchManifestDto MatchManifest;
    }

    public sealed class IssueGameTicketRequest
    {
        [JsonProperty("roomId")] public string RoomId;
        [JsonProperty("map")] public string Map;
        [JsonProperty("maxPlayers")] public int MaxPlayers;
        [JsonProperty("preferredRegion")] public string PreferredRegion;
        [JsonProperty("useP2PRelay")] public bool UseP2PRelay;

        public IssueGameTicketRequest(string roomId, string map, int maxPlayers, string preferredRegion, bool useP2PRelay = true)
        {
            RoomId = roomId;
            Map = map;
            MaxPlayers = maxPlayers;
            PreferredRegion = preferredRegion;
            UseP2PRelay = useP2PRelay;
        }
    }

    public sealed class IssueGameTicketResponse
    {
        [JsonProperty("transitionId")] public string TransitionId;
        [JsonProperty("ticketId")] public string TicketId;
        [JsonProperty("expireAtMs")] public long ExpireAtMs;
        [JsonProperty("serverId")] public string ServerId;
        [JsonProperty("endpoint")] public EndpointDto Endpoint;
        [JsonProperty("key")] public string Key;
        [JsonProperty("mapId")] public string MapId;
        [JsonProperty("maxPlayers")] public int MaxPlayers;
        [JsonProperty("matchManifest")] public MatchManifestDto MatchManifest;
    }
}

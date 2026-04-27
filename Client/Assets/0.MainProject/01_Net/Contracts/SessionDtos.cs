using Newtonsoft.Json;

public static class SessionDtos
{
    public sealed class EndpointDto
    {
        [JsonProperty("host")] public string Host;
        [JsonProperty("port")] public int Port;
    }

    public sealed class IssueTownTicketRequest
    {
        [JsonProperty("preferredRegion")] public string PreferredRegion;
        public IssueTownTicketRequest(string preferredRegion) => PreferredRegion = preferredRegion;
    }

    public sealed class IssueTownTicketResponse
    {
        [JsonProperty("ticketId")] public string TicketId;
        [JsonProperty("expireAtMs")] public long ExpireAtMs;
        [JsonProperty("endpoint")] public EndpointDto Endpoint;
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
    }
}

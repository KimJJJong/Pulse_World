using System;
using System.Collections.Generic;

    // Base for type-checking
    [Serializable]
    public class BasePacket
    {
        public string type;
    }

    // --- Requests ---
    [Serializable]
    public class ReadyRequest
    {
        public string type = "Ready";
        public bool value;
    }

    [Serializable]
    public class StartRequest
    {
        public string type = "Start";
    }

    [Serializable]
    public class HostProbePingRequest
    {
        public string type = "HostProbePing";
        public string nonce;
    }

    [Serializable]
    public class HostProbeReportRequest
    {
        public string type = "HostProbeReport";
        public int rttMs;
        public long reportedAtMs;
    }

    [Serializable]
    public class HostSelectionReportRequest
    {
        public string type = "HostSelectionReport";
        public string steamId64;
        public bool steamEnabled;
        public bool steamInitialized;
        public bool steamLobbyJoined;
        public bool steamReady;
        public int currentServerRttMs = -1;
        public float currentServerLossPct;
        public int currentServerJitterMs = -1;
        public float avgFrameMs = -1f;
        public float p95FrameMs = -1f;
        public int sendQueueDepth;
        public long reportedAtMs;
    }

    [Serializable]
    public class BindSteamLobbyRequest
    {
        public string type = "BindSteamLobby";
        public string steamLobbyId;
    }

    // --- Responses / Events ---
    
    [Serializable]
    public class MemberReadyState
    {
        public string uid;
        public bool ready;
    }

    [Serializable]
    public class WaitingRoomDto
    {
        public string roomId;
        public string title;
        public string mapId;
        public int maxPlayers;
        public string ownerUid;
        public string status;
        public bool useP2PRelay;
        public string steamLobbyId;
        public string preferredHostUid;
        public int hostEpoch;
        public int hostSelectionEpoch;
        public string hostSelectionMode;
        public string hostSelectionMetricVersion;
        public float hostSelectionScore;
        public long hostSelectionUpdatedAtMs;
        public List<string> hostCandidateOrder;
        public List<HostSelectionCandidateState> hostSelectionCandidates;
        public List<string> memberUids; // JsonUtility supports List<string>
        public List<MemberReadyState> memberReady; // Changed from Dictionary for JsonUtility compatibility
        public List<MemberTransportState> memberTransport;
    }

    [Serializable]
    public class MemberTransportState
    {
        public string uid;
        public string name;
        public string steamId64;
        public string clientVersion;
        public int hostProbeRttMs;
        public long hostProbeReportedAtMs;
        public bool steamEnabled;
        public bool steamInitialized;
        public bool steamLobbyJoined;
        public bool steamReady;
        public int currentServerRttMs;
        public float currentServerLossPct;
        public int currentServerJitterMs;
        public float avgFrameMs;
        public float p95FrameMs;
        public int sendQueueDepth;
        public long hostSelectionReportedAtMs;
    }

    [Serializable]
    public class HostSelectionCandidateState
    {
        public string uid;
        public bool isEligible;
        public float candidateCost;
        public float averagePairCost;
        public float worstPairCost;
        public int averagePairRttMs;
        public int worstPairRttMs;
        public int steamPairCount;
        public int serverRelayPairCount;
        public int unavailablePairCount;
        public float hostCapacityPenalty;
        public bool steamReady;
        public int currentServerRttMs;
        public float currentServerLossPct;
        public int currentServerJitterMs;
        public float avgFrameMs;
        public float p95FrameMs;
        public List<string> disqualifiedReasons;
    }

    // "Init"
    [Serializable]
    public class InitMsg
    {
        public string type;
        public WaitingRoomDto room;
    }

    // "MemberJoin"
    [Serializable]
    public class MemberJoinMsg
    {
        public string type;
        public string uid;
        public string name;
    }

    // "MemberLeave"
    [Serializable]
    public class MemberLeaveMsg
    {
        public string type;
        public string uid;
    }

    // "MemberUpdate"
    [Serializable]
    public class MemberUpdateMsg
    {
        public string type;
        public string uid;
        public bool ready;
    }

    [Serializable]
    public class HostProbePongMsg
    {
        public string type;
        public string nonce;
        public long serverTimeMs;
    }

    [Serializable]
    public class HostCandidateUpdateMsg
    {
        public string type;
        public string preferredHostUid;
        public int hostEpoch;
        public int hostSelectionEpoch;
        public string hostSelectionMode;
        public string hostSelectionMetricVersion;
        public float hostSelectionScore;
        public long hostSelectionUpdatedAtMs;
        public List<string> hostCandidateOrder;
        public List<HostSelectionCandidateState> hostSelectionCandidates;
        public string uid;
        public int hostProbeRttMs;
    }

    [Serializable]
    public class SteamLobbyBoundMsg
    {
        public string type;
        public string steamLobbyId;
    }

    [Serializable]
    public class WsMatchParticipantDto
    {
        public string uid;
        public string steamId64;
        public int actorId;
        public string loadoutHash;
    }

    [Serializable]
    public class WsMatchManifestDto
    {
        public string matchId;
        public string roomId;
        public string networkMode;
        public string protocolVersion;
        public string mapId;
        public int stageSeed;
        public int songStartDelayMs;
        public string hostUid;
        public string hostSteamId64;
        public int hostEpoch;
        public int preferredHostRttMs;
        public string hostSelectionMode;
        public string hostSelectionMetricVersion;
        public int hostSelectionEpoch;
        public float hostSelectionScore;
        public long hostSelectionUpdatedAtMs;
        public List<string> hostCandidateOrder;
        public long createdAtMs;
        public List<WsMatchParticipantDto> participants;
    }

    // "GameStart"
    [Serializable]
    public class GameStartMsg
    {
        public string type;
        public EndpointDto endpoint;
        public string ticket;
        public string mapId;
        public int maxPlayers;
        public bool useP2PRelay;
        public WsMatchManifestDto matchManifest;
    }

    [Serializable]
    public class EndpointDto
    {
        public string host;
        public int port;
    }

    // "Error"
    [Serializable]
    public class ErrorMsg
    {
        public string type;
        public string message;
        public string code;
    }

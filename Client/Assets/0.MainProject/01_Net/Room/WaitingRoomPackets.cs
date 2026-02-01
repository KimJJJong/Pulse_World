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
        public List<string> memberUids; // JsonUtility supports List<string>
        public List<MemberReadyState> memberReady; // Changed from Dictionary for JsonUtility compatibility
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

    // "GameStart"
    [Serializable]
    public class GameStartMsg
    {
        public string type;
        public EndpointDto endpoint;
        public string ticket;
        public string mapId;
        public int maxPlayers;
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

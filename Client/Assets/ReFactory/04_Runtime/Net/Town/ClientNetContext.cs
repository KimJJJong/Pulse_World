//using System;

//public sealed class ClientNetContext
//{
//    public static ClientNetContext Instance { get; } = new ClientNetContext();
//    ClientNetContext() { }

//    // ---- handshake ----
//    public string Uid { get; private set; } = "";
//    public long ServerTimeMsAtHandshake { get; private set; }
//    public long SessionEpoch { get; private set; }
//    public string Role { get; private set; } = "Unknown";

//    // ---- map ----
//    public bool TownSceneLoaded { get; private set; }
//    public bool HandshakeOk { get; private set; }
//    public bool InitMapReceived { get; private set; }

//    public int Rev { get; private set; }
//    public int TickRate { get; private set; }
//    public string MapId { get; private set; } = "";
//    public string MapVersion { get; private set; } = "";
//    public int MyActorId { get; private set; }

//    public void ApplyHandshakeOk(string uid, long serverTimeMs, long sessionEpoch, string role)
//    {
//        Uid = uid;
//        ServerTimeMsAtHandshake = serverTimeMs;
//        SessionEpoch = sessionEpoch;
//        Role = role;
//        HandshakeOk = true;
//    }

//    public void MarkTownSceneLoaded() => TownSceneLoaded = true;

//    public void ApplyInitMap(int rev, int tickRate, string mapId, string mapVersion, int myActorId)
//    {
//        Rev = rev;
//        TickRate = tickRate;
//        MapId = mapId;
//        MapVersion = mapVersion;
//        MyActorId = myActorId;
//        InitMapReceived = true;
//    }

//    public void ResetForReconnect()
//    {
//        TownSceneLoaded = false;
//        HandshakeOk = false;
//        InitMapReceived = false;
//        Rev = 0;
//        TickRate = 0;
//        MapId = "";
//        MapVersion = "";
//        MyActorId = 0;
//    }
//}

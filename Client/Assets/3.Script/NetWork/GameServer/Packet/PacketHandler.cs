using Client;
using Newtonsoft.Json.Bson;
using ServerCore;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem.HID;

class PacketHandler
{
    public static void SC_ErrorHandler(PacketSession session, IPacket packet)
    {
        SC_Error e = (SC_Error)packet;
        UnityEngine.Debug.LogError($"[Client] Error {e.code} {e.message}");
    }
    public static void SC_WelcomeHandler(PacketSession session, IPacket packet)
    {
        SC_Welcome w = (SC_Welcome)packet;
        ServerSession server = (ServerSession)session;
        TimeSync.SetOffsetFromServerNow(w.serverTimeMs);

        UnityEngine.Debug.Log($"In WelconHandle : [{w.matchId}] || [{w.slot}]");

        // 씬 로드 후 CS_Loaded 보고
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("RhythmTest")
            .completed += _ =>
            {
                CS_Loaded loaded = new CS_Loaded { matchId = w.matchId, uid = NetWorkManager.Instance.Uid };
                NetWorkManager.Instance.Send(loaded.Write());
            };
        UnityEngine.Debug.Log($"MyActorId : {w.slot}");
        ClientHandlers.Instance.GS.SetMyActorId( w.slot);

    }
    public static void SC_AllPlayersLoadedHandler(PacketSession session, IPacket packet)
    {
        // WelcomeHandler에서 load한 Scene 대기 후 해당 AllPlayerLoader 받으면 바로 시작 하도록
    }
    public static void SC_GameBeginHandler(PacketSession session, IPacket packet)
    {
        SC_GameBegin g = (SC_GameBegin)packet;
        var waitMs = TimeSync.MillisUntil(g.startAtMs);
        UnityEngine.Debug.Log($"[GameStart] MatchId :{g.matchId}  WatiMs : {waitMs}  startTick : {g.startTick}");
        //UnityEngine.CoroutineRunner.Run(BeginAfter(waitMs, g.startTick)); // 임의 유틸, 본인 코루틴으로 대체
        
        // 이거 나중에 GameStart Manager생기면 위임
        if (PingManager.Instance is null)
        {
            UnityEngine.Debug.Log("PingManager Summon");
            new GameObject("PingManager").AddComponent<PingManager>();
        }
        PingManager.Instance.Configure(interval: 2000, timeout: 6000, maxMiss: 3);
        PingManager.Instance.StartLoop();
        // 끝날땐 PingManager.Instance?.StopLoop(); Destroy(PingManager.Instance.gameObject);
    }

    public static void SC_PongHandler(PacketSession session, IPacket packet)
    {
        SC_Pong pongPacket = (SC_Pong)packet;
        PingManager.Instance?.OnPong(pongPacket);
    }

    public static void SC_WarnHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_Warn((SC_Warn)packet);





        /// <summary>
        /// rhytm
        /// </summary>
        /// <param name="session"></param>
        /// <param name="packet"></param>
    public static void SC_InitGameHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_InitGame((SC_InitGame)packet);
    public static void SC_BeatActionsHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_BeatActions((SC_BeatActions)packet);
    public static void SC_BeatSyncHandler(PacketSession session, IPacket packet)
         => ClientHandlers.Instance.Handle_SC_BeatSync((SC_BeatSync)packet);

    public static void SC_BeatTelegraphsHandler(PacketSession session, IPacket packet)
    {

    }
}
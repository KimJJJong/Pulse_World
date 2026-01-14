using ServerCore;
using UnityEngine;

class PacketHandler
{
    public static void SC_ErrorHandler(PacketSession session, IPacket packet)
    {
        SC_Error e = (SC_Error)packet;
        UnityEngine.Debug.LogError($"[Client] Error {e.code} {e.message}");
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



    public static void SC_HandshakeOkHandler(PacketSession session, IPacket packet)
    {
        var p = (SC_HandshakeOk)packet;

        ClientNetContext.Instance.ApplyHandshakeOk(
            uid: p.Uid,
            serverTimeMs: p.ServerTimeMs,
            sessionEpoch: p.SessionEpoch,
            role: "Town"
            );

        // 1) 네트워크 상태 확정
        NetworkManager.Instance.OnHandshakeSucceeded();

        TownFlow.OnHandshakeOk();


        //UnityEngine.Debug.Log($"[Network] UID :{p.Uid} || STATE : {p.ServerRole} || Epoch : {p.SessionEpoch} ||");
    }

    public static void SC_HandshakeFailHandler(PacketSession session, IPacket packet)
    {
        var p = (SC_HandshakeFail)packet;

        // 1) 실패 사유 로깅
        // 예: p.reason, p.code 등이 있다면 사용
        UnityEngine.Debug.LogError("[Network] Handshake FAIL");

        // 2) 연결 종료 및 상태 정리
        NetworkManager.Instance.OnHandshakeFailed("HandshakeFail");
        ClientNetContext.Instance.ResetForReconnect();

        // 3) 필요하면 UI 알림/재시도 트리거
        // UIManager.Instance.ShowError("서버 인증 실패");
    }

    public static void SC_ForcedDisconnectHandler(PacketSession session, IPacket packet)
    {
        var p = (SC_ForcedDisconnect)packet;

        // 1) 서버 강제 종료 사유 로깅
        UnityEngine.Debug.LogWarning("[Network] Forced disconnect by server");

        // 2) 즉시 연결 정리
        NetworkManager.Instance.OnForcedDisconnect("ForcedDisconnect");
        ClientNetContext.Instance.ResetForReconnect();

        // 3) UI 알림/로비 이동 등
        // SceneRouter.Load(SceneNames.Town);
    }

    ///TOWN
    public static void SC_InitMapHandler(PacketSession session, IPacket packet)
    {
        var p = (SC_InitMap)packet;
        Debug.Log("[IN]SC_InitMapHandler");
        // 1) 컨텍스트 저장
        ClientNetContext.Instance.ApplyInitMap(
            rev: p.Revision,
            tickRate: p.TickRate,
            mapId: p.MapId,
            mapVersion: "Test",//p.MapVersion,
            myActorId: p.MyActorId
        );
        ClientHandlers.Instance.HandleSC_InitMap((SC_InitMap)packet);

  
        // 이거 나중에 GameStart Manager생기면 위임
        if (PingManager.Instance is null)
        {
            UnityEngine.Debug.Log("PingManager Summon");
            new GameObject("PingManager").AddComponent<PingManager>();
        }
        PingManager.Instance.Configure(interval: 2000, timeout: 6000, maxMiss: 3);
        PingManager.Instance.StartLoop();
    }

    public static void SC_ReadyAckHandler(PacketSession session, IPacket packet)
    {

    }


    public static void SC_TownBeatActionsHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.HandleSC_TownBeatActions((SC_TownBeatActions)packet);


    /// <summary>
    /// rhytm
    /// </summary>
    /// <param name="session"></param>
    /// <param name="packet"></param>
    public static void SC_InitGameHandler(PacketSession session, IPacket packet) { }
        //=> ClientHandlers.Instance.Handle_SC_InitGame((SC_InitGame)packet);
    public static void SC_CalibResultHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_CalibResult((SC_CalibResult)packet);

    public static void SC_BeatActionsHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_BeatActions((SC_BeatActions)packet);
    public static void SC_BeatSyncHandler(PacketSession session, IPacket packet)
         => ClientHandlers.Instance.Handle_SC_BeatSync((SC_BeatSync)packet);

    public static void SC_BeatTelegraphsHandler(PacketSession session, IPacket packet)
         => ClientHandlers.Instance.Handle_SC_BeatTelegraphs((SC_BeatTelegraphs)packet);

    public static void SC_EntityDespawnHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_EntityDespawn((SC_EntityDespawn)packet);

    public static void SC_EntitySpawnHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_EntitySpawnHandler((SC_EntitySpawn)packet);
}
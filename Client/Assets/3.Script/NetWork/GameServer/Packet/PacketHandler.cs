using ServerCore;
using UnityEngine;

class PacketHandler
{
    public static void SC_ErrorHandler(PacketSession session, IPacket packet)
    {
        SC_Error e = (SC_Error)packet;
        UnityEngine.Debug.LogError($"[Client] Error {e.code} {e.message}");
    }
    //public static void SC_WelcomeHandler(PacketSession session, IPacket packet)
    //{
    //    SC_Welcome w = (SC_Welcome)packet;
    //    ServerSession server = (ServerSession)session;
    //    //TimeSync.SetOffsetFromServerNow(w.serverTimeMs);

    //    UnityEngine.Debug.Log($"In WelconHandle : [{w.matchId}] || [{w.slot}]");

    //    // 씬 로드 후 CS_Loaded 보고
    //    UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("RhythmTest")
    //        .completed += _ =>
    //        {
    //            CS_Loaded loaded = new CS_Loaded { matchId = w.matchId, uid = NetWorkManager.Instance.Uid };
    //            NetWorkManager.Instance.Send(loaded.Write());
    //        };
    //    UnityEngine.Debug.Log($"MyActorId : {w.slot}");
    //    ClientHandlers.Instance.GS.SetMyActorId( w.slot);

    //}
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

        // 1) 네트워크 상태 확정
        NetworkManager.Instance.OnHandshakeSucceeded();

        // 2) 서버에서 내려준 세션/매치 정보가 있다면 반영
        // 예시 (필드가 있을 경우만)
        // ClientGameState.Instance.MatchId = p.matchId;
        // ClientGameState.Instance.ServerTimeMs = p.serverTimeMs;

        UnityEngine.Debug.Log($"[Network] UID :{p.Uid} || STATE : {p.State} || Epoch : {p.Epoch} || RoomID : {p.RoomId}");
    }

    public static void SC_HandshakeFailHandler(PacketSession session, IPacket packet)
    {
        var p = (SC_HandshakeFail)packet;

        // 1) 실패 사유 로깅
        // 예: p.reason, p.code 등이 있다면 사용
        UnityEngine.Debug.LogError("[Network] Handshake FAIL");

        // 2) 연결 종료 및 상태 정리
        NetworkManager.Instance.OnHandshakeFailed("HandshakeFail");

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

        // 3) UI 알림/로비 이동 등
        // SceneRouter.Load(SceneNames.Town);
    }
    /// <summary>
    /// rhytm
    /// </summary>
    /// <param name="session"></param>
    /// <param name="packet"></param>
    public static void SC_InitGameHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_InitGame((SC_InitGame)packet);
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
}
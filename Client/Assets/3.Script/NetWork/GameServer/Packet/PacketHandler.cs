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

        Debug.Log("SC_HandshakeOkHandler");

        // 이친구도 조금더 엘래강스하게 뺄 수 없을까?
        string serverRole = "Unknown";
        switch (p.ServerRole)
        {
            case 1:
                serverRole = "Town";
                break;
            case 2:
                serverRole = "Game";
                break;
            default:
                serverRole = "Undefine";
                break;
        }

        SessionContext.Instance.ApplyHandshakeOk(
            uid: p.Uid,
            serverTimeMs: p.ServerTimeMs,
            sessionEpoch: p.SessionEpoch,
            role: serverRole

            );

        // 2) 네트워크 상태 확정 (Ready 이벤트 발행 -> ClientFlow가 씬 전환)
        NetworkManager.Instance.OnHandshakeSucceeded();

        //TownFlow.OnHandshakeOk();


        //UnityEngine.Debug.Log($"[Network] UID :{p.Uid} || STATE : {p.ServerRole} || Epoch : {p.SessionEpoch} ||");
    }

    public static void SC_HandshakeFailHandler(PacketSession session, IPacket packet)
    {
        var p = (SC_HandshakeFail)packet;

        Debug.LogError("[Network] Handshake FAIL");

        // 1) 네트워크 매니저가 종료 처리(이벤트 발행 포함)
        NetworkManager.Instance.OnHandshakeFailed("HandshakeFail");

        // 2) 세션 컨텍스트 리셋
        SessionContext.Instance.ResetForReconnect();

        //  여기서 SceneRouter.Load 같은 것도 하지 않는 게 정석
        //    -> ClientFlow가 Failed 이벤트 받아서 Login으로 보내는 정책 처리
    }

    public static void SC_ForcedDisconnectHandler(PacketSession session, IPacket packet)
    {
        var p = (SC_ForcedDisconnect)packet;

        Debug.LogWarning("[Network] Forced disconnect by server");

        NetworkManager.Instance.OnForcedDisconnect("ForcedDisconnect");
        SessionContext.Instance.ResetForReconnect();
    }

    public static void SC_InitMapHandler(PacketSession session, IPacket packet)
    {
        var p = (SC_InitMap)packet;
        Debug.Log("[IN]SC_InitMapHandler");
        // 1) 컨텍스트 저장
        SessionContext.Instance.ApplyInitMap(
            rev: p.Revision,
            tickRate: p.TickRate,
            mapId: p.MapId,
            mapVersion: "Test",//p.MapVersion,  // 서버 필드이름 사용 TODO : 
            myActorId: p.MyActorId,
            map: p

        );
        if (p.Mode == 1)
        {
            var townCtx = UnityEngine.Object.FindFirstObjectByType<TownSceneContext>();
            if (townCtx != null)
            {
                townCtx.OnInitMap(p);
            }
            else
            {
                // 씬 아직 준비 전이면 세션 컨텍스트만 저장된 상태.
                // TownSceneContext.EnterTownAsync()에서 SessionContext.InitMapReceived 체크하고 처리 가능.
                Debug.LogWarning("[SC_InitMap] TownSceneContext not found (scene not ready yet?)");
            }

        }
        else if (p.Mode == 2)
        {
            var gameCtx = UnityEngine.Object.FindFirstObjectByType<GameSceneContext>();
            if (gameCtx != null)
            {
                gameCtx.OnInitMap(p);
            }
            else
            {
                // 씬 아직 준비 전이면 세션 컨텍스트만 저장된 상태.
                // TownSceneContext.EnterTownAsync()에서 SessionContext.InitMapReceived 체크하고 처리 가능.
                Debug.LogWarning("[SC_InitMap] TownSceneContext not found (scene not ready yet?)");
            }

        }

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

    public static void SC_ActionInstantBroadcastHandler(PacketSession session, IPacket packet)
         => ClientHandlers.Instance.Handle_SC_ActionInstantBroadcast((SC_ActionInstantBroadcast)packet);

    public static void SC_BeatActionsHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_BeatActions((SC_BeatActions)packet);
    public static void SC_BeatSyncHandler(PacketSession session, IPacket packet)
         => ClientHandlers.Instance.Handle_SC_BeatSync((SC_BeatSync)packet);

    public static void SC_BeatTelegraphsHandler(PacketSession session, IPacket packet)
         => ClientHandlers.Instance.Handle_SC_BeatTelegraphs((SC_BeatTelegraphs)packet);

    public static void SC_EntityDespawnHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_EntityDespawn((SC_EntityDespawn)packet);

    public static void SC_CancelActionHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_CancelAction((SC_CancelAction)packet);

    public static void SC_EntitySpawnHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_EntitySpawnHandler((SC_EntitySpawn)packet);

    public static void SC_ReturnToTownHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_ReturnToTown((SC_ReturnToTown)packet);

    public static void SC_InventoryHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_Inventory((SC_Inventory)packet);

    public static void SC_EquipResultHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_EquipResult((SC_EquipResult)packet);

    public static void SC_UpdateSkillSlotsHandler(PacketSession session, IPacket packet)
        => ClientHandlers.Instance.Handle_SC_UpdateSkillSlots((SC_UpdateSkillSlots)packet);
}
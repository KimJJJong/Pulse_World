using ControlPlane.Grpc.V1;
using Server;
using Server.Presentation.Tcp;               // IAuthedTcpConnection (내가 만든 확장 인터페이스)
using ServerCore;
using Shared;
// [ping-fix] ClientSession 의 모든 Console.WriteLine 을 LogManager 로 대체하여
// stdout 동기 I/O 로 인한 수신 스레드 블로킹 제거.
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

public class ClientSession : PacketSession, ITcpConnection, IAuthedTcpConnection
{
    private const string TownRelayKeyPrefix = "townp2p:";

    // ---- ITcpConnection ----
    public string ConnId { get; } = Guid.NewGuid().ToString("N");
    public bool IsConnected { get; private set; } = false;

    private readonly CancellationTokenSource _cts = new();
    public CancellationToken ConnectionToken => _cts.Token;

    // ---- IAuthedTcpConnection ----
    public bool HasAuth { get; private set; } = false;
    public string Uid { get; private set; } = "";
    public long Epoch { get; private set; } = 0;
    public string Key { get;  set; } = ""; // roomId 같은 ctx


    public int ActorId { get; set; } = -1;
    private string _currentWorldId = "";
    public override string CurrentWorldId
    {
        get => _currentWorldId;
        set
        {
            var next = value ?? "";
            if (string.Equals(_currentWorldId, next, StringComparison.Ordinal))
                return;

            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=world_change uid={UidOrDash()} epoch={Epoch} conn={ConnId} actor={ActorId} seat={SeatIndex} key={KeyOrDash()} from={WorldOrDash(_currentWorldId)} to={WorldOrDash(next)}");
            _currentWorldId = next;
        }
    }
    public int SeatIndex {  get; set; } = -1;

  
   


    public long LastPingAtMs { get; set; } = 0;
    public int LastPingSeq { get; set; } = -1;

    public override void OnConnected(EndPoint endPoint)
    {
        IsConnected = true;
        // [ping-fix] Console.WriteLine → LogManager
        LogManager.Instance.LogInfo("ClientSession", $"Connected ep={endPoint} connId={ConnId}");
        LogManager.Instance.LogInfo("SessionLifecycle", $"event=connect conn={ConnId} endpoint={endPoint}");
    }

    public override void OnRecvPacket(ArraySegment<byte> buffer)
    {
        PacketManager.Instance.OnRecvPacket(this, buffer);
    }

    public override void OnDisconnected(EndPoint endPoint)
    {
        IsConnected = false;
        try { _cts.Cancel(); } catch { }
        // [ping-fix] Console.WriteLine → LogManager
        LogManager.Instance.LogInfo("ClientSession", $"OnDisconnected ep={endPoint} connId={ConnId}");
        LogManager.Instance.LogInfo(
            "SessionLifecycle",
            $"event=disconnect uid={UidOrDash()} epoch={Epoch} conn={ConnId} actor={ActorId} seat={SeatIndex} key={KeyOrDash()} world={WorldOrDash(CurrentWorldId)} endpoint={endPoint}");

        // registry정리
        if (HasAuth)
        {
            LogManager.Instance.LogInfo(
                "SessionLifecycle",
                $"event=registry_unbind reason=disconnect uid={UidOrDash()} epoch={Epoch} conn={ConnId} key={KeyOrDash()} world={WorldOrDash(CurrentWorldId)}");
            ServerServices.Registry.UnbindIfMatch(Uid, ConnId, Epoch);
        }

        if (!string.IsNullOrEmpty(CurrentWorldId))
        {
            if (TownManager.TryGet(CurrentWorldId, out var world))
            {
                LogManager.Instance.LogInfo(
                    "SessionLifecycle",
                    $"event=disconnect_route action=remove roomType=Town world={CurrentWorldId} uid={UidOrDash()} epoch={Epoch} conn={ConnId}");
                world.RemovePlayer(Uid, Epoch);
            }
            else if (GameManager.TryGet(CurrentWorldId, out var game))
            {
                 // GameRoom은 재접속을 위해 Detach만 수행
                 // [ping-fix] Console.WriteLine → LogManager
                 LogManager.Instance.LogInfo("ClientSession", $"Detach from GameRoom matchId={game.MatchId}");
                 LogManager.Instance.LogInfo(
                    "SessionLifecycle",
                    $"event=disconnect_route action=detach roomType=Game world={game.MatchId} uid={UidOrDash()} epoch={Epoch} conn={ConnId}");
                 game.DetachIfMatch(Uid, Epoch, ConnId);
            }
            else if (TownP2PRelayManager.TryGet(CurrentWorldId, out var townRelay))
            {
                LogManager.Instance.LogInfo("ClientSession", $"Detach from TownP2PRelay worldId={CurrentWorldId}");
                LogManager.Instance.LogInfo(
                    "SessionLifecycle",
                    $"event=disconnect_route action=remove roomType=TownP2P world={CurrentWorldId} uid={UidOrDash()} epoch={Epoch} conn={ConnId}");
                townRelay.RemovePlayer(Uid, Epoch);
                NotifyTownRoomDisconnected(CurrentWorldId, Uid, Epoch, ConnId, "tcp_disconnect");
            }
            else if (P2PRelayManager.TryGet(CurrentWorldId, out var relay))
            {
                LogManager.Instance.LogInfo("ClientSession", $"Detach from P2PRelay worldId={CurrentWorldId}");
                LogManager.Instance.LogInfo(
                    "SessionLifecycle",
                    $"event=disconnect_route action=detach roomType=GameP2P world={CurrentWorldId} uid={UidOrDash()} epoch={Epoch} conn={ConnId}");
                relay.DetachIfMatch(Uid, Epoch, ConnId);
            }
            else
            {
                LogManager.Instance.LogWarning(
                    "SessionLifecycle",
                    $"event=disconnect_route action=missing_world world={CurrentWorldId} uid={UidOrDash()} epoch={Epoch} conn={ConnId}");
            }
        }

        SessionManager.Instance.Remove(this);
    }

    public override void OnSend(int numOfBytes)
    {
    }


    public void BindAuth(string uid, long epoch, string key)
    {
        HasAuth = true;
        Uid = uid;
        Epoch = epoch;
        Key = key ?? "";
        LogManager.Instance.LogInfo(
            "SessionLifecycle",
            $"event=auth_bind uid={UidOrDash()} epoch={Epoch} conn={ConnId} key={KeyOrDash()} world={WorldOrDash(CurrentWorldId)}");
    }

    // -----------------------------
    // Handshake 응답 전송
    // -----------------------------
    public Task SendHandshakeOkAsync(string uid, long epoch,int serverRole, string key ="")
    {
        //  네 패킷 구조에 맞춰 SC_HandshakeOk 정의해서 보내면 됨
        var p = new SC_HandshakeOk
        {
            Uid = uid,
            ServerTimeMs = Util.AppRef.ServerTimeMs(),
            SessionEpoch = epoch,
            ServerRole = serverRole,
            //key = key ?? ""      //Key -> Room id : TODO
        };
        
        Send(p.Write());
        return Task.CompletedTask;
    }

    public Task SendHandshakeFailAsync(string reason)
    {
        var p = new SC_HandshakeFail
        {
            message = reason ?? "handshake_failed"
        };

        Send(p.Write());
        return Task.CompletedTask;
    }

    // -----------------------------
    // Close
    // -----------------------------
    public void Close(string reason)
    {
        // 네 Session 종료 방식에 맞게 정리
        // [ping-fix] Console.WriteLine → LogManager
        LogManager.Instance.LogInfo("ClientSession", $"Close connId={ConnId} reason={reason}");
        LogManager.Instance.LogInfo(
            "SessionLifecycle",
            $"event=close reason={reason ?? "-"} uid={UidOrDash()} epoch={Epoch} conn={ConnId} actor={ActorId} seat={SeatIndex} key={KeyOrDash()} world={WorldOrDash(CurrentWorldId)}");
        try { _cts.Cancel(); } catch { }

        Disconnect();
    }

    public static void NotifyTownRoomDisconnected(string worldId, string uid, long epoch, string connId, string reason)
    {
        var roomId = ExtractTownRoomId(worldId);
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(uid))
        {
            LogManager.Instance.LogWarning(
                "SessionLifecycle",
                $"event=town_room_cleanup_skip reason=missing_context cleanupReason={reason ?? "-"} world={WorldOrDash(worldId)} uid={UidOrDashStatic(uid)} epoch={epoch} conn={ConnOrDash(connId)}");
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var ok = await ServerServices.ApiClient.LeaveTownRoomAsync(roomId, uid, reason ?? "server_disconnect");
                LogManager.Instance.LogInfo(
                    "SessionLifecycle",
                    $"event=town_room_cleanup result={(ok ? "ok" : "fail")} reason={reason ?? "-"} room={roomId} world={WorldOrDash(worldId)} uid={UidOrDashStatic(uid)} epoch={epoch} conn={ConnOrDash(connId)}");
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError(
                    "SessionLifecycle",
                    $"event=town_room_cleanup result=exception reason={reason ?? "-"} room={roomId} world={WorldOrDash(worldId)} uid={UidOrDashStatic(uid)} epoch={epoch} conn={ConnOrDash(connId)} err={ex.Message}");
            }
        });
    }

    private static string ExtractTownRoomId(string worldId)
    {
        if (string.IsNullOrWhiteSpace(worldId))
            return "";

        return worldId.StartsWith(TownRelayKeyPrefix, StringComparison.OrdinalIgnoreCase)
            ? worldId.Substring(TownRelayKeyPrefix.Length)
            : "";
    }

    private string UidOrDash() => string.IsNullOrWhiteSpace(Uid) ? "-" : Uid;
    private string KeyOrDash() => string.IsNullOrWhiteSpace(Key) ? "-" : Key;
    private static string WorldOrDash(string worldId) => string.IsNullOrWhiteSpace(worldId) ? "-" : worldId;
    private static string UidOrDashStatic(string uid) => string.IsNullOrWhiteSpace(uid) ? "-" : uid;
    private static string ConnOrDash(string connId) => string.IsNullOrWhiteSpace(connId) ? "-" : connId;

    // -----------------------------
    // Send helper (★ 여기만 네 코드에 맞게 연결)
    // -----------------------------
    //private void SendPacket(IPacket packet)
    //{
    //    // 네가 쓰는 ServerCore의 패킷 송신 방식에 맞춰 한 줄만 맞추면 끝.
    //    // 흔한 패턴: packet.Write() -> Send(sendBuffer)
    //    Send(sendBuffer);
    //}




}


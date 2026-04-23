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
    public override string CurrentWorldId { get; set; } = "";
    public int SeatIndex {  get; set; } = -1;

  
   


    public long LastPingAtMs { get; set; } = 0;
    public int LastPingSeq { get; set; } = -1;

    public override void OnConnected(EndPoint endPoint)
    {
        IsConnected = true;
        // [ping-fix] Console.WriteLine → LogManager
        LogManager.Instance.LogInfo("ClientSession", $"Connected ep={endPoint} connId={ConnId}");
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

        // registry정리
        if (HasAuth)
        {
            ServerServices.Registry.UnbindIfMatch(Uid, ConnId, Epoch);
        }

        if (!string.IsNullOrEmpty(CurrentWorldId))
        {
            if (TownManager.TryGet(CurrentWorldId, out var world))
            {
                world.RemovePlayer(Uid, Epoch);
            }
            else if (GameManager.TryGet(CurrentWorldId, out var game))
            {
                 // GameRoom은 재접속을 위해 Detach만 수행
                 // [ping-fix] Console.WriteLine → LogManager
                 LogManager.Instance.LogInfo("ClientSession", $"Detach from GameRoom matchId={game.MatchId}");
                 game.DetachIfMatch(Uid, Epoch, ConnId);
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
        try { _cts.Cancel(); } catch { }

        Disconnect();
    }

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


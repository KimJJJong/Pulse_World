using ServerCore;
using System;
using System.Net;
using Shared;
using System.Collections.Generic;
using Server;


public class ClientSession : PacketSession
{
    public string MatchId { get; set; }
    public string Uid { get; set; }
    public int Slot { get; set; } = -1; 
    public bool Loaded { get; set; }

    public long LastPingAtMs { get; set; } = 0;
    public int LastPingSeq { get; set; } = -1;

    public override void OnConnected(EndPoint endPoint)
    {
        // tmp : Check PlayerNum
        // Console.WriteLine($"OnConnected : {SessionID} In");

        Console.WriteLine($"GameServer와 ClientSession 이 연결되었습니다: {endPoint}");

        //    S_ReqSessionInit reqPacket = new S_ReqSessionInit();

        //Program.VerifyTownTicketAsync("test123");


    }

    public override void OnRecvPacket(ArraySegment<byte> buffer)
    {
        PacketManager.Instance.OnRecvPacket(this, buffer);
    }

    public override void OnDisconnected(EndPoint endPoint)
    {
        GameManager.TryGet(MatchId, out var room);
        room.Unbind(this);

        SessionManager.Instance.Remove(this);



        Console.WriteLine($"OnDisconnected : {endPoint}");
    }

    public override void OnSend(int numOfBytes)
    {
    }

      
}


using ServerCore;
using System;
using System.Net;
using Shared;
using System.Collections.Generic;


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

        // TODO : Client 요청에 따른 Enter 관리
        //Program.Room.Enter(this); 직접 처리 하지 않고 JobQueue : Push
        //Program.Lobby.Push(() => Program.Lobby.Enter(this));

        //Program.Room.Push(() => Program.Room.Enter(this));
        //Program.Room.Enter(this);
        Console.WriteLine($"GameServer와 ClientSession 이 연결되었습니다: {endPoint}");

        //    S_ReqSessionInit reqPacket = new S_ReqSessionInit();





    }

    public override void OnRecvPacket(ArraySegment<byte> buffer)
    {
        PacketManager.Instance.OnRecvPacket(this, buffer);
    }

    public override void OnDisconnected(EndPoint endPoint)
    {

        SessionManager.Instance.Remove(this);

/*        if (Room != null)
        {
            GameRoom room = Room;
            room.RemoveClient(PlayingID);
            Room = null;
        }


        SessionManager.Instance.Remove(this);
        LogManager.Instance.LogInfo("ClientSession", $"Disconnected: {endPoint}");*/

        Console.WriteLine($"OnDisconnected : {endPoint}");
    }

    public override void OnSend(int numOfBytes)
    {
         //Console.WriteLine($"Transferred bytes: {numOfBytes}");
    }

      
}


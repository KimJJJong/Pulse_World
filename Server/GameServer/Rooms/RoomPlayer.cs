using System;

public sealed class RoomPlayer
{
    public string Uid { get; }
    public long Epoch { get; }
    public int ActorId { get; }
    public int SeatIndex { get; }

    public ClientSession? Conn { get; private set; }

    public RoomPlayer(string uid, long epoch, int actorId, int seatIndex)
    {
        Uid = uid;
        Epoch = epoch;
        ActorId = actorId;
        SeatIndex = seatIndex;
    }

    public long LastDetachedTime { get; private set; } = 0;

    public void Attach(ClientSession s) 
    {
        Conn = s;
        LastDetachedTime = 0;
    }

    public void Detach() 
    {
        Conn = null;
        LastDetachedTime = Environment.TickCount64;
    }
}

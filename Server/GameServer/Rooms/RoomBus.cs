public interface IRoomBroadcaster
{
    void Broadcast(IPacket pkt);
    void SendTo(ClientSession s, IPacket pkt);
}

public sealed class RoomBus : IRoomBroadcaster
{
    private readonly GameRoom _room;
    public RoomBus(GameRoom room) => _room = room;

    public void Broadcast(IPacket pkt) => _room.Broadcast(pkt);
    public void SendTo(ClientSession s, IPacket pkt) => _room.SendTo(s, pkt);
}
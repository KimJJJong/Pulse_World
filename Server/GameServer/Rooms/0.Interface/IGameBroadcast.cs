/// <summary>
/// 게임 세션 내 모든 클라이언트에게 패킷 브로드캐스트용 인터페이스
/// </summary>
public interface IGameBroadcaster
{
    void Broadcast(IPacket pkt);
    void SendToSlot(int slot, IPacket pkt);
}
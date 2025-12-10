namespace Shared
{
    public record StartTicketClaims(
    string MatchId, string RoomId,
    string Side,          // "A" | "B"   || slot: int 1 ,slot: int 2;
    string Uid, string OpponentUid,
    string GsHost, int GsPort, int TickRate,
    long StartAtTick, string Nonce, string Jti);

    public record ClientHello(string Ticket, long ClientTimeMs, string ClientVer);
    public record HelloAck(bool Ok, string Code, long ServerStartTimeMs, long StartTick, int TickRate);



}

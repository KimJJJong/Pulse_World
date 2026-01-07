using Server.Domain.Auth;
using Server.Domain.Connections;

public static class ServerServices
{
    public static HandshakeFlow HandshakeFlow = default!;
    public static PresenceLeaseRenewer LeaseRenewer = default!;
    public static ConnectionRegistry Registry = default!;
}

using Server.Domain.Auth;
using Server.Domain.Connections;
using System;

public static class ServerServices
{
    private static int _initialized; // 0/1
    private static IServiceProvider? _sp;

    // === 노출할 서비스들 ===
    public static HandshakeFlow HandshakeFlow => GetRequired<HandshakeFlow>();
    public static ConnectionRegistry Registry => GetRequired<ConnectionRegistry>();
    public static PresenceLeaseRenewer LeaseRenewer => GetRequired<PresenceLeaseRenewer>();
    
    // Inventory
    public static GameServer.Content.Item.InventoryManager InventoryManager => GetRequired<GameServer.Content.Item.InventoryManager>();
    public static GameServer.Content.Item.ItemTemplateManager ItemTemplates => GetRequired<GameServer.Content.Item.ItemTemplateManager>();

    // (필요하면) CP client, options 등도 동일 패턴으로 노출 가능
    // public static GrpcControlPlaneClient Cp => GetRequired<GrpcControlPlaneClient>();

    public static void Init(IServiceProvider sp)
    {
        if (sp == null) throw new ArgumentNullException(nameof(sp));
        if (System.Threading.Interlocked.Exchange(ref _initialized, 1) == 1)
            throw new InvalidOperationException("ServerServices already initialized.");

        _sp = sp;

        // 부팅 시점에 필수 서비스가 다 있는지 '즉시' 검증
        _ = HandshakeFlow;
        _ = Registry;
        _ = LeaseRenewer;
        _ = InventoryManager;
        // Explicitly load data
        ItemTemplates.Load();
    }

    private static T GetRequired<T>() where T : class
    {
        var sp = _sp ?? throw new InvalidOperationException("ServerServices not initialized. Call ServerServices.Init() in Program.cs");
        var obj = sp.GetService(typeof(T)) as T;
        if (obj == null)
            throw new InvalidOperationException($"Required service not registered: {typeof(T).FullName}");
        return obj;
    }
}

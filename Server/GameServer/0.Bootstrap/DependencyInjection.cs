//using Microsoft.Extensions.DependencyInjection;
//using Server.Domain.Auth;
//using Server.Domain.Connections;
//using Server.Infrastructure.ControlPlaneClient;
//using Server.Presentation.Tcp.PacketHandlers;

//namespace Server.Bootstrap;

//public static class DependencyInjection
//{
//    public static IServiceCollection AddServerServices(this IServiceCollection services)
//    {
//        services.AddSingleton<GrpcControlPlaneClient>();

//        services.AddSingleton<HandshakeFlow>();
//        services.AddSingleton<PresenceLeaseRenewer>();

//        // Kick subscriber
//        services.AddHostedService<ControlEventSubscriber>();


//        services.AddSingleton<ConnectionRegistry>();
//        services.AddSingleton<IConnectionKicker>(sp => sp.GetRequiredService<ConnectionRegistry>());

//        services.AddSingleton<HandshakeHandler>();


//        return services;
//    }
//}

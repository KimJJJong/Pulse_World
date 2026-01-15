//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Server.Infrastructure.Options;

//namespace Server.Bootstrap;

//public static class OptionsRegistration
//{
//    public static IServiceCollection AddServerOptions(this IServiceCollection services, IConfiguration config)
//    {
//        services.Configure<ControlPlaneOptions>(config.GetSection("ControlPlane"));
//        services.Configure<ServerIdentityOptions>(config.GetSection("ServerIdentity"));
//        return services;
//    }
//}

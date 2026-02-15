using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Server.Bootstrap;

public static class DependencyInjection
{
    public static IServiceCollection AddServerServices(this IServiceCollection services, HostBuilderContext context)
    {
        // Moved to ServerHost.cs
        // This file is currently unused but kept for structure if needed later.
        
        return services;
    }
}

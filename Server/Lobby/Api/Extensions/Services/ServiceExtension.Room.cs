using Lobby.Infrastructure.Persistence.InMemory;
using Lobby.Infrastructure.Realtime;
using Lobby.Domain.Auth.Interface;
using Lobby.Api.WebSockets;
using Lobby.Infrastructure.Lifecycle;

namespace Lobby.Api.Extensions;

public static partial class ServiceExtensions
{
    public static IServiceCollection AddLobbyRoomServices(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryRoomStore>();
        services.AddSingleton<IRoomRepository, RoomRepository>();
        services.AddSingleton<IRoomReadModel, RoomRepository>();
        services.AddSingleton<IRoomBroadcaster, RoomBroadcaster>();
        services.AddSingleton<IRoomLifecycle, RoomLifecycleService>();
        services.AddSingleton<ConnectionRegistry>();
        services.AddSingleton<RoomSocketHub>();
        return services;
    }
}

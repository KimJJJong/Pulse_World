using Grpc.Net.Client;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Content;
using Server.Domain.Auth;
using Server.Domain.Connections;
using Server.Infrastructure.ControlPlaneClient;
using Server.Infrastructure.Options;
using Server.Net;
using Server.Runtime;
using Server.Workers;
using Shared.ControlPlane;

namespace Server.Bootstrap;

public static class ServerHost
{
    public static IHost Build(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                // 기본: appsettings.json / appsettings.{env}.json / env / args
                // Host.CreateDefaultBuilder가 이미 해주지만,
                // 커스터마이즈 포인트가 필요하면 여기서 추가하면 됨.
            })
            .ConfigureServices((ctx, services) =>
            {
                // ---- Options 바인딩 ----
                services.AddOptions<ServerOptions>()
                    .Bind(ctx.Configuration.GetSection("Server"))
                    .Validate(o => !string.IsNullOrWhiteSpace(o.ServerId), "Server:ServerId is required")
                    .Validate(o => o.Bind.Port > 0, "Server:Bind:Port must be > 0")
                    .ValidateOnStart();

                services.AddOptions<ControlPlaneOptions>()
                    .Bind(ctx.Configuration.GetSection("ControlPlane"))
                    .Validate(o => !string.IsNullOrWhiteSpace(o.Address), "ControlPlane:Address required")
                    .ValidateOnStart();


                services.AddSingleton<GrpcControlPlaneClient>();



                // ---- RoleContext ----
                services.AddSingleton<RoleContext>();

                // ---- ControlPlane ----
                //services.AddSingleton<ControlPlaneClientFactory>();
                services.AddSingleton<ControlPlaneRegistrar>();

                // Heartbeat는 BackgroundService
                services.AddHostedService<ControlPlaneHeartbeatService>();

                // ---- TCP Listener ----
                services.AddHostedService<TcpListenerHostedService>();

                // ---- Role module 선택 등록 ----
                services.AddSingleton<IRoleModuleResolver, RoleModuleResolver>();
                services.AddSingleton<IRoleModule, GameRoleModule>();
                services.AddSingleton<IRoleModule, TownRoleModule>();

                services.AddSingleton<HandshakeFlow>();
                services.AddSingleton<ConnectionRegistry>();
                services.AddSingleton<PresenceLeaseRenewer>();


                // ---- Content ----
                services.AddHostedService<ContentInitHostedService>();

                // ---- Worker ----
                services.AddSingleton<IUpdatableSnapshotProvider, GameSnapshotProvider>();
                services.AddSingleton<IUpdatableSnapshotProvider, TownSnapshotProvider>();
                services.AddHostedService<DomainWorkerHostedService>();

                // ---- (선택) Logger, LogManager 연동 ----
                // 지금 LogManager.Instance 쓰고 있으면, ILogger로 천천히 이관 추천
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .Build();
    }
}

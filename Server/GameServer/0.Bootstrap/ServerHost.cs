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
using System;

namespace Server.Bootstrap;

public static class ServerHost
{
    public static IHost Build(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                // CreateDefaultBuilder가 기본 appsettings/appsettings.{env} + env + args 로딩을 이미 해줌.

                var role = ReadRoleArg(args) ?? ctx.Configuration["Server:Role:Name"]; // fallback

                if (!string.IsNullOrWhiteSpace(role))
                {
                    // appsettings.Town.json / appsettings.Game.json 같은 파일을 추가 로드
                    cfg.AddJsonFile($"appsettings.{role}.json", optional: true, reloadOnChange: true);
                }

                // 필요하면 여기서 더 추가 가능:
                // cfg.AddJsonFile("appsettings.Local.json", optional:true, reloadOnChange:true);
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


                // Runtime managers 이거 static으로 이용하고 있는데 이후에 바꿀 필요가 있을까?
                //builder.Services.AddSingleton<TownManager>();
                //builder.Services.AddSingleton<GameRoomManager>();


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
                //services.AddSingleton<ServerIdentityOptions>();

                services.AddSingleton<ConnectionRegistry>();
                services.AddSingleton<IConnectionKicker>(sp => sp.GetRequiredService<ConnectionRegistry>());

                services.AddSingleton<PresenceLeaseRenewer>();


                // ---- Content ----
                services.AddHostedService<ContentInitHostedService>();

                // ---- Worker ----
                services.AddSingleton<IUpdatableSnapshotProvider, GameSnapshotProvider>();
                services.AddSingleton<IUpdatableSnapshotProvider, TownSnapshotProvider>();
                services.AddHostedService<DomainWorkerHostedService>();

                // Role startups
                services.AddSingleton<IRoleStartup, TownStartup>();
                services.AddSingleton<IRoleStartup, GameStartup>();
                services.AddHostedService<RoleStartupHostedService>();

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


    static string? ReadRoleArg(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];

            if (a.StartsWith("--role=", StringComparison.OrdinalIgnoreCase))
                return a.Substring("--role=".Length);

            if (string.Equals(a, "--role", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
                return args[i + 1];
        }
        return null;
    }

}

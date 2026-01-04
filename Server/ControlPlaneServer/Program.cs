
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ControlPlaneServer.Infra;
using ControlPlaneServer.Domain.Registry;
using ControlPlaneServer.Domain.Tickets;
using ControlPlaneServer.Domain.Allocation;
using ControlPlaneServer.Domain.Transition;
using ControlPlaneServer.Domain.Presence;
using ControlPlaneServer.Services;
using ControlPlaneServer.Domain.Rooms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(o =>
{
    o.EnableDetailedErrors = true;   // 핵심
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);



builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(50051, lo => lo.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();

// Options
builder.Services.Configure<ControlPlaneOptions>(builder.Configuration.GetSection("ControlPlane"));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));

// Infra
builder.Services.AddSingleton<ControlPlaneServer.Infra.TimeProvider>();
builder.Services.AddSingleton<RedisStore>();

// Domain
builder.Services.AddSingleton<ServerRegistryService>();
builder.Services.AddSingleton<TicketService>();
builder.Services.AddSingleton<AllocatorService>();
builder.Services.AddSingleton<TransitionService>();
builder.Services.AddSingleton<ControlEventHub>();      // CP -> realtime push
builder.Services.AddSingleton<PresenceService>();      // Presence는 Hub를 사용해 Kick push
builder.Services.AddSingleton<RoomService>();

var app = builder.Build();

app.MapGrpcService<ControlPlaneGrpcService>();
app.MapGet("/", () => "ControlPlaneServer gRPC is running.");

app.Run();
using ControlPlane.Infra;
using ControlPlane.Domain.Tickets;
using ControlPlane.Domain.Registry;
using ControlPlane.Domain.Allocation;
using ControlPlane.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

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
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
builder.Services.Configure<TicketOptions>(builder.Configuration.GetSection("Tickets"));
builder.Services.Configure<RegistryOptions>(builder.Configuration.GetSection("ServerRegistry"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));

// Infra
builder.Services.AddSingleton<ControlPlane.Infra.TimeProvider>();
builder.Services.AddSingleton<RedisStore>();

// Domain
builder.Services.AddSingleton<TicketService>();
builder.Services.AddSingleton<ServerRegistryService>();
builder.Services.AddSingleton<AllocatorService>();

var app = builder.Build();

app.MapGrpcService<ControlPlaneGrpcService>();
app.MapGet("/", () => "ControlPlane gRPC running");

app.Run();

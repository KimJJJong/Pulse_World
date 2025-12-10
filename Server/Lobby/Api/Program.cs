using Lobby.Api.Extensions;
using Lobby.Api.Extensions.Auth;
using Lobby.Api.Extensions.Middleware;
using Lobby.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// =========================================
//  Config 로드 (appsettings.json + env)
// =========================================
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// =========================================
//  Serilog 설정
// =========================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Npgsql", Serilog.Events.LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} traceId={traceId} path={path} method={method}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

//  Controllers + JSON 옵션
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

//  DI: Lobby Services, DB, Redis

builder.Services.AddLobbyServices(builder.Configuration);

//  JWT Auth (RS256)

builder.Services.AddJwtAuth(builder.Configuration);

// 기타 서비스
builder.Services.AddCors(o => o.AddDefaultPolicy(
    p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRateLimiter(_ => { });

// Build App
var app = builder.Build();

//  새 DB 구조 반영된 출력
Console.WriteLine("[CONFIG] AuthDb = " + builder.Configuration.GetConnectionString("Database"));
Console.WriteLine("[CONFIG] Redis  = " + builder.Configuration.GetConnectionString("Redis"));
Console.WriteLine($"[CONFIG] Ticket:TtlSeconds = {builder.Configuration["App:Ticket:TtlSeconds"]}");
Console.WriteLine($"[CONFIG] GameServer Static Host = {builder.Configuration["App:GameServer:Static:PublicHost"]}");

//  DB Migration : TODO : DB Dump 로 Migration 교체 필요
await MigrationRunner.ApplyMigrationAsync(builder.Configuration);

//Middleware pipeline
app.UseLobbyPipeline();

app.Run();

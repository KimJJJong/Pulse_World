using ApiServer.Bootstrap;
using ApiServer.Presentation.Http.Middleware;
using System.Text.Json;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var systemApiKey = builder.Configuration["SystemApiKey"];
if (string.IsNullOrWhiteSpace(systemApiKey)
    || systemApiKey.Length < 32
    || systemApiKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
    || systemApiKey.Contains("change-me", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "SystemApiKey must be a non-placeholder secret of at least 32 characters.");
}

// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/security-.txt", 
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Warning,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Options + DI
builder.Services
    .AddApiOptions(builder.Configuration)
    .AddApiServices(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Use PascalCase (matches C# properties)
    });
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();



app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseMiddleware<FirewallMiddleware>();

app.UseRouting();

app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseMiddleware<AccessTokenAuthMiddleware>();
app.UseMiddleware<IdempotencyForGameTicketMiddleware>();

app.UseWebSockets();

app.Map("/hub/room", async context =>
{
    // Resolve scoped handler
    var handler = context.RequestServices.GetRequiredService<ApiServer.Presentation.WebSockets.RoomWebSocketHandler>();
    await handler.HandleAsync(context);
});

app.MapControllers();
app.MapControllers();
await app.ApplyMigrationsAsync();

app.Run();

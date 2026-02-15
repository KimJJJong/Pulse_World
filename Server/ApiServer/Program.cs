using ApiServer.Bootstrap;
using ApiServer.Presentation.Http.Middleware;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Options + DI
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

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

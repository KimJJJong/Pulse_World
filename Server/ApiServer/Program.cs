using ApiServer.Bootstrap;
using ApiServer.Presentation.Http.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Options + DI
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services
    .AddApiOptions(builder.Configuration)
    .AddApiServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();



app.UseMiddleware<ProblemDetailsMiddleware>();

app.UseRouting();

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

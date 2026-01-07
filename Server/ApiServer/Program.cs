using ApiServer.Bootstrap;
using ApiServer.Presentation.Http.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Options + DI
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

app.MapControllers();
app.Run();

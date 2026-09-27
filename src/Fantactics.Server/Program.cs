using Fantactics.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();

var app = builder.Build();

app.MapGet("/", () => "Fantactics server");
app.MapHub<GameHub>("/game");

app.Run();

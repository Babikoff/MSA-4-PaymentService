using NotificationService.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Health check (для установки docker-compose итд).
app.MapGet("/health", () => Results.Ok("ok"));

// REST для orchestrator workers.
app.MapNotificationEndpoints();

app.Run();

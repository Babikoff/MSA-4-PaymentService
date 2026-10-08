using Microsoft.Extensions.Options;
using Orchestrator.Models;
using Orchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

builder.Services.Configure<ZeebeOptions>(builder.Configuration.GetSection("Zeebe"));
builder.Services.Configure<ServiceUrls>(builder.Configuration.GetSection("Services"));
builder.Services.Configure<SagaOptions>(builder.Configuration.GetSection("Saga"));

builder.Services.AddSingleton<ISagaRepository, InMemorySagaRepository>();

builder.Services.AddHttpClient<IPaymentClient, PaymentServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Payment"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IFraudCheckClient, FraudCheckServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:FraudCheck"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<INotificationClient, NotificationServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Notification"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<ISagaStateService, SagaStateService>();
builder.Services.AddSingleton<IZeebeWorkerService, ZeebeWorkerService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { Service = "Orchestrator", Status = "Running" }));
app.MapGet("/ready", () => Results.Ok(new { Database = "OK" }));
app.MapGet("/sagas/{paymentId:guid}/status", async (Guid paymentId, ISagaRepository repo) =>
    await repo.GetAsync(paymentId) is { } s ? Results.Ok(s) : Results.NotFound());

var zeebeWorker = app.Services.GetRequiredService<IZeebeWorkerService>();
try
{
    await zeebeWorker.StartAsync(app.Lifetime.ApplicationStopping);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Zeebe workers not started; service still serves /health and /sagas.");
}

app.MapGet("/shutdown", async (IZeebeWorkerService w, CancellationToken ct) =>
{
    await w.StopAsync(ct);
    return Results.Ok(new { Stopped = true });
});

app.Run();

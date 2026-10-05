using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Endpoints;
using PaymentService.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройки Postgres
//TODO: проверить нужна ли захардкоженная строка соединения 
var conn = builder.Configuration.GetConnectionString("Payments")
    ?? "Host=postgres;Port=5432;Database=paymentdb;Username=orchestrpay;Password=password";
builder.Services.AddDbContext<PaymentDbContext>(o => o.UseNpgsql(conn));

// Загружаем настройки для эмулятора бизнес-логики (для прототипа).
//TODO: заменить эмуляцию в реальном продукте
builder.Services.Configure<PaymentOptions>(
    builder.Configuration.GetSection("PaymentServiceMocking"));

// Services.
builder.Services.AddScoped<DbInitializer>();
builder.Services.AddScoped<FundsService>();
builder.Services.AddScoped<IdempotencyService>();

var app = builder.Build();

// Health check (для установки docker-compose итд).
app.MapGet("/health", () => Results.Ok("ok"));

// REST для orchestrator workers.
app.MapPaymentEndpoints();

// Ensure the paymentdb database + schema on startup (prototype).
using (var scope = app.Services.CreateScope())
{
    var init = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    try { await init.InitializeAsync(); }
    catch (Exception ex)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        log.LogError(ex, "Database init failed.");
    }
}

app.Run();

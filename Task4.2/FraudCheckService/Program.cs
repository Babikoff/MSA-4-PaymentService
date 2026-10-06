using Microsoft.EntityFrameworkCore;
using FraudCheckService.Data;
using FraudCheckService.Endpoints;
using FraudCheckService.Services;

var builder = WebApplication.CreateBuilder(args);


// Настройки Postgres
//TODO: проверить нужна ли захардкоженная строка соединения 
var conn = builder.Configuration.GetConnectionString("Payments")
    ?? "Host=postgres;Port=5432;Database=fraudcheckdb;Username=orchestrpay;Password=password";
builder.Services.AddDbContext<FraudCheckDbContext>(o => o.UseNpgsql(conn));

// Загружаем настройки для эмулятора бизнес-логики (для прототипа).
//TODO: заменить эмуляцию в реальном продукте
builder.Services.Configure<FraudOptions>(
    builder.Configuration.GetSection("FraudCheckServiceMocking"));

// Services.
builder.Services.AddScoped<DbInitializer>();
builder.Services.AddScoped<FraudRulesService>();
builder.Services.AddScoped<IdempotencyService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Health check (для установки docker-compose итд).
app.MapGet("/health", () => Results.Ok("ok"));

app.MapPaymentEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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


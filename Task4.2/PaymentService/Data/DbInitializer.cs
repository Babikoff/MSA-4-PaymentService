using Microsoft.EntityFrameworkCore;

namespace PaymentService.Data;

/// <summary>
/// Инициализация базы "paymentdb".
/// </summary>
public class DbInitializer
{
    private readonly PaymentDbContext _db;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(PaymentDbContext db, ILogger<DbInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        var created = await _db.Database.EnsureCreatedAsync();
        _logger.LogInformation("Payment database ensured (created: {Created}).", created);
    }
}
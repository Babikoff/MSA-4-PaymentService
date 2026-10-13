using Microsoft.EntityFrameworkCore;

namespace FraudCheckService.Data;

/// <summary>
/// Инициализация базы "fraudcheckdb".
/// </summary>
public class DbInitializer
{
    private readonly FraudCheckDbContext _db;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(FraudCheckDbContext db, ILogger<DbInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        var created = await _db.Database.EnsureCreatedAsync();
        _logger.LogInformation("FraudCheckService database ensured (created: {Created}).", created);
    }
}
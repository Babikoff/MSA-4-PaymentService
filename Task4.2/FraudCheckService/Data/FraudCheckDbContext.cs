using Microsoft.EntityFrameworkCore;
using FraudCheckService.Domain;

namespace FraudCheckService.Data;

public class FraudCheckDbContext : DbContext
{
    public FraudCheckDbContext(DbContextOptions<FraudCheckDbContext> options)
        : base(options)
    {
    }

    public DbSet<FraudCheckCase> Payments => Set<FraudCheckCase>();
    public DbSet<OperationRecord> OperationRecords => Set<OperationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FraudCheckCase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PayerId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CounterpartyId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Amount).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(8).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);
        });

        modelBuilder.Entity<OperationRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Operation).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
        });
    }
}
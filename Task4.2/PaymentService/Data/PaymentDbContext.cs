using Microsoft.EntityFrameworkCore;
using PaymentService.Domain;

namespace PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OperationRecord> OperationRecords => Set<OperationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PayerId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CounterpartyId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(8).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(64);
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
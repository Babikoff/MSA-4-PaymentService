using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Domain;

namespace PaymentService.Services;

/// <summary>Outcome of an idempotent operation (replayed or fresh).</summary>
public sealed record OpOutcome<T>(bool Success, T? Data);

/// <summary>
/// Applies a domain operation exactly once per idempotency key. A retried job
/// (same key) gets the stored result back instead of being re-applied.
/// </summary>
public class IdempotencyService
{
    private readonly PaymentDbContext _db;

    public IdempotencyService(PaymentDbContext db) => _db = db;

    public async Task<OpOutcome<T>> ExecuteAsync<T>(
        string? key,
        Guid paymentId,
        string operation,
        Func<Payment, Task<(bool Ok, T Data)>> apply) where T : class
    {
        key ??= $"{paymentId}:{operation}";

        var existing = await _db.OperationRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key);

        if (existing is not null)
            return Replay<T>(existing);

        await using var tx = await _db.Database.BeginTransactionAsync();

        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment is null)
            throw new EntityNotFoundException(paymentId);

        // Double-check under the transaction to stay safe under concurrency.
        var concurrent = await _db.OperationRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key);
            
        if (concurrent is not null)
            return Replay<T>(concurrent);

        var (ok, data) = await apply(payment);

        _db.OperationRecords.Add(new OperationRecord
        {
            Key = key,
            PaymentId = paymentId,
            Operation = operation,
            Success = ok,
            DataJson = JsonSerializer.Serialize(data),
            FirstAttemptAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return new OpOutcome<T>(ok, data);
    }

    private static OpOutcome<T> Replay<T>(OperationRecord record) where T : class
    {
        var data = record.DataJson is null
            ? null
            : JsonSerializer.Deserialize<T>(record.DataJson);

        return new OpOutcome<T>(record.Success, data);
    }
}
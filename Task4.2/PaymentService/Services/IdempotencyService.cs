using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Domain;

namespace PaymentService.Services;

/// <summary>
/// Класс Wrapper делающий бизнес-операции идемпотентными.
/// </summary>
/// <typeparam name="T">Тип DTO-результата операции (например, <c>HoldResult</c>).</typeparam>
/// <param name="Success">Флаг успеха бизнес-операции (сохранённый из первого вызова).</param>
/// <param name="Data">Сохранённый результат первого выполнения; <c>null</c>, если он не сериализовался.</param>
public sealed record OperationOutcome<T>(bool Success, T? Data);

/// <summary>
/// Гарантирует «выполнить ровно один раз, ответить каждый раз» (паттерн Idempotency Key):
/// доменная операция применяется один раз на ключ идемпотентности,
/// а повторный запрос (ретрай job из Zeebe, at-least-once) получает сохранённый
/// результат вместо повторного применения — effectively-once поверх at-least-once.
/// </summary>
public class IdempotencyService
{
    private readonly PaymentDbContext _db;

    /// <summary>Создаёт сервис идемпотентности.</summary>
    /// <param name="db">Контекст БД: операция и запись ключа коммитятся в одной транзакции.</param>
    public IdempotencyService(PaymentDbContext db) => _db = db;

    /// <summary>
    /// Выполняет доменную операцию ровно один раз на ключ идемпотентности;
    /// повтор с тем же ключом возвращает сохранённый результат (Replay).
    /// </summary>
    /// <typeparam name="T">Тип DTO-результата операции.</typeparam>
    /// <param name="key">Ключ идемпотентности (заголовок <c>Idempotency-Key</c>); если <c>null</c> — выводится из paymentId и operation.</param>
    /// <param name="paymentId">Идентификатор платежа — агрегат, к которому применяется операция.</param>
    /// <param name="operation">Имя операции (<c>HOLD</c>, <c>TRANSFER</c>, <c>RELEASE</c>, <c>RETURN</c>, <c>COMPLETE</c>, <c>FINISH</c>).</param>
    /// <param name="apply">Бизнес-логика: меняет состояние платежа и возвращает (успех, результат).</param>
    /// <returns><see cref="OperationOutcome{T}"/>: флаг успеха и результат — первый вызов или Replay.</returns>
    /// <exception cref="EntityNotFoundException">Платеж с указанным идентификатором не найден.</exception>
    public async Task<OperationOutcome<T>> ExecuteAsync<T>(
        string? key,
        Guid paymentId,
        string operation,
        Func<Payment, Task<(bool Ok, T Data)>> apply) where T : class
    {
        // Создаём составной ключ
        key ??= $"{paymentId}:{operation}";

        // Если запись по ключу уже есть - вернём её.
        var existing = await _db.OperationRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key);

        if (existing is not null)
            return Replay<T>(existing);

        // Транзакция: изменение статуса и запись ключа коммитятся атомарно.
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        // Загрузка главной сущности 
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);

        // Если почему-то не найдена, то ошибка
        if (payment is null)
            throw new EntityNotFoundException(paymentId);

        // Повторная проверка ключа уже внутри транзакции — защита
        // от конкурентных ретраев, прошедших шаг 2 до коммита другого запроса.
        var concurrent = await _db.OperationRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key);
            
        if (concurrent is not null)
            return Replay<T>(concurrent);

        // Вызываем делегат применяющий бизнес-логику для данного экземлпяра сущности
        var (ok, data) = await apply(payment);

        // Сохраняем действие в журнала
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

        return new OperationOutcome<T>(ok, data);
    }

    /// <summary>
    /// Формирует повтор (Replay) из сохранённой записи: десериализует результат первого
    /// выполнения, чтобы повтор вернул тот же ответ (в т.ч. то же random-решение).
    /// </summary>
    /// <typeparam name="T">Тип DTO-результата операции.</typeparam>
    /// <param name="record">Запись <see cref="OperationRecord"/> с ключом и сериализованным результатом.</param>
    /// <returns><see cref="OperationOutcome{T}"/> с сохранёнными значением успеха и данными.</returns>
    private static OperationOutcome<T> Replay<T>(OperationRecord record) where T : class
    {
        var data = record.DataJson is null
            ? null
            : JsonSerializer.Deserialize<T>(record.DataJson);

        return new OperationOutcome<T>(record.Success, data);
    }
}
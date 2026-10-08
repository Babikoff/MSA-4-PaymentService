using FraudCheckService.Data;
using FraudCheckService.Domain;
using FraudCheckService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FraudCheckService.Services;

/// <summary>Эмуляция антифрод-движка (прототип).</summary>
public class FraudRulesService
{
    private readonly FraudCheckDbContext _db;
    private readonly FraudOptions _options;

    public FraudRulesService(IOptions<FraudOptions> options, FraudCheckDbContext db)
    {
        _options = options.Value;
        _db = db;
    }

    /// <summary>
    /// Возвращает существующую запись FraudCheckCase или создаёт новую в статусе IN_PROGRESS.
    /// Нужна до идемпотентности: она работает только с существующим FraudCheckCase.
    /// </summary>
    public async Task<FraudCheckCase> LoadOrCreateCaseAsync(
        Guid paymentId, FraudCheckRequest request, CancellationToken ct)
    {
        var existing = await _db.Payments
            .FirstOrDefaultAsync(c => c.PaymentId == paymentId, ct);

        if (existing is not null)
            return existing;

        var now = DateTimeOffset.UtcNow;
        var created = new FraudCheckCase
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            PayerId = request.PayerId,
            CounterpartyId = request.CounterpartyId,
            Amount = request.Amount,
            Currency = request.Currency,
            CheckType = string.IsNullOrWhiteSpace(request.CheckType)
                ? FraudCheckType.AUTO.ToString()
                : request.CheckType,
            Status = FraudCheckStatus.IN_PROGRESS,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Payments.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>
    /// Авто-проверка (ANTIFRAUD_AUTOCHECK). 
    /// AUTO -> сразу ALLOW; MANUAL -> ожидание решения оператора (AWAITING_MANUAL_CHECK).
    /// Изменения применяются к case и сохраняются слоем идемпотентности.
    /// </summary>
    public async Task AutoCheckAsync(FraudCheckCase c, FraudCheckRequest request, CancellationToken ct)
    {
        //TODO: убрать рандомные задержки (сделано для эмуляции реальной работы в прототипе)
        await Task.Delay(Random.Shared.Next(_options.MinDelayMs, _options.MaxDelayMs), ct);

        if (string.Equals(request.CheckType, FraudCheckType.MANUAL.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            c.Decision = null;
            c.Transition(FraudCheckStatus.AWAITING_MANUAL_CHECK);
            c.Comment = "Awaiting manual check (mock).";
        }
        else
        {
            c.ApplyDecision(FraudCheckDecision.ALLOW);
            c.Comment = "Auto-check passed (mock).";
        }

        c.RiskScore = 0;
    }

    /// <summary>
    /// Решение оператора (шаг ANTIFRAUD_MANUAL_CHECK): применяет ALLOW|BLOCK один раз;
    /// false -> уже принято другое решение (409).
    /// </summary>
    public async Task<bool> ApplyManualDecisionAsync(
        FraudCheckCase c,
        ManualDecisionRequest request,
        CancellationToken ct
        )
    {
        //TODO: убрать рандомные задержки (сделано для эмуляции реальной работы в прототипе)
        await Task.Delay(Random.Shared.Next(_options.MinDelayMs, _options.MaxDelayMs), ct);

        // if Decision has a value, put it into decided
        if (c.Decision is { } decided && decided != request.Decision)
            return false; // Already finalized with another decision -> 409.

        c.OperatorId = request.OperatorId;
        c.Comment = request.Comment ?? c.Comment;
        c.ApplyDecision(request.Decision);
        return true;
    }

    /// <summary>Текущее состояние проверки из БД; null, если нет.</summary>
    public async Task<FraudCheckCase?> GetCaseAsync(Guid paymentId, CancellationToken ct)
        => await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PaymentId == paymentId, ct);

    /// <summary>Очередь AWAITING_MANUAL_CHECK для оператора.</summary>
    public async Task<IReadOnlyList<PendingCheckResponse>> GetPendingAsync(CancellationToken ct)
        => await _db.Payments.AsNoTracking()
            .Where(c => c.Status == FraudCheckStatus.AWAITING_MANUAL_CHECK)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new PendingCheckResponse(c.Id, c.PaymentId, c.Amount, c.CreatedAt))
            .ToListAsync(ct);

    // === Внутренние правила (private) ===

    /// <summary>Чёрный список counterparty -> BLOCK. Простая детерминированная заготовка.</summary>
    private bool IsBlacklisted(string counterpartyId)
    {
        return false;
    }

    /// <summary>Порог суммы -> MANUAL (FraudOptions.ManualThreshold), например ≥ 100000.</summary>
    private bool ExceedsManualThreshold(decimal amount)
    {
        return false;
    }

    /// <summary>
    /// Случайное решение по вероятностям Allow/Block/Manual из FraudOptions
    /// (остаток вероятности -> MANUAL). Аналог FundsService + задержка 50–500 мс.
    /// </summary>
    private FraudCheckDecision DecideByChance()
    {
        return FraudCheckDecision.ALLOW;
    }

    /// <summary>Вычисляет RiskScore (0..100) и список сработавших правил по решению.</summary>
    private (int RiskScore, List<string> RuleHits) EvaluateRules(
        string counterpartyId, decimal amount, FraudCheckDecision decision)
    {
        return (0, new List<string> { });
    }

    ///// <summary>
    ///// enrichment: тянет платёж из PaymentService GET /api/payments/{id},
    ///// если в запросе не переданы payerId/amount. Таймаут + fallback на данные запроса.
    ///// </summary>
    //private Task EnrichFromPaymentAsync(FraudCheckCase c, FraudCheckRequest request, CancellationToken ct)
    //{
    //    return new Task();
    //}
}
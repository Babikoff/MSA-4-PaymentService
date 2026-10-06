using FraudCheckService.Domain;
using FraudCheckService.Models;
using Microsoft.Extensions.Options;

namespace FraudCheckService.Services;

/// <summary>Эмуляция антифрод-движка (прототип).</summary>
public class FraudRulesService
{
    public FraudRulesService(IOptions<FraudOptions> options/*, FraudCheckHttpClient paymentClient*/) { }

    /// <summary>
    /// Авто-проверка платежа (шаг ANTIFRAUD_AUTOCHECK).
    /// Берёт данные платежа из запроса, при необходимости enrichment из PaymentService;
    /// применяет правила + случайность; сохраняет FraudCheckCase и решение.
    /// Повтор по тому же PaymentId не создаёт новую проверку (unique-index).
    /// </summary>
    public async Task<FraudDecisionResponse> AutoCheckAsync(FraudCheckRequest request, CancellationToken ct)
    {
        return new FraudDecisionResponse(
            Guid.NewGuid(), 
            request.PaymentId,
            request.PayerId,
            request.CounterpartyId,
            request.Amount,
            request.Currency,
            request.CheckType,
            FraudCheckStatus.ANTIFRAUD_CHECKED,
            FraudCheckDecision.ALLOW,
            0, //risk scope
            "-- Test comment --",
            DateTime.Now,
            DateTime.Now
            );
    }

    /// <summary>
    /// Решение оператора (шаг ANTIFRAUD_MANUAL_CHECK): переводит AWAITING_MANUAL_CHECK
    /// в ANTIFRAUD_CHECKED / FRAUD_OPERATION_DETECTED. 409 — если уже принято другое решение.
    /// </summary>
    public async Task<FraudDecisionResponse> ApplyManualDecisionAsync(
        ManualDecisionRequest request, 
        CancellationToken ct
        )
    {
        return new FraudDecisionResponse(
            Guid.NewGuid(), 
            request.PaymentId,
            string.Empty, //request.PayerId,
            string.Empty, //request.CounterpartyId,
            0, //request.Amount,
            string.Empty, //request.Currency,
            string.Empty, //request.CheckType,
            FraudCheckStatus.ANTIFRAUD_CHECKED,
            FraudCheckDecision.ALLOW,
            0, //risk scope
            "-- Test comment MANNUAL --",
            DateTime.Now,
            DateTime.Now
            );
    }

    /// <summary>Текущее решение проверки (polling воркера оркестратора).</summary>
    public async Task<FraudDecisionResponse?> GetDecisionAsync(Guid paymentId, CancellationToken ct)
    {
/*
    Guid Id,
    Guid PaymentId,
    string PayerId,
    string CounterpartyId,
    decimal Amount,
    string Currency,
    string CheckType,
    FraudCheckStatus Status,
    FraudCheckDecision? Decision,
    int RiskScore,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt

*/

        return new FraudDecisionResponse(
            Guid.NewGuid(), 
            paymentId,
            string.Empty, //request.PayerId,
            string.Empty, //request.CounterpartyId,
            0, //request.Amount,
            string.Empty, //request.Currency,
            string.Empty, //request.CheckType,
            FraudCheckStatus.ANTIFRAUD_CHECKED,
            FraudCheckDecision.ALLOW,
            0, //risk scope
            "-- Onhock test comment --",
            DateTime.Now,
            DateTime.Now
            );
    }

    /// <summary>Полное состояние проверки (отладка/тесты); null, если нет.</summary>
    public async Task<FraudCheckCase?> GetCaseAsync(Guid paymentId, CancellationToken ct)
    {
        //TODO: load from DB
        return new FraudCheckCase() {
            PaymentId = paymentId,
            PayerId = string.Empty,
            Amount = 0,
            CheckType = FraudCheckType.AUTO.ToString(), 
            Currency = string.Empty,
            CounterpartyId = string.Empty,
            Comment = string.Empty,
            Decision = FraudCheckDecision.ALLOW,
            OperatorId = string.Empty,
            CreatedAt = DateTime.Now,
            RiskScore = 0,
            Status = FraudCheckStatus.IN_PROGRESS,
            UpdatedAt = DateTime.Now,
        };
    }

    /// <summary>Очередь AWAITING_MANUAL_CHECK для оператора.</summary>
    public async Task<IReadOnlyList<PendingCheckResponse>> GetPendingAsync(CancellationToken ct)
    {
        return new List<PendingCheckResponse> {};
    }

    // === Внутренние правила (private) ===

    /// <summary>Чёрный список counterparty → BLOCK. Простая детерминированная заготовка.</summary>
    private bool IsBlacklisted(string counterpartyId)
    {
        return false;
    }

    /// <summary>Порог суммы → MANUAL (FraudOptions.ManualThreshold), например ≥ 100000.</summary>
    private bool ExceedsManualThreshold(decimal amount)
    {
        return false;
    }

    /// <summary>
    /// Случайное решение по вероятностям Allow/Block/Manual из FraudOptions
    /// (остаток вероятности → MANUAL). Аналог FundsService + задержка 50–500 мс.
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
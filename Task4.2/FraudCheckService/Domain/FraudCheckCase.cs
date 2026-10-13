namespace FraudCheckService.Domain;

/// <summary>
/// Aggregate root: Антифрод-проверка платежа.
/// </summary>
public class FraudCheckCase
{
    public Guid Id { get; set; }

    /// <summary>Идентификатор проверяемого платежа (уникален: одна проверка на платёж).</summary>
    public Guid PaymentId { get; set; }

    public string PayerId { get; set; } = string.Empty;
    public string CounterpartyId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>Какая проверка: автоматическая или ручная.</summary>
    public string CheckType { get; set; } = "AUTO";

    public FraudCheckStatus Status { get; set; } = FraudCheckStatus.IN_PROGRESS;

    /// <summary>Финальное решение; null, пока не принято.</summary>
    public FraudCheckDecision? Decision { get; set; }

    /// <summary>Оценка риска (от 0 до 100), чем выше - тем опаснее.</summary>
    public int RiskScore { get; set; }

    /// <summary>JSON-массив имён сработавших правил (RuleHits).</summary>
    public string? RuleHitsJson { get; set; }

    /// <summary>Оператор, принявший ручное решение.</summary>
    public string? OperatorId { get; set; }
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Переход статуса с фиксацией времени.</summary>
    public void Transition(FraudCheckStatus next)
    {
        Status = next;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Фиксирует решение (и финальный статус) - один раз на проверку.</summary>
    public void ApplyDecision(FraudCheckDecision decision)
    {
        Decision = decision;
        Status = decision switch
        {
            FraudCheckDecision.ALLOW => FraudCheckStatus.ANTIFRAUD_CHECKED,
            FraudCheckDecision.BLOCK => FraudCheckStatus.FRAUD_OPERATION_DETECTED,
            _ => FraudCheckStatus.AWAITING_MANUAL_CHECK
        };
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
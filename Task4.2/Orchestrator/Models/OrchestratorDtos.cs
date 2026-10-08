namespace Orchestrator.Models;

/// <summary>DTO оркестратора (зеркало контрактов доменных сервисов).</summary>
public record CreatePaymentPayload(string PayerId, string CounterpartyId, decimal Amount, string Currency);

/// <summary>Ответ PaymentService (PaymentResponse): Id, PayerId, CounterpartyId, Status.</summary>
public record PaymentResult(Guid Id, string PayerId, string CounterpartyId, string Status);

/// <summary>Ответ hold: { HoldOk, Status } (200 ok / 409 fail).</summary>
public record HoldResult(bool HoldOk, string? Status);

/// <summary>Ответ transfer: { TransferOk, Status } (200 ok / 409 fail).</summary>
public record TransferResult(bool TransferOk, string? Status);

/// <summary>Ответ FraudCheck (FraudDecisionResponse): Decision enum строкой, RiskScore.</summary>
public record FraudDecisionResult(Guid PaymentId, string Status, string? Decision, int RiskScore);

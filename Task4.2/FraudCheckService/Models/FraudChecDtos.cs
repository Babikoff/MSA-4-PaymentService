using FraudCheckService.Domain;

namespace FraudCheckService.Models;

public record FraudCheckRequest(Guid PaymentId, string PayerId, string CounterpartyId,
                         decimal Amount, string Currency, string CheckType);
public record FraudDecisionResponse(
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
  );

public record ManualDecisionRequest(Guid PaymentId, FraudCheckDecision Decision /*ALLOW|BLOCK*/, string OperatorId, string? Comment);
public record PendingCheckResponse(Guid CheckId, Guid PaymentId, decimal Amount, DateTimeOffset CreatedAt);
public record ErrorResponse(string Error);   
public record ManualDecisionResult(bool Success);

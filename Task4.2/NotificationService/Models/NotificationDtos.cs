namespace NotificationService.Models;

public record NotifyUserRequest(
    Guid PaymentId,
    string RecipientId,
    string EventType, // `PAYMENT_FAILED` | `PAYER_NOTIFIED`)
    string PaymentStatus,
    string Message,
    string Channel
);

public record NotifySecurityRequest(
    Guid PaymentId,
    string RecipientId,
    string EventType, // `FRAUD_OPERATION_DETECTED`)
    string PaymentStatus,
    string Message,
    string[]? RuleHits,
    int? RiskScore
);

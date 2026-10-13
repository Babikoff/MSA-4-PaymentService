namespace PaymentService.Models;

public record CreatePaymentRequest(
    string? PaymentId,
    string PayerId,
    string CounterpartyId,
    decimal Amount,
    string Currency);

public record PaymentResponse(
    Guid Id,
    string PayerId,
    string CounterpartyId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record HoldResult(bool HoldOk);

public record TransferResult(bool TransferOk);

public record AckResult(bool Success);

public record ErrorResponse(string Error);
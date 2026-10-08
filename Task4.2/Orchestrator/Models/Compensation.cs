namespace Orchestrator.Models;

public record CompensationRequest(
    string JobType,
    string PaymentId,
    string? Reason);

public record CompensationResult(bool Success, string Message);

public record CompensationState(
    string JobType,
    string PaymentId,
    DateTime RequestedAt,
    bool Executed,
    string? Status);

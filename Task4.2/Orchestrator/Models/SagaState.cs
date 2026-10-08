namespace Orchestrator.Models;

public enum SagaStatus
{
    PENDING,
    CREATED,
    HELD,
    ANTIFRAUD_RUN,
    ANTIFRAUD_ALLOW,
    ANTIFRAUD_BLOCK,
    ANTIFRAUD_MANUAL,
    COMPLETED,
    CANCELED,
    COMPENSATION_NEEDED,
    COMPENSATION_PENDING
}

public class SagaState
{
    public Guid PaymentId { get; set; }
    public SagaStatus Status { get; set; } = SagaStatus.PENDING;
    public bool HoldOk { get; set; }
    public string? FraudDecision { get; set; }
    public bool TransferOk { get; set; }
    public string? CurrentJob { get; set; }
    public List<Compensation> CompensationQueue { get; } = new();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class Compensation
{
    public string JobType { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public bool Executed { get; set; }
    public string? Reason { get; set; }
}

public record NotifyResult(bool Sent, string Message);

public record SagaStartResult(Guid InstanceKey, string Status);

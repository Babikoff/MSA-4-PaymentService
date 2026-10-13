namespace PaymentService.Domain;

/// <summary>Aggregate root: состояние процесса платежа.</summary>
public class Payment
{
    public Guid Id { get; set; }
    public string PayerId { get; set; } = string.Empty;
    public string CounterpartyId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.PAYMENT_STARTED;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public void Transition(PaymentStatus next)
    {
        Status = next;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
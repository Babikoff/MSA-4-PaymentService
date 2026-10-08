namespace PaymentService.Domain;

/// <summary>Статусы процесса платежа.</summary>
public enum PaymentStatus
{
    PAYMENT_STARTED,
    FUNDS_HELD,
    FUNDS_RELEASED,
    FUNDS_TRANSFERRED,
    FUNDS_RETURNED,
    PAYMENT_PROCESS_COMPLETED,
    PAYMENT_PROCESS_CANCELED,
    FAILED
}
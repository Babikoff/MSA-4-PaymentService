using Orchestrator.Models;

namespace Orchestrator.Services;

public interface IPaymentClient
{
    Task<PaymentResult> CreateAsync(Guid paymentId, CreatePaymentPayload payload, CancellationToken ct = default);
    Task<HoldResult> HoldAsync(Guid paymentId, CancellationToken ct = default);
    Task<TransferResult> TransferAsync(Guid paymentId, CancellationToken ct = default);
    Task ReturnAsync(Guid paymentId, CancellationToken ct = default);
    Task ReleaseAsync(Guid paymentId, CancellationToken ct = default);
    Task CompleteAsync(Guid paymentId, CancellationToken ct = default);
    Task FinishAsync(Guid paymentId, CancellationToken ct = default);
    Task<PaymentResult?> GetAsync(Guid paymentId, CancellationToken ct = default);
}

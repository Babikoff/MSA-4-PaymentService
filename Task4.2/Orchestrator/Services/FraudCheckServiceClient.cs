using Orchestrator.Models;

namespace Orchestrator.Services;

public interface IFraudCheckClient
{
    Task<FraudDecisionResult> AutoCheckAsync(Guid paymentId, CreatePaymentPayload payload, CancellationToken ct = default);
}

public class FraudCheckServiceClient : IFraudCheckClient
{
    private readonly HttpClient _http;

    public FraudCheckServiceClient(HttpClient http) => _http = http;

    // POST /api/fraud/checks { paymentId, payerId, counterpartyId, amount, currency, checkType } -> FraudDecisionResponse.
    public async Task<FraudDecisionResult> AutoCheckAsync(Guid paymentId, CreatePaymentPayload payload, CancellationToken ct = default)
    {
        var body = new { paymentId, payerId = payload.PayerId, counterpartyId = payload.CounterpartyId, amount = payload.Amount, currency = payload.Currency, checkType = "AUTO" };
        using var resp = await _http.PostAsJsonAsync("/api/fraud/checks", body, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<FraudDecisionResult>(ct)
            ?? throw new InvalidOperationException($"Empty fraud response for {paymentId}");
    }
}
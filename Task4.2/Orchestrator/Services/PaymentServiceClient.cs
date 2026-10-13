using Orchestrator.Models;

namespace Orchestrator.Services;

public class PaymentServiceClient : IPaymentClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PaymentServiceClient> _logger;

    public PaymentServiceClient(HttpClient http, ILogger<PaymentServiceClient> logger)
    { _http = http; _logger = logger; }

    // POST /api/payments { paymentId, payerId, counterpartyId, amount, currency } -> PaymentResponse.
    public async Task<PaymentResult> CreateAsync(Guid paymentId, CreatePaymentPayload payload, CancellationToken ct = default)
    {
        var body = new { paymentId = paymentId.ToString(), payerId = payload.PayerId, counterpartyId = payload.CounterpartyId, amount = payload.Amount, currency = payload.Currency };
        using var resp = await PostWithRetryAsync(() => _http.PostAsJsonAsync("/api/payments", body, ct), ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PaymentResult>(ct)
            ?? throw new InvalidOperationException($"Empty create response for {paymentId}");
    }

    // POST /api/payments/{id}/hold -> { holdOk, status } (200 ok / 409 hold-fail).
    public async Task<HoldResult> HoldAsync(Guid paymentId, CancellationToken ct = default)
    {
        using var resp = await PostWithRetryAsync(() => _http.PostAsync($"/api/payments/{paymentId}/hold", null, ct), ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            return await resp.Content.ReadFromJsonAsync<HoldResult>(ct) ?? new HoldResult(false, "FUNDS_HOLD_FAILED");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<HoldResult>(ct)
            ?? throw new InvalidOperationException($"Empty hold response for {paymentId}");
    }

    // POST /api/payments/{id}/transfer -> { transferOk, status } (200 ok / 409 fail).
    public async Task<TransferResult> TransferAsync(Guid paymentId, CancellationToken ct = default)
    {
        using var resp = await PostWithRetryAsync(() => _http.PostAsync($"/api/payments/{paymentId}/transfer", null, ct), ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            return await resp.Content.ReadFromJsonAsync<TransferResult>(ct) ?? new TransferResult(false, "TRANSFER_FAILED");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TransferResult>(ct)
            ?? throw new InvalidOperationException($"Empty transfer response for {paymentId}");
    }

    public async Task ReturnAsync(Guid paymentId, CancellationToken ct = default)
    {
        using var resp = await PostWithRetryAsync(() => _http.PostAsync($"/api/payments/{paymentId}/return", null, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ReleaseAsync(Guid paymentId, CancellationToken ct = default)
    {
        using var resp = await PostWithRetryAsync(() => _http.PostAsync($"/api/payments/{paymentId}/release", null, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task CompleteAsync(Guid paymentId, CancellationToken ct = default)
    {
        using var resp = await PostWithRetryAsync(() => _http.PostAsync($"/api/payments/{paymentId}/complete", null, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task FinishAsync(Guid paymentId, CancellationToken ct = default)
    {
        using var resp = await PostWithRetryAsync(() => _http.PostAsync($"/api/payments/{paymentId}/finish", null, ct), ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<PaymentResult?> GetAsync(Guid paymentId, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"/api/payments/{paymentId}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PaymentResult>(ct);
    }

    // Simple retry for transient 5xx / network errors (idempotent endpoints).
    private async Task<HttpResponseMessage> PostWithRetryAsync(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var resp = await send();
                if ((int)resp.StatusCode < 500 || attempt >= 2) return resp;
                resp.Dispose();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (attempt >= 2) throw;
                _logger.LogWarning(ex, "Payment call failed, retry {Attempt}", attempt + 1);
            }
            await Task.Delay(200 * (attempt + 1), ct);
        }
    }
}
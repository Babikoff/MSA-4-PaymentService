namespace Orchestrator.Services;

public interface INotificationClient
{
    Task SendUserNotificationAsync(Guid paymentId, string recipientId, string eventType, string paymentStatus, string message, CancellationToken ct = default);
    Task SendSecurityNotificationAsync(Guid paymentId, string paymentStatus, string message, int riskScore, IReadOnlyList<string>? ruleHits, CancellationToken ct = default);
}

public class NotificationServiceClient : INotificationClient
{
    private readonly HttpClient _http;

    public NotificationServiceClient(HttpClient http) => _http = http;

    // POST /api/notifications/user { paymentId, recipientId, eventType, paymentStatus, message, channel } -> User notified.
    public async Task SendUserNotificationAsync(Guid paymentId, string recipientId, string eventType, string paymentStatus, string message, CancellationToken ct = default)
    {
        var body = new { paymentId, recipientId, eventType, paymentStatus, message, channel = "PUSH" };
        using var resp = await _http.PostAsJsonAsync("/api/notifications/user", body, ct);
        resp.EnsureSuccessStatusCode();
    }

    // POST /api/notifications/security { paymentId, recipientId, eventType, paymentStatus, message, ruleHits, riskScore }.
    public async Task SendSecurityNotificationAsync(Guid paymentId, string paymentStatus, string message, int riskScore, IReadOnlyList<string>? ruleHits, CancellationToken ct = default)
    {
        var body = new { paymentId, recipientId = "security", eventType = "FRAUD_OPERATION_DETECTED", paymentStatus, message, ruleHits = ruleHits ?? Array.Empty<string>(), riskScore };
        using var resp = await _http.PostAsJsonAsync("/api/notifications/security", body, ct);
        resp.EnsureSuccessStatusCode();
    }
}
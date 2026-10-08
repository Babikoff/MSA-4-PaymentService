using System.Text.Json;
using Microsoft.Extensions.Options;
using Orchestrator.Models;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace Orchestrator.Services;

/// <summary>
/// Zeebe workers: 11 saga job types. Sync handlers (zb-client API):
/// sync handler + GetAwaiter().GetResult() for domain REST calls.
/// Fail-open: exceptions logged, job failed with retries so saga can retry.
/// </summary>
public class ZeebeWorkerService : IZeebeWorkerService, IDisposable
{
    private readonly ILogger<ZeebeWorkerService> _logger;
    private readonly ISagaStateService _saga;
    private readonly IPaymentClient _paymentClient;
    private readonly IFraudCheckClient _fraudCheckClient;
    private readonly INotificationClient _notificationClient;
    private readonly ZeebeOptions _zeebeOptions;
    private readonly SagaOptions _options;
    private readonly List<IDisposable> _workers = new();
    private IZeebeClient? _client;
    private volatile bool _running;
    public bool IsRunning => _running;

    public ZeebeWorkerService(
        ILogger<ZeebeWorkerService> logger,
        ISagaStateService saga,
        IPaymentClient paymentClient,
        IFraudCheckClient fraudCheckClient,
        INotificationClient notificationClient,
        IOptions<ZeebeOptions> zeebeOptions,
        IOptions<SagaOptions> options)
    {
        _logger = logger; _saga = saga; _paymentClient = paymentClient;
        _fraudCheckClient = fraudCheckClient; _notificationClient = notificationClient;
        _zeebeOptions = zeebeOptions.Value; _options = options.Value;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Zeebe worker starting on gateway {Address}", _zeebeOptions.GatewayAddress);
        _client = _zeebeOptions.UsePlaintext
            ? ZeebeClient.Builder().UseGatewayAddress(_zeebeOptions.GatewayAddress).UsePlainText().Build()
            : ZeebeClient.Builder().UseGatewayAddress(_zeebeOptions.GatewayAddress).UseTransportEncryption().Build();

        var c = _client;
        Open(c, "CREATE_PAYMENT", HandleCreatePayment);
        Open(c, "HOLD_FUNDS", HandleHoldFunds);
        Open(c, "ANTIFRAUD_AUTOCHECK", HandleAntifraudAutoCheck);
        Open(c, "ANTIFRAUD_MANUAL_CHECK", HandleAntifraudManualCheck);
        Open(c, "TRANSFER_FUNDS", HandleTransfer);
        Open(c, "COMPLETE_PAYMENT", HandleComplete);
        Open(c, "RETURN_FUNDS", HandleReturn);
        Open(c, "RELEASE_FUNDS", HandleRelease);
        Open(c, "FINISH_PAYMENT_PROCESS", HandleFinish);
        Open(c, "SEND_NOTIFICATION_TO_USER", HandleNotifyUser);
        Open(c, "SEND_NOTIFICATION_TO_SECURITY", HandleNotifySecurity);

        _running = true;
        _logger.LogInformation("Zeebe worker subscribed to 11 job types.");
        return Task.CompletedTask;
    }

    private void Open(IZeebeClient client, string type, Action<IJobClient, IJob> handler)
    {
        _workers.Add(client.NewWorker()
            .JobType(type)
            .Handler((jc, job) =>
            {
                try { handler(jc, job); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled in {JobType} key={Key}", type, job.Key);
                    try { jc.NewFailCommand(job.Key).Retries(job.Retries - 1).ErrorMessage(ex.Message).Send(); } catch { }
                }
            })
            .MaxJobsActive(_options.MaxJobsToActivate)
            .Name(Environment.MachineName)
            .Timeout(TimeSpan.FromMilliseconds(_options.JobLocalTimeoutMs))
            .PollInterval(TimeSpan.FromSeconds(1))
            .Open());
    }

    // Step CREATE_PAYMENT: POST /api/payments -> PAYMENT_STARTED.
    private void HandleCreatePayment(IJobClient jc, IJob job)
    {
        var vars = Parse(job.Variables);
        var paymentId = GetGuid(vars, "paymentId");
        var payload = new CreatePaymentPayload(
            GetString(vars, "payerId") ?? "unknown",
            GetString(vars, "counterpartyId") ?? "unknown",
            GetDecimal(vars, "amount"),
            GetString(vars, "currency") ?? "USD");
        _logger.LogInformation("CREATE_PAYMENT {PaymentId}", paymentId);
        Run(_paymentClient.CreateAsync(paymentId, payload));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), status = "PAYMENT_STARTED" }))
            .Send();
        var state = Run(_saga.CreateAsync(paymentId, "CREATE_PAYMENT"));
        state.Status = SagaStatus.CREATED;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step HOLD_FUNDS: POST .../hold -> holdOk (200 ok / 409 fail -> default branch).
    private void HandleHoldFunds(IJobClient jc, IJob job)
    {
        var paymentId = GetGuid(Parse(job.Variables), "paymentId");
        _logger.LogInformation("HOLD_FUNDS {PaymentId}", paymentId);
        var hold = Run(_paymentClient.HoldAsync(paymentId));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), holdOk = hold.HoldOk }))
            .Send();
        var state = Run(_saga.GetAsync(paymentId)) ?? new SagaState { PaymentId = paymentId };
        state.HoldOk = hold.HoldOk;
        state.Status = hold.HoldOk ? SagaStatus.HELD : SagaStatus.CANCELED;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step ANTIFRAUD_AUTOCHECK: POST /api/fraud/checks -> fraudDecision.
    private void HandleAntifraudAutoCheck(IJobClient jc, IJob job)
    {
        var vars = Parse(job.Variables);
        var paymentId = GetGuid(vars, "paymentId");
        _logger.LogInformation("ANTIFRAUD_AUTOCHECK {PaymentId}", paymentId);
        var payment = Run(_paymentClient.GetAsync(paymentId));
        var payload = new CreatePaymentPayload(
            GetString(vars, "payerId") ?? payment?.PayerId ?? "unknown",
            GetString(vars, "counterpartyId") ?? payment?.CounterpartyId ?? "unknown",
            GetDecimal(vars, "amount"),
            GetString(vars, "currency") ?? "USD");
        var fraud = Run(_fraudCheckClient.AutoCheckAsync(paymentId, payload));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), fraudDecision = fraud.Decision ?? "MANUAL", riskScore = fraud.RiskScore }))
            .Send();
        var state = Run(_saga.GetAsync(paymentId)) ?? new SagaState { PaymentId = paymentId };
        state.FraudDecision = fraud.Decision;
        state.Status = SagaStatus.ANTIFRAUD_RUN;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step ANTIFRAUD_MANUAL_CHECK: re-read decision (polling operator).
    private void HandleAntifraudManualCheck(IJobClient jc, IJob job)
    {
        var paymentId = GetGuid(Parse(job.Variables), "paymentId");
        _logger.LogInformation("ANTIFRAUD_MANUAL_CHECK {PaymentId}", paymentId);
        var fraud = Run(_fraudCheckClient.AutoCheckAsync(paymentId,
            new CreatePaymentPayload("unknown", "unknown", 0, "USD")));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), fraudDecision = fraud.Decision ?? "MANUAL" }))
            .Send();
        var state = Run(_saga.GetAsync(paymentId)) ?? new SagaState { PaymentId = paymentId };
        state.FraudDecision = fraud.Decision;
        state.Status = SagaStatus.ANTIFRAUD_MANUAL;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step TRANSFER_FUNDS: POST .../transfer -> transferOk.
    private void HandleTransfer(IJobClient jc, IJob job)
    {
        var paymentId = GetGuid(Parse(job.Variables), "paymentId");
        _logger.LogInformation("TRANSFER_FUNDS {PaymentId}", paymentId);
        var transfer = Run(_paymentClient.TransferAsync(paymentId));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), transferOk = transfer.TransferOk }))
            .Send();
        var state = Run(_saga.GetAsync(paymentId)) ?? new SagaState { PaymentId = paymentId };
        state.TransferOk = transfer.TransferOk;
        state.Status = SagaStatus.COMPENSATION_NEEDED;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step COMPLETE_PAYMENT -> PAYMENT_PROCESS_COMPLETED.
    private void HandleComplete(IJobClient jc, IJob job)
    {
        var paymentId = GetGuid(Parse(job.Variables), "paymentId");
        _logger.LogInformation("COMPLETE_PAYMENT {PaymentId}", paymentId);
        Run(_paymentClient.CompleteAsync(paymentId));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), status = "PAYMENT_PROCESS_COMPLETED" }))
            .Send();
        var state = Run(_saga.GetAsync(paymentId)) ?? new SagaState { PaymentId = paymentId };
        state.Status = SagaStatus.COMPLETED;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step RETURN_FUNDS (cancel / transfer-fail) -> FUNDS_RETURNED.
    private void HandleReturn(IJobClient jc, IJob job)
    {
        var paymentId = GetGuid(Parse(job.Variables), "paymentId");
        _logger.LogInformation("RETURN_FUNDS {PaymentId}", paymentId);
        Run(_paymentClient.ReturnAsync(paymentId));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), status = "FUNDS_RETURNED" }))
            .Send();
        var state = Run(_saga.GetAsync(paymentId)) ?? new SagaState { PaymentId = paymentId };
        state.Status = SagaStatus.CANCELED;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step RELEASE_FUNDS (fraud-block / cancel-hold) -> FUNDS_RELEASED.
    private void HandleRelease(IJobClient jc, IJob job)
    {
        var paymentId = GetGuid(Parse(job.Variables), "paymentId");
        _logger.LogInformation("RELEASE_FUNDS {PaymentId}", paymentId);
        Run(_paymentClient.ReleaseAsync(paymentId));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), status = "FUNDS_RELEASED" }))
            .Send();
        var state = Run(_saga.GetAsync(paymentId)) ?? new SagaState { PaymentId = paymentId };
        state.Status = SagaStatus.COMPENSATION_PENDING;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step FINISH_PAYMENT_PROCESS -> PAYMENT_PROCESS_CANCELED.
    private void HandleFinish(IJobClient jc, IJob job)
    {
        var paymentId = GetGuid(Parse(job.Variables), "paymentId");
        _logger.LogInformation("FINISH_PAYMENT_PROCESS {PaymentId}", paymentId);
        Run(_paymentClient.FinishAsync(paymentId));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { paymentId = paymentId.ToString(), status = "PAYMENT_PROCESS_CANCELED" }))
            .Send();
        var state = Run(_saga.GetAsync(paymentId)) ?? new SagaState { PaymentId = paymentId };
        state.Status = SagaStatus.CANCELED;
        state.CurrentJob = null;
        Run(_saga.SaveAsync(state));
    }

    // Step SEND_NOTIFICATION_TO_USER: POST /api/notifications/user.
    private void HandleNotifyUser(IJobClient jc, IJob job)
    {
        var vars = Parse(job.Variables);
        var paymentId = GetGuid(vars, "paymentId");
        var eventType = GetString(vars, "eventType") ?? "PAYER_NOTIFIED";
        var paymentStatus = GetString(vars, "paymentStatus") ?? GetString(vars, "status") ?? "PAYMENT_PROCESS_COMPLETED";
        _logger.LogInformation("SEND_NOTIFICATION_TO_USER {PaymentId} ({EventType})", paymentId, eventType);
        Run(_notificationClient.SendUserNotificationAsync(paymentId, "user", eventType, paymentStatus, eventType));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { notificationSent = true }))
            .Send();
    }

    // Step SEND_NOTIFICATION_TO_SECURITY: POST /api/notifications/security.
    private void HandleNotifySecurity(IJobClient jc, IJob job)
    {
        var vars = Parse(job.Variables);
        var paymentId = GetGuid(vars, "paymentId");
        var riskScore = GetInt(vars, "riskScore");
        _logger.LogInformation("SEND_NOTIFICATION_TO_SECURITY {PaymentId}", paymentId);
        Run(_notificationClient.SendSecurityNotificationAsync(paymentId, "FRAUD_OPERATION_DETECTED", "fraud detected", riskScore, null));
        jc.NewCompleteJobCommand(job.Key)
            .Variables(JsonSerializer.Serialize(new { securityNotified = true }))
            .Send();
    }

    private static Dictionary<string, JsonElement> Parse(string variables)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(variables)
                ?? new Dictionary<string, JsonElement>();
        }
        catch { return new Dictionary<string, JsonElement>(); }
    }

    private static Guid GetGuid(Dictionary<string, JsonElement> vars, string name)
    {
        if (vars.TryGetValue(name, out var el) && el.ValueKind == JsonValueKind.String
            && Guid.TryParse(el.GetString(), out var g)) return g;
        throw new InvalidOperationException($"Job variable '{name}' (GUID) is required.");
    }

    private static string? GetString(Dictionary<string, JsonElement> vars, string name) =>
        vars.TryGetValue(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static decimal GetDecimal(Dictionary<string, JsonElement> vars, string name)
    {
        if (!vars.TryGetValue(name, out var el)) return 0;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetDecimal(out var d) ? d : 0,
            JsonValueKind.String => decimal.TryParse(el.GetString(), out var d) ? d : 0,
            _ => 0
        };
    }

    private static int GetInt(Dictionary<string, JsonElement> vars, string name)
    {
        if (!vars.TryGetValue(name, out var el)) return 0;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out var i) ? i : 0,
            JsonValueKind.String => int.TryParse(el.GetString(), out var i) ? i : 0,
            _ => 0
        };
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _running = false;
        foreach (var w in _workers) { try { w.Dispose(); } catch { } }
        _workers.Clear();
        try { _client?.Dispose(); } catch { }
        _client = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var w in _workers) { try { w.Dispose(); } catch { } }
        try { _client?.Dispose(); } catch { }
    }

    // Helpers: run async REST/state calls from sync zb-client handler.
    private static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();
    private static void Run(Task task) => task.GetAwaiter().GetResult();
}

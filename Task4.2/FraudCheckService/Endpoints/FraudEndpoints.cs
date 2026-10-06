using Microsoft.AspNetCore.Mvc;
using FraudCheckService.Domain;
using FraudCheckService.Models;
using FraudCheckService.Services;

namespace FraudCheckService.Endpoints;

public static class FraudEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        /* Нужно реализовать:
        POST	/api/fraud/checks	ANTIFRAUD_AUTOCHECK	Автопроверка. Идемпотентна (Idempotency-Key/paymentId:AUTOCHECK). Если платёж не передан в теле — тянет GET PaymentService /api/payments/{id} (enrichment, таймаут по F8). Возвращает FraudDecisionResponse
        POST	/api/fraud/checks/{paymentId}/manual-decision	ANTIFRAUD_MANUAL_CHECK	Финальное решение оператора → статус ANTIFRAUD_CHECKED / FRAUD_OPERATION_DETECTED. Идемпотентна (:MANUAL_DECISION); 409 при повторе с другим решением
        GET	/api/fraud/checks/{paymentId}/decision	polling worker'а	Текущее решение (для долгого polling'а ANTIFRAUD_MANUAL_CHECK-воркером оркестратора, пока оператор не решил)
        GET	/api/fraud/checks/{paymentId}	—	Состояние проверки (отладка/тесты)
        GET	/api/fraud/checks/pending	—	Очередь AWAITING_MANUAL_CHECK для оператора 
         */

        var group = app.MapGroup("/api/fraud/checks");

        // Анто-проверка (ANTIFRAUD_AUTOCHECK) + решение оператора (ANTIFRAUD_MANUAL_CHECK).
        group.MapPost("/", AutoCheckAsync);
        group.MapPost("/{paymentId:guid}/manual-decision", ManualDecideAsync);
        // Polling-решение для воркера оркестратора; полное состояние и очередь оператора.
        group.MapGet("/{paymentId:guid}/decision", DecisionAsync);
        group.MapGet("/{paymentId:guid}", GetAsync);
        group.MapGet("/pending", PendingAsync);
    }

    private static string IdemKey(HttpRequest req, Guid id, string op) =>
        req.Headers.TryGetValue("Idempotency-Key", out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.ToString()
            : $"{id}:{op}";

    // Шаг ANTIFRAUD_AUTOCHECK: авто-проверка платежа (идемпотентна по ключу).
    private static async Task<IResult> AutoCheckAsync(
        [FromBody] FraudCheckRequest? req,
        HttpRequest http,
        IdempotencyService idem,
        FraudRulesService rules,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (req is null)
            return Results.BadRequest(new ErrorResponse("The request is empty."));

        if (Guid.Empty.Equals(req.PaymentId))
            return Results.BadRequest(new ErrorResponse("PaymentId (GUID) is required."));

        try
        {
            var outcome = await idem.ExecuteAsync<FraudDecisionResponse>(
                IdemKey(http, req.PaymentId, FraudCheckType.AUTO.ToString()),
                req.PaymentId,
                "AUTOCHECK",
                //() => rules.LoadOrCreateCaseAsync(paymentId, req, ct),
                async c =>
                {
                    var resp = await rules.AutoCheckAsync(req, ct);
                    return (true, resp);
                });

            logger.LogInformation("Auto-check {PaymentId} -> {Decision}.",
                req.PaymentId, outcome.Data!.Decision);
            return Results.Ok(outcome.Data);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    // Шаг ANTIFRAUD_MANUAL_CHECK: финальное решение оператора (ALLOW|BLOCK).
    private static async Task<IResult> ManualDecideAsync(
        Guid paymentId,
        [FromBody] ManualDecisionRequest? req,
        HttpRequest http,
        IdempotencyService idem,
        FraudRulesService rules,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (req is null ||
            (req.Decision != FraudCheckDecision.ALLOW && req.Decision != FraudCheckDecision.BLOCK))
            return Results.BadRequest(new ErrorResponse("Decision must be ALLOW or BLOCK."));

        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return Results.BadRequest(new ErrorResponse("OperatorId is required."));

        try
        {
            var outcome = await idem.ExecuteAsync<FraudDecisionResponse>(
                IdemKey(http, paymentId, FraudCheckType.MANUAL.ToString()), paymentId, FraudCheckType.MANUAL.ToString(),
                //() => rules.LoadCaseAsync(paymentId, ct), // нет проверки -> 404
                async c =>
                {
                    var resp = await rules.ApplyManualDecisionAsync(req, ct);
                    return (true, resp);
                });

            //TODO: check
            // if (!outcome.Success)
            //     // Уже принято другое решение или нет ожидания ручной проверки.
            //     return Results.Conflict(outcome.Data ??
            //         new ErrorResponse("Decision already finalized or not awaiting manual check."));

            logger.LogInformation("Manual decision for {PaymentId}: {Decision} by {Operator}.",
                paymentId, req.Decision, req.OperatorId);

            return Results.Ok(outcome.Data);
        }
        catch (EntityNotFoundException)
        {
            return Results.NotFound(new ErrorResponse($"Fraud check for payment {paymentId} not found."));
        }
    }

    private static FraudDecisionResponse ToResponse(FraudCheckCase p) => 
        new (
            p.Id,
            p.PaymentId,
            p.PayerId,
            p.CounterpartyId,
            p.Amount,
            p.Currency,
            p.CheckType,
            p.Status,
            p.Decision,
            p.RiskScore,
            p.Comment,
            p.CreatedAt,
            p.UpdatedAt
        );


    // Текущее решение (polling ANTIFRAUD_MANUAL_CHECK-воркером оркестратора).
    private static async Task<IResult> DecisionAsync(
        Guid paymentId, FraudRulesService rules, CancellationToken ct)
    {
        var c = await rules.GetCaseAsync(paymentId, ct);
        return c is null
            ? Results.NotFound(new ErrorResponse($"Fraud check for payment {paymentId} not found."))
            : Results.Ok(ToResponse(c));
    }

    // Полное состояние проверки (отладка/тесты).
    private static async Task<IResult> GetAsync(
        Guid paymentId, FraudRulesService rules, CancellationToken ct)
    {
        var c = await rules.GetCaseAsync(paymentId, ct);
        return c is null
            ? Results.NotFound(new ErrorResponse($"Fraud check for payment {paymentId} not found."))
            : Results.Ok(ToResponse(c));
    }

    // Очередь AWAITING_MANUAL_CHECK для оператора.
    private static async Task<IResult> PendingAsync(
        FraudRulesService rules, CancellationToken ct)
        => Results.Ok(await rules.GetPendingAsync(ct));
}

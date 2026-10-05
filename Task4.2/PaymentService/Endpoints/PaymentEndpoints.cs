using Microsoft.AspNetCore.Mvc;
using PaymentService.Domain;
using PaymentService.Models;
using PaymentService.Services;

namespace PaymentService.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/payments");

        group.MapPost("/", CreateAsync);
        group.MapPost("/{id:guid}/hold", HoldAsync);
        group.MapPost("/{id:guid}/release", ReleaseAsync);
        group.MapPost("/{id:guid}/transfer", TransferAsync);
        group.MapPost("/{id:guid}/return", ReturnAsync);
        group.MapPost("/{id:guid}/complete", CompleteAsync);
        group.MapPost("/{id:guid}/finish", FinishAsync);
        group.MapGet("/{id:guid}", GetAsync);
    }

    private static string IdemKey(HttpRequest req, Guid id, string op) =>
        req.Headers.TryGetValue("Idempotency-Key", out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.ToString()
            : $"{id}:{op}";

    // Mapping
    //TODO: использовать automapper
    private static PaymentResponse ToResponse(Payment p) => new(
        p.Id, p.PayerId, p.CounterpartyId, p.Amount, p.Currency,
        p.Status.ToString(), p.CreatedAt, p.UpdatedAt);

    // Шаг CREATE_PAYMENT: идемпотентно на основе входящего PaymentId.
    private static async Task<IResult> CreateAsync(
        [FromBody] CreatePaymentRequest req,
        Data.PaymentDbContext db,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.PayerId) ||
            string.IsNullOrWhiteSpace(req.CounterpartyId) ||
            string.IsNullOrWhiteSpace(req.Currency) || req.Amount <= 0)
        {
            return Results.BadRequest(new ErrorResponse("PayerId, CounterpartyId, Currency required; Amount > 0."));
        }

        Guid id = Guid.NewGuid();
        if (!string.IsNullOrWhiteSpace(req.PaymentId))
        {
            if (!Guid.TryParse(req.PaymentId, out id))
                return Results.BadRequest(new ErrorResponse("PaymentId must be a GUID when supplied."));

            var existing = await db.Payments.FindAsync(new object[] { id }, ct);
            if (existing is not null)
            {
                logger.LogInformation("Create replay for {Id}.", id);
                return Results.Ok(ToResponse(existing));
            }
        }

        var now = DateTimeOffset.UtcNow;
        var payment = new Payment
        {
            Id = id,
            PayerId = req.PayerId,
            CounterpartyId = req.CounterpartyId,
            Amount = req.Amount,
            Currency = req.Currency,
            Status = PaymentStatus.PAYMENT_STARTED,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created payment {Id} PAYMENT_STARTED.", id);
        return Results.Created($"/api/payments/{id}", ToResponse(payment));
    }

    // Шаг HOLD_FUNDS
    private static async Task<IResult> HoldAsync(
        Guid id, HttpRequest http, IdempotencyService idem,
        FundsService funds, Data.PaymentDbContext db,
        ILogger<Program> logger)
    {
        try
        {
            var outcome = await idem.ExecuteAsync<HoldResult>(
                IdemKey(http, id, "HOLD"), id, "HOLD",
                async p =>
                {
                    var ok = await funds.HoldAsync(p);
                    logger.LogInformation("Hold {Id} -> {Status} (ok={Ok}).", id, p.Status, ok);
                    return (ok, new HoldResult(ok));
                });

            var status = (await db.Payments.FindAsync(id))?.Status.ToString();

            return outcome.Success
                ? Results.Ok(new { outcome.Data!.HoldOk, Status = status })
                : Results.Conflict(new { outcome.Data!.HoldOk, Status = status });
        }
        catch (EntityNotFoundException) { return Results.NotFound(new ErrorResponse($"Payment {id} not found.")); }
    }

    // Шаг RELEASE_FUNDS
    private static async Task<IResult> ReleaseAsync(
        Guid id, HttpRequest http, IdempotencyService idem,
        FundsService funds, ILogger<Program> logger)
    {
        try
        {
            await idem.ExecuteAsync<AckResult>(
                IdemKey(http, id, "RELEASE"), id, "RELEASE",
                async p => { await funds.ReleaseAsync(p); return (true, new AckResult(true)); }
                );

            logger.LogInformation("Release {Id} -> FUNDS_RELEASED.", id);

            return Results.Ok(new { Success = true, Status = PaymentStatus.FUNDS_RELEASED.ToString() });
        }
        catch (EntityNotFoundException) { return Results.NotFound(new ErrorResponse($"Payment {id} not found.")); }
    }

    // Шаг TRANSFER_FUNDS
    private static async Task<IResult> TransferAsync(
        Guid id, HttpRequest http, IdempotencyService idem,
        FundsService funds, Data.PaymentDbContext db,
        ILogger<Program> logger)
    {
        try
        {
            var outcome = await idem.ExecuteAsync<TransferResult>(
                IdemKey(http, id, "TRANSFER"), id, "TRANSFER",
                async p =>
                {
                    var ok = await funds.TransferAsync(p);
                    logger.LogInformation("Transfer {Id} -> {Status} (ok={Ok}).", id, p.Status, ok);
                    return (ok, new TransferResult(ok));
                });

            var status = (await db.Payments.FindAsync(id))?.Status.ToString();

            return outcome.Success
                ? Results.Ok(new { outcome.Data!.TransferOk, Status = status })
                : Results.Conflict(new { outcome.Data!.TransferOk, Status = status });
        }
        catch (EntityNotFoundException) { return Results.NotFound(new ErrorResponse($"Payment {id} not found.")); }
    }

    // Шаг RETURN_FUNDS
    private static async Task<IResult> ReturnAsync(
        Guid id, HttpRequest http, IdempotencyService idem,
        FundsService funds, ILogger<Program> logger)
    {
        try
        {
            await idem.ExecuteAsync<AckResult>(
                IdemKey(http, id, "RETURN"), id, "RETURN",
                async p => { await funds.ReturnAsync(p); return (true, new AckResult(true)); }
            );

            logger.LogInformation("Return {Id} -> FUNDS_RETURNED.", id);

            return Results.Ok(new { Success = true, Status = PaymentStatus.FUNDS_RETURNED.ToString() });
        }
        catch (EntityNotFoundException) { return Results.NotFound(new ErrorResponse($"Payment {id} not found.")); }
    }

    // Шаг COMPLETE_PAYMENT
    private static async Task<IResult> CompleteAsync(
        Guid id, HttpRequest http, IdempotencyService idem,
        FundsService funds, ILogger<Program> logger)
    {
        try
        {
            await idem.ExecuteAsync<AckResult>(
                IdemKey(http, id, "COMPLETE"), id, "COMPLETE",
                async p => { await funds.CompleteAsync(p); return (true, new AckResult(true)); }
            );

            logger.LogInformation("Complete {Id} -> PAYMENT_PROCESS_COMPLETED.", id);

            return Results.Ok(new { Success = true, Status = PaymentStatus.PAYMENT_PROCESS_COMPLETED.ToString() });
        }
        catch (EntityNotFoundException) { return Results.NotFound(new ErrorResponse($"Payment {id} not found.")); }
    }

    // Шаг FINISH_PAYMENT_PROCESS (cancellation path).
    private static async Task<IResult> FinishAsync(
        Guid id, HttpRequest http, IdempotencyService idem, ILogger<Program> logger)
    {
        try
        {
            await idem.ExecuteAsync<AckResult>(
                IdemKey(http, id, "FINISH"), id, "FINISH",
                p =>
                {
                    p.Transition(PaymentStatus.PAYMENT_PROCESS_CANCELED);
                    return Task.FromResult((true, new AckResult(true)));
                });

            logger.LogInformation("Finish {Id} -> PAYMENT_PROCESS_CANCELED.", id);

            return Results.Ok(new { Success = true, Status = PaymentStatus.PAYMENT_PROCESS_CANCELED.ToString() });
        }
        catch (EntityNotFoundException) { return Results.NotFound(new ErrorResponse($"Payment {id} not found.")); }
    }

    // Получить платёж
    private static async Task<IResult> GetAsync(Guid id, Data.PaymentDbContext db)
    {
        var p = await db.Payments.FindAsync(id);
        
        return p is null
            ? Results.NotFound(new ErrorResponse($"Payment {id} not found."))
            : Results.Ok(ToResponse(p));
    }
}

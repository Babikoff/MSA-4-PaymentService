using Microsoft.AspNetCore.Mvc;
using NotificationService.Models;

namespace NotificationService.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications");

        group.MapPost("/user", NotifyUserAsync);
        group.MapPost("/security", NotifySecrurityAsync);
    }

    private static async Task<IResult> NotifyUserAsync(
        [FromBody] NotifyUserRequest? r,
        ILogger<Program> logger)
    {
        logger.LogInformation($"{DateTime.Now} PaymentId: {r?.PaymentId} RecipientId: {r?.RecipientId} EventType: {r?.EventType} " +
            $" PaymentStatus: {r?.PaymentStatus} Message: {r?.Message} Channel: {r?.Channel}"
        );
        await Task.Delay(0);
        return Results.Ok("User notified");
    }

    private static async Task<IResult> NotifySecrurityAsync(
        [FromBody] NotifySecurityRequest? r,
        ILogger<Program> logger)
    {
        logger.LogInformation($"{DateTime.Now} PaymentId: {r?.PaymentId} RecipientId: {r?.RecipientId} EventType: {r?.EventType} " +
            $" PaymentStatus: {r?.PaymentStatus} Message: {r?.Message} RiskScore: {r?.RiskScore}" +
            $" RuleHits: [{string.Join(';', r?.RuleHits ?? [])}]"
        );
        await Task.Delay(0);
        return Results.Ok("Security notified");
    }
}

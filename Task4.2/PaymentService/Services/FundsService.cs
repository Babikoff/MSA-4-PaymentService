using Microsoft.Extensions.Options;
using PaymentService.Domain;

namespace PaymentService.Services;

/// <summary>Платёжный сервис.</summary>
/// <remarks>
/// На фазе прототипа проекта просто эмулирует случайным образом возможное поведение системы. 
/// </remarks>
public class FundsService
{
    private readonly PaymentOptions _options;

    public FundsService(IOptions<PaymentOptions> options) => _options = options.Value;

    private int RandomDelayMs()
        => Random.Shared.Next(_options.MinDelayMs, _options.MaxDelayMs + 1);

    private Task DelayAsync() => Task.Delay(RandomDelayMs());

    /// <summary>Обработчик шага HOLD_FUNDS (зарезервировать средства).</summary>
    /// <remarks>Добавлена эмуляция случайных ошибок.</remarks>
    public async Task<bool> HoldAsync(Payment payment)
    {
        await DelayAsync();

        if (Random.Shared.NextDouble() < _options.HoldFailureProbability)
        {
            payment.Transition(PaymentStatus.FAILED);
            return false;
        }
        payment.Transition(PaymentStatus.FUNDS_HELD);
        return true;
    }

    /// <summary>Обработчик шага RELEASE_FUNDS (снять резервирование).</summary>
    public async Task ReleaseAsync(Payment payment)
    {
        await DelayAsync();
        payment.Transition(PaymentStatus.FUNDS_RELEASED);
    }

    /// <summary>Обработчик шага TRANSFER_FUNDS (перевести деньги на счёт получателя).</summary>
    /// <remarks>Добавлена эмуляция случайных ошибок.</remarks>
    public async Task<bool> TransferAsync(Payment payment)
    {
        await DelayAsync();
        if (Random.Shared.NextDouble() < _options.TransferFailureProbability)
        {
            return false;
        }
        payment.Transition(PaymentStatus.FUNDS_TRANSFERRED);
        return true;
    }

    /// <summary>Обработчик шага RETURN_FUNDS (вернуть уже переведённые средства плательщику).</summary>
    public async Task ReturnAsync(Payment payment)
    {
        await DelayAsync();
        payment.Transition(PaymentStatus.FUNDS_RETURNED);
    }

    /// <summary>Обработчик шага COMPLETE_PAYMENT (финальный успешный шаг).</summary>
    public async Task CompleteAsync(Payment payment)
    {
        await DelayAsync();
        payment.Transition(PaymentStatus.PAYMENT_PROCESS_COMPLETED);
    }
}
namespace PaymentService.Services;

/// <summary>Значения для эмуляции реального поведения системы (для тестов и режима прототипа).</summary>
public class PaymentOptions
{
    public double HoldFailureProbability { get; set; } = 0.05;
    public double TransferFailureProbability { get; set; } = 0.05;
    public int MinDelayMs { get; set; } = 50;
    public int MaxDelayMs { get; set; } = 500;
}
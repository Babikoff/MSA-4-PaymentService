namespace PaymentService.Domain;

/// <summary>
/// Специальная запись об идемпотентности, благодаря которой 
/// при повторной попытке выполнения задания оркестратора возвращается тот же результат, 
/// а не применяется дважды.
/// </summary>
public class OperationRecord
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public Guid PaymentId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset FirstAttemptAt { get; set; }
}
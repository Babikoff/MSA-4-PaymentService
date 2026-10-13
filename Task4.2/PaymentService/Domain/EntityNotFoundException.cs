namespace PaymentService.Domain;

/// <summary>Ошибка: платёж не найден (-> 404).</summary>
public sealed class EntityNotFoundException : Exception
{
    public Guid Id { get; }

    public EntityNotFoundException(Guid id)
        : base($"Payment {id} not found.")
    {
        Id = id;
    }
}
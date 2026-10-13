using System.Collections.Concurrent;
using Orchestrator.Models;

namespace Orchestrator.Services;

public interface ISagaStateService
{
    Task<SagaState> CreateAsync(Guid paymentId, string initialJob, CancellationToken ct = default);
    Task<SagaState?> GetAsync(Guid paymentId, CancellationToken ct = default);
    Task SaveAsync(SagaState state, CancellationToken ct = default);
    Task DeleteAsync(Guid paymentId, CancellationToken ct = default);
}

public interface ISagaRepository
{
    Task<SagaState?> GetAsync(Guid paymentId, CancellationToken ct = default);
    Task<SagaState> AddAsync(SagaState state, CancellationToken ct = default);
    Task SaveAsync(SagaState state, CancellationToken ct = default);
    Task DeleteAsync(Guid paymentId, CancellationToken ct = default);
}

public class InMemorySagaRepository : ISagaRepository
{
    //TODO: использовать постоянное хранилище данных или кэш с устареванием
    private readonly ConcurrentDictionary<Guid, SagaState> _store = new();

    public Task<SagaState?> GetAsync(Guid paymentId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(paymentId, out var s) ? (SagaState?)s : null);

    public Task<SagaState> AddAsync(SagaState state, CancellationToken ct = default)
    {
        _store[state.PaymentId] = state;
        return Task.FromResult(state);
    }

    public Task SaveAsync(SagaState state, CancellationToken ct = default)
    {
        _store[state.PaymentId] = state;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid paymentId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryRemove(paymentId, out _));
}

public class SagaStateService : ISagaStateService
{
    private readonly ISagaRepository _repo;
    private readonly ILogger<SagaStateService> _logger;

    public SagaStateService(ISagaRepository repo, ILogger<SagaStateService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<SagaState> CreateAsync(Guid paymentId, string initialJob, CancellationToken ct = default)
    {
        var state = new SagaState
        {
            PaymentId = paymentId,
            Status = SagaStatus.PENDING,
            CurrentJob = initialJob,
            StartTime = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return _repo.AddAsync(state, ct);
    }

    public Task<SagaState?> GetAsync(Guid paymentId, CancellationToken ct = default) =>
        _repo.GetAsync(paymentId, ct);

    public Task SaveAsync(SagaState state, CancellationToken ct = default) =>
        _repo.SaveAsync(state, ct);

    public Task DeleteAsync(Guid paymentId, CancellationToken ct = default) =>
        _repo.DeleteAsync(paymentId, ct);
}

namespace Orchestrator.Services;

public interface IZeebeWorkerService
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    bool IsRunning { get; }
}

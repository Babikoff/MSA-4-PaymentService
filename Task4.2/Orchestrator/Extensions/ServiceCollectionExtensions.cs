using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orchestrator.Services;

namespace Orchestrator.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorServices(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ZeebeOptions>(config.GetSection("Zeebe"));
        services.Configure<ServiceUrls>(config.GetSection("Services"));
        services.Configure<SagaOptions>(config.GetSection("Saga"));
        return services;
    }
}

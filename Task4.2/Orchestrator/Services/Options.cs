namespace Orchestrator.Services;

public class ZeebeOptions
{
    public string GatewayAddress { get; set; } = "localhost:26500";
    public bool UsePlaintext { get; set; } = true;
}

public class ServiceUrls
{
    public string Payment { get; set; } = "http://localhost:3000";
    public string FraudCheck { get; set; } = "http://localhost:3001";
    public string Notification { get; set; } = "http://localhost:3002";
}

public class SagaOptions
{
    public string StateStore { get; set; } = "InMemory";
    public int MaxJobsToActivate { get; set; } = 10;
    public int JobLocalTimeoutMs { get; set; } = 30000;
}

namespace HttpProxy.ResiliencePolicy;

public class ResiliencePolicyConfig
{
    public bool EnableRetry { get; set; }
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public bool ExponentialBackoff { get; set; } = true;

    public bool EnableCircuitBreaker { get; set; }
    public int FailureThreshold { get; set; } = 5;
    public int DurationOfBreakSeconds { get; set; } = 30;
    public bool EnableTimeout { get; set; }
    public int TimeoutSeconds { get; set; }
}

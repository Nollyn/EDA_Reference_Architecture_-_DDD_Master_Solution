using Polly;

namespace BuildingBlocks.Resilience;

/// <summary>
/// Configuration options for standard enterprise platform HTTP resilience pipelines.
/// </summary>
public sealed class PlatformHttpResilienceOptions
{
    /// <summary>
    /// Configuration section key name in appsettings.
    /// </summary>
    public const string SectionName = "Resilience:Http";

    /// <summary>
    /// Maximum number of retry attempts for transient HTTP failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retry attempts.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Backoff type for retries (defaults to Exponential).
    /// </summary>
    public DelayBackoffType BackoffType { get; set; } = DelayBackoffType.Exponential;

    /// <summary>
    /// Whether to enable randomized exponential jitter on retries to avoid thundering herd.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Circuit breaker failure ratio threshold (0.0 to 1.0).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum throughput required in sampling duration before evaluating circuit breaker.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>
    /// Sampling duration for circuit breaker statistics.
    /// </summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Duration circuit breaker remains open before transitioning to half-open.
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Timeout per individual HTTP request attempt.
    /// </summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Total cumulative timeout for the entire HTTP request operation including retries.
    /// </summary>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

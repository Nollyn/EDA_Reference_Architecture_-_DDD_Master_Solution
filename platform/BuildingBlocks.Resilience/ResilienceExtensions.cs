using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace BuildingBlocks.Resilience;

/// <summary>
/// Reusable extension methods for standard enterprise platform HTTP resilience pipelines using Polly v8.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Configures standardized platform HTTP resilience strategies (Retry with exponential jitter, Circuit Breaker, Attempt Timeout, and Total Request Timeout).
    /// </summary>
    /// <param name="builder">Target HTTP client builder.</param>
    /// <param name="configuration">Optional application configuration.</param>
    /// <param name="policySection">Optional configuration section name override.</param>
    /// <param name="configure">Optional customization callback for <see cref="HttpStandardResilienceOptions"/>.</param>
    /// <returns>The HTTP client builder for chaining.</returns>
    public static IHttpStandardResiliencePipelineBuilder AddPlatformHttpResilience(
        this IHttpClientBuilder builder,
        IConfiguration? configuration = null,
        string? policySection = null,
        Action<HttpStandardResilienceOptions>? configure = null)
    {
        var platformOptions = new PlatformHttpResilienceOptions();
        var sectionName = !string.IsNullOrWhiteSpace(policySection)
            ? policySection
            : PlatformHttpResilienceOptions.SectionName;

        configuration?.GetSection(sectionName).Bind(platformOptions);

        return builder.AddStandardResilienceHandler(options =>
        {
            // Exponential Retry with Jitter
            options.Retry.MaxRetryAttempts = platformOptions.MaxRetryAttempts;
            options.Retry.BackoffType = platformOptions.BackoffType;
            options.Retry.Delay = platformOptions.RetryBaseDelay;
            options.Retry.UseJitter = platformOptions.UseJitter;

            // Circuit Breaker
            options.CircuitBreaker.FailureRatio = platformOptions.CircuitBreakerFailureRatio;
            options.CircuitBreaker.MinimumThroughput = platformOptions.CircuitBreakerMinimumThroughput;
            options.CircuitBreaker.SamplingDuration = platformOptions.CircuitBreakerSamplingDuration;
            options.CircuitBreaker.BreakDuration = platformOptions.CircuitBreakerBreakDuration;

            // Timeouts
            options.AttemptTimeout.Timeout = platformOptions.AttemptTimeout;
            options.TotalRequestTimeout.Timeout = platformOptions.TotalRequestTimeout;

            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Configures standardized platform HTTP resilience using explicit <see cref="PlatformHttpResilienceOptions"/>.
    /// </summary>
    /// <param name="builder">Target HTTP client builder.</param>
    /// <param name="configureOptions">Options configuration action.</param>
    /// <returns>The HTTP standard resilience pipeline builder for chaining.</returns>
    public static IHttpStandardResiliencePipelineBuilder AddPlatformHttpResilience(
        this IHttpClientBuilder builder,
        Action<PlatformHttpResilienceOptions> configureOptions)
    {
        var platformOptions = new PlatformHttpResilienceOptions();
        configureOptions(platformOptions);

        return builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = platformOptions.MaxRetryAttempts;
            options.Retry.BackoffType = platformOptions.BackoffType;
            options.Retry.Delay = platformOptions.RetryBaseDelay;
            options.Retry.UseJitter = platformOptions.UseJitter;

            options.CircuitBreaker.FailureRatio = platformOptions.CircuitBreakerFailureRatio;
            options.CircuitBreaker.MinimumThroughput = platformOptions.CircuitBreakerMinimumThroughput;
            options.CircuitBreaker.SamplingDuration = platformOptions.CircuitBreakerSamplingDuration;
            options.CircuitBreaker.BreakDuration = platformOptions.CircuitBreakerBreakDuration;

            options.AttemptTimeout.Timeout = platformOptions.AttemptTimeout;
            options.TotalRequestTimeout.Timeout = platformOptions.TotalRequestTimeout;
        });
    }
}

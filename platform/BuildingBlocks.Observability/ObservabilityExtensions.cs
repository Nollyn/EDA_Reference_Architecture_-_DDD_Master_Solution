using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability;

/// <summary>
/// Extension methods for configuring enterprise platform observability.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Registers OpenTelemetry distributed tracing, metrics, W3C propagators, and correlation enrichment for the service.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="defaultServiceName">Default service name if not specified in configuration.</param>
    /// <param name="configure">Optional customization callback for platform observability options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPlatformObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string defaultServiceName = "PlatformService",
        Action<PlatformObservabilityOptions>? configure = null)
    {
        var options = new PlatformObservabilityOptions
        {
            ServiceName = configuration["OpenTelemetry:ServiceName"] ?? defaultServiceName,
            ServiceVersion = configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0",
            OtlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"]
                ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                ?? "http://localhost:4317"
        };

        configure?.Invoke(options);

        services.AddSingleton(options);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion))
            .WithTracing(tracing =>
            {
                tracing.AddSource(options.ServiceName);

                foreach (var source in options.AdditionalSources)
                {
                    tracing.AddSource(source);
                }

                if (options.EnableAspNetCoreInstrumentation)
                {
                    tracing.AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                    });
                }

                if (options.EnableHttpClientInstrumentation)
                {
                    tracing.AddHttpClientInstrumentation(o =>
                    {
                        o.RecordException = true;
                    });
                }

                if (options.EnableNpgsqlInstrumentation)
                {
                    tracing.AddNpgsql();
                }

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(options.ServiceName);

                foreach (var meter in options.AdditionalMeters)
                {
                    metrics.AddMeter(meter);
                }

                if (options.EnableAspNetCoreInstrumentation)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                if (options.EnableHttpClientInstrumentation)
                {
                    metrics.AddHttpClientInstrumentation();
                }

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            });

        return services;
    }

    /// <summary>
    /// Adds the platform correlation identifier middleware into the ASP.NET Core request processing pipeline.
    /// </summary>
    /// <param name="app">Target application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UsePlatformCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}

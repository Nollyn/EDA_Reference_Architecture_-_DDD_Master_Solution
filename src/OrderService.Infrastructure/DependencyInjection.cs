using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.Application.Ports;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.ExternalServices;
using OrderService.Infrastructure.Messaging.Inbox;
using OrderService.Infrastructure.Messaging.Outbox;
using OrderService.Infrastructure.Messaging.RabbitMq;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Persistence.Queries;
using OrderService.Infrastructure.Persistence.Repositories;
using Polly;

namespace OrderService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql")
            ?? "Host=localhost;Port=5432;Database=orders_db;Username=postgres;Password=postgres";

        // EF Core 9 / PostgreSQL
        services.AddDbContext<OrderDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            });
        });

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderQueries>(_ => new OrderQueries(connectionString));
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        // Outbox Publisher
        services.AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        // RabbitMQ
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IRabbitMqConnectionManager, RabbitMqConnectionManager>();
        services.AddScoped<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();
        services.AddSingleton<IRabbitMqInboxConsumer, RabbitMqInboxConsumer>();

        // Resilient HTTP Client (Polly v8) for Fraud Check
        services.AddHttpClient<IFraudCheckService, FraudCheckService>(client =>
        {
            var fraudServiceUrl = configuration["ExternalServices:FraudCheckUrl"];
            if (!string.IsNullOrWhiteSpace(fraudServiceUrl))
            {
                client.BaseAddress = new Uri(fraudServiceUrl);
            }
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 5;
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        });

        // OpenTelemetry
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: Telemetry.ServiceName,
                serviceVersion: Telemetry.ServiceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(Telemetry.ServiceName)
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(o =>
                    {
                        o.RecordException = true;
                    })
                    .AddNpgsql()
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(otlpEndpoint);
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(Telemetry.ServiceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(otlpEndpoint);
                    });
            });

        return services;
    }
}

using BuildingBlocks.Observability;
using BuildingBlocks.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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

        // Resilient HTTP Client using Platform Resilience BuildingBlock
        services.AddHttpClient<IFraudCheckService, FraudCheckService>(client =>
        {
            var fraudServiceUrl = configuration["ExternalServices:FraudCheckUrl"];
            if (!string.IsNullOrWhiteSpace(fraudServiceUrl))
            {
                client.BaseAddress = new Uri(fraudServiceUrl);
            }
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddPlatformHttpResilience(configuration, "Resilience:FraudCheck");

        // Platform OpenTelemetry Observability
        services.AddPlatformObservability(
            configuration,
            defaultServiceName: Telemetry.ServiceName,
            configure: options =>
            {
                options.AdditionalSources.Add(Telemetry.ServiceName);
                options.AdditionalMeters.Add(Telemetry.ServiceName);
            });

        return services;
    }
}

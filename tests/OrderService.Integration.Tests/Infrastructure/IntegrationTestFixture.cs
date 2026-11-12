using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace OrderService.Integration.Tests.Infrastructure;

public sealed class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlContainer PostgreSqlContainer { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("orders_test_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public RabbitMqContainer RabbitMqContainer { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public async Task InitializeAsync()
    {
        await PostgreSqlContainer.StartAsync();
        await RabbitMqContainer.StartAsync();

        // Ensure database schema is initialized
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        await dbInitializer.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await PostgreSqlContainer.DisposeAsync();
        await RabbitMqContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = PostgreSqlContainer.GetConnectionString(),
                ["RabbitMQ:HostName"] = RabbitMqContainer.Hostname,
                ["RabbitMQ:Port"] = RabbitMqContainer.GetMappedPublicPort(5672).ToString(),
                ["RabbitMQ:UserName"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/",
                ["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4317"
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.UseEnvironment("Testing");
    }
}

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}

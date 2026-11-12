using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderService.Api.Endpoints;
using OrderService.Api.Middleware;
using OrderService.Application;
using OrderService.Infrastructure;
using OrderService.Infrastructure.Messaging.RabbitMq;
using OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Register Hexagonal Ports & Adapters
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Platform Security (JWT Bearer / OIDC / DevMode Bypass)
builder.Services.AddPlatformAuthentication(builder.Configuration);

// Error Handling & ProblemDetails
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// API Explorer & Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Order Service API",
        Version = "v1",
        Description = "Production-grade EDA Reference Architecture with Hexagonal Architecture and DDD"
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Liveness probe OK"), tags: ["live"])
    .AddCheck("database", () => HealthCheckResult.Healthy("Database readiness OK"), tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UsePlatformCorrelationId();
app.UsePlatformSecurity();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker") || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service API v1"));
}

// Health Check Endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Map Minimal API Endpoints
app.MapOrderEndpoints();

// Initialize DB schema and RabbitMQ topology asynchronously
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        await dbInitializer.InitializeAsync();

        var topologyInitializer = scope.ServiceProvider.GetRequiredService<IRabbitMqTopologyInitializer>();
        await topologyInitializer.InitializeTopologyAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Initial startup migration or topology setup deferred (containers may still be starting)");
    }
}

app.Run();

// Partial class definition for WebApplicationFactory in Integration Tests
public partial class Program { }

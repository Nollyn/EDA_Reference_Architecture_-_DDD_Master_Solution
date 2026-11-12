using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using OrderService.Application.Ports;
using OrderService.Domain.ValueObjects;

namespace OrderService.Infrastructure.ExternalServices;

public sealed record FraudCheckApiRequest(Guid OrderId, Guid CustomerId, decimal Amount);
public sealed record FraudCheckApiResponse(bool IsAllowed, string? Reason);

public sealed class FraudCheckService : IFraudCheckService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FraudCheckService> _logger;

    public FraudCheckService(HttpClient httpClient, ILogger<FraudCheckService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FraudCheckResult> CheckFraudAsync(OrderId orderId, CustomerId customerId, decimal amount, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing external fraud verification for Order {OrderId}, Customer {CustomerId}, Amount {Amount:F2}...", orderId, customerId, amount);

        // Simulated check: amount over 100,000 flagged as potential fraud or test rejection
        if (amount > 100000m)
        {
            _logger.LogWarning("Fraud check rejected: amount exceeds threshold.");
            return FraudCheckResult.Rejected("Order total exceeds allowed single-transaction limit for fraud prevention.");
        }

        try
        {
            // If base address is configured, perform real HTTP call through Polly v8 resilience pipeline
            if (_httpClient.BaseAddress is not null)
            {
                var request = new FraudCheckApiRequest(orderId.Value, customerId.Value, amount);
                var response = await _httpClient.PostAsJsonAsync("/api/v1/fraud-check", request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<FraudCheckApiResponse>(cancellationToken: cancellationToken);
                    if (result is not null && !result.IsAllowed)
                    {
                        return FraudCheckResult.Rejected(result.Reason ?? "Rejected by external fraud provider.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External fraud service call failed or timed out. Falling back to default risk assessment.");
        }

        return FraudCheckResult.Approved();
    }
}

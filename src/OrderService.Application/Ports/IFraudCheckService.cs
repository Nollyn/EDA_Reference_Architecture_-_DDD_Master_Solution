using OrderService.Domain.ValueObjects;

namespace OrderService.Application.Ports;

public readonly record struct FraudCheckResult(bool IsAllowed, string? Reason = null)
{
    public static FraudCheckResult Approved() => new(true);
    public static FraudCheckResult Rejected(string reason) => new(false, reason);
}

public interface IFraudCheckService
{
    Task<FraudCheckResult> CheckFraudAsync(OrderId orderId, CustomerId customerId, decimal amount, CancellationToken cancellationToken = default);
}

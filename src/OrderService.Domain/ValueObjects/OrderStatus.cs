namespace OrderService.Domain.ValueObjects;

public enum OrderStatus
{
    Draft = 1,
    Submitted = 2,
    Confirmed = 3,
    Paid = 4,
    Shipped = 5,
    Cancelled = 6
}

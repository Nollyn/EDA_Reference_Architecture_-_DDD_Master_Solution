using OrderService.Domain.Exceptions;

namespace OrderService.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new OrderDomainException("Money amount cannot be negative.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new OrderDomainException("Currency code must be a 3-letter ISO code.");

        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money From(decimal amount, string currency) => new(amount, currency);

    public static Money Zero(string currency = "USD") => new(0m, currency);

    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Currency != other.Currency)
            throw new OrderDomainException($"Cannot add money with different currencies: {Currency} and {other.Currency}.");

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Currency != other.Currency)
            throw new OrderDomainException($"Cannot subtract money with different currencies: {Currency} and {other.Currency}.");

        if (Amount < other.Amount)
            throw new OrderDomainException("Money subtraction would result in a negative amount.");

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(int quantity)
    {
        if (quantity < 0)
            throw new OrderDomainException("Quantity cannot be negative when multiplying money.");

        return new Money(Amount * quantity, Currency);
    }

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
    public static Money operator *(Money left, int right) => left.Multiply(right);

    public override string ToString() => $"{Amount:F2} {Currency}";
}

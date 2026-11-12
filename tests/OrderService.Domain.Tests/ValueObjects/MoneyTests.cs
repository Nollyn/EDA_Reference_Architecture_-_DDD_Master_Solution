using FluentAssertions;
using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;
using Xunit;

namespace OrderService.Domain.Tests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_WithValidAmountAndCurrency_Should_CreateInstance()
    {
        // Act
        var money = new Money(100.505m, "usd");

        // Assert
        money.Amount.Should().Be(100.51m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_WithNegativeAmount_Should_ThrowOrderDomainException()
    {
        // Act
        var act = () => new Money(-1.00m, "USD");

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*cannot be negative*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Constructor_WithInvalidCurrency_Should_ThrowOrderDomainException(string currency)
    {
        // Act
        var act = () => new Money(10.00m, currency);

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*3-letter ISO code*");
    }

    [Fact]
    public void Add_WithSameCurrency_Should_ReturnSummedMoney()
    {
        // Arrange
        var m1 = Money.From(10.25m, "USD");
        var m2 = Money.From(20.50m, "USD");

        // Act
        var result = m1.Add(m2);

        // Assert
        result.Amount.Should().Be(30.75m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_WithDifferentCurrency_Should_ThrowOrderDomainException()
    {
        // Arrange
        var usd = Money.From(10.00m, "USD");
        var eur = Money.From(10.00m, "EUR");

        // Act
        var act = () => usd.Add(eur);

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtract_WhenResultIsNegative_Should_ThrowOrderDomainException()
    {
        // Arrange
        var small = Money.From(10.00m, "USD");
        var large = Money.From(20.00m, "USD");

        // Act
        var act = () => small.Subtract(large);

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*negative amount*");
    }

    [Fact]
    public void Multiply_Should_ReturnMultipliedAmount()
    {
        // Arrange
        var money = Money.From(15.25m, "USD");

        // Act
        var result = money.Multiply(4);

        // Assert
        result.Amount.Should().Be(61.00m);
        result.Currency.Should().Be("USD");
    }
}

namespace OrderService.Domain.ValueObjects;

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty.", nameof(value));
        return new(value);
    }
    public override string ToString() => Value.ToString();
}

public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public static CustomerId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CustomerId cannot be empty.", nameof(value));
        return new(value);
    }
    public override string ToString() => Value.ToString();
}

public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
    public static ProductId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(value));
        return new(value);
    }
    public override string ToString() => Value.ToString();
}

public readonly record struct OrderItemId(Guid Value)
{
    public static OrderItemId New() => new(Guid.NewGuid());
    public static OrderItemId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("OrderItemId cannot be empty.", nameof(value));
        return new(value);
    }
    public override string ToString() => Value.ToString();
}

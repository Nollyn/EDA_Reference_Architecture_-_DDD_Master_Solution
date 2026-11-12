using OrderService.Domain.Common;

namespace OrderService.Domain.Exceptions;

public class OrderDomainException : DomainException
{
    public OrderDomainException(string message) : base(message)
    {
    }

    public OrderDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

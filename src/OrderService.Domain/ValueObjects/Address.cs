using OrderService.Domain.Exceptions;

namespace OrderService.Domain.ValueObjects;

public sealed record Address
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(string street, string city, string state, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new OrderDomainException("Street cannot be empty.");
        if (string.IsNullOrWhiteSpace(city))
            throw new OrderDomainException("City cannot be empty.");
        if (string.IsNullOrWhiteSpace(state))
            throw new OrderDomainException("State cannot be empty.");
        if (string.IsNullOrWhiteSpace(postalCode))
            throw new OrderDomainException("PostalCode cannot be empty.");
        if (string.IsNullOrWhiteSpace(country))
            throw new OrderDomainException("Country cannot be empty.");

        Street = street.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
    }

    public override string ToString() => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}

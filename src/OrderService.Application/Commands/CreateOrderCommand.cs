using OrderService.Application.Common;
using OrderService.Application.DTOs;
using OrderService.Application.Events;
using OrderService.Application.Exceptions;
using OrderService.Application.Ports;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Repositories;
using OrderService.Domain.ValueObjects;

namespace OrderService.Application.Commands;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    AddressDto ShippingAddress,
    string Currency,
    IReadOnlyList<CreateOrderItemDto> Items) : IRequest<Guid>;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly IFraudCheckService _fraudCheckService;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IIntegrationEventPublisher eventPublisher,
        IFraudCheckService fraudCheckService)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _fraudCheckService = fraudCheckService;
    }

    public async Task<Guid> HandleAsync(CreateOrderCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        var customerId = CustomerId.From(request.CustomerId);
        var shippingAddress = new Address(
            request.ShippingAddress.Street,
            request.ShippingAddress.City,
            request.ShippingAddress.State,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.Country);

        var order = Order.Create(customerId, shippingAddress, request.Currency);

        foreach (var itemDto in request.Items)
        {
            var productId = ProductId.From(itemDto.ProductId);
            var unitPrice = Money.From(itemDto.UnitPrice, request.Currency);
            order.AddItem(productId, itemDto.ProductName, unitPrice, itemDto.Quantity);
        }

        order.Submit();

        // Resilient fraud verification check before persisting
        var fraudResult = await _fraudCheckService.CheckFraudAsync(order.Id, customerId, order.TotalAmount.Amount, cancellationToken);
        if (!fraudResult.IsAllowed)
        {
            throw new ValidationException("FraudCheck", fraudResult.Reason ?? "Order failed automated fraud check.");
        }

        await _orderRepository.AddAsync(order, cancellationToken);

        var integrationEvent = new OrderCreatedIntegrationEvent(
            order.Id.Value,
            order.CustomerId.Value,
            order.TotalAmount.Amount,
            order.Currency,
            order.Items.Select(i => new OrderCreatedItemPayload(i.ProductId.Value, i.ProductName, i.Quantity, i.UnitPrice.Amount)).ToList());

        await _eventPublisher.PublishAsync(integrationEvent, cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return order.Id.Value;
    }

    private static void ValidateRequest(CreateOrderCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.CustomerId == Guid.Empty)
            errors[nameof(request.CustomerId)] = ["CustomerId cannot be empty."];

        if (request.ShippingAddress is null)
            errors[nameof(request.ShippingAddress)] = ["Shipping address is required."];

        if (request.Items is null || request.Items.Count == 0)
            errors[nameof(request.Items)] = ["Order must contain at least one item."];
        else
        {
            for (var i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                if (item.ProductId == Guid.Empty)
                    errors[$"Items[{i}].ProductId"] = ["ProductId cannot be empty."];
                if (string.IsNullOrWhiteSpace(item.ProductName))
                    errors[$"Items[{i}].ProductName"] = ["ProductName cannot be empty."];
                if (item.UnitPrice < 0)
                    errors[$"Items[{i}].UnitPrice"] = ["UnitPrice cannot be negative."];
                if (item.Quantity <= 0)
                    errors[$"Items[{i}].Quantity"] = ["Quantity must be greater than zero."];
            }
        }

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }
}

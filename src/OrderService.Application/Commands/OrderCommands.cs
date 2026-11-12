using OrderService.Application.Common;
using OrderService.Application.Events;
using OrderService.Application.Exceptions;
using OrderService.Application.Ports;
using OrderService.Domain.Repositories;
using OrderService.Domain.ValueObjects;

namespace OrderService.Application.Commands;

public sealed record ConfirmOrderCommand(Guid OrderId) : IRequest<bool>;

public sealed class ConfirmOrderCommandHandler : IRequestHandler<ConfirmOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _eventPublisher;

    public ConfirmOrderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IIntegrationEventPublisher eventPublisher)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> HandleAsync(ConfirmOrderCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await _orderRepository.GetByIdAsync(OrderId.From(request.OrderId), cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        order.Confirm();
        await _orderRepository.UpdateAsync(order, cancellationToken);

        var integrationEvent = new OrderConfirmedIntegrationEvent(
            order.Id.Value,
            order.CustomerId.Value,
            order.TotalAmount.Amount,
            order.Currency);

        await _eventPublisher.PublishAsync(integrationEvent, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<bool>;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _eventPublisher;

    public CancelOrderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IIntegrationEventPublisher eventPublisher)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> HandleAsync(CancelOrderCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException(nameof(request.Reason), "Cancellation reason is required.");

        var order = await _orderRepository.GetByIdAsync(OrderId.From(request.OrderId), cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        order.Cancel(request.Reason);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        var integrationEvent = new OrderCancelledIntegrationEvent(
            order.Id.Value,
            order.CustomerId.Value,
            order.CancellationReason ?? request.Reason);

        await _eventPublisher.PublishAsync(integrationEvent, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}

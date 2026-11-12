using Microsoft.Extensions.Logging;
using OrderService.Application.Common;
using OrderService.Application.Events;
using OrderService.Application.Ports;
using OrderService.Domain.Repositories;
using OrderService.Domain.ValueObjects;

namespace OrderService.Application.Commands;

public sealed record ProcessInventoryReservationFailedCommand(Guid OrderId, string Reason) : IRequest<bool>;

public sealed class ProcessInventoryReservationFailedCommandHandler : IRequestHandler<ProcessInventoryReservationFailedCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ILogger<ProcessInventoryReservationFailedCommandHandler> _logger;

    public ProcessInventoryReservationFailedCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IIntegrationEventPublisher eventPublisher,
        ILogger<ProcessInventoryReservationFailedCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ProcessInventoryReservationFailedCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogWarning("Executing compensating transaction for Order {OrderId}: Inventory reservation failed. Reason: {Reason}", request.OrderId, request.Reason);

        var order = await _orderRepository.GetByIdAsync(OrderId.From(request.OrderId), cancellationToken);
        if (order is null)
        {
            _logger.LogError("Cannot compensate Order {OrderId}: Order not found.", request.OrderId);
            return false;
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            _logger.LogInformation("Order {OrderId} is already cancelled.", request.OrderId);
            return true;
        }

        order.Cancel($"Compensation: Inventory reservation failed - {request.Reason}");
        await _orderRepository.UpdateAsync(order, cancellationToken);

        var integrationEvent = new OrderCancelledIntegrationEvent(
            order.Id.Value,
            order.CustomerId.Value,
            order.CancellationReason!);

        await _eventPublisher.PublishAsync(integrationEvent, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation("Successfully compensated Order {OrderId}, status is now Cancelled.", request.OrderId);
        return true;
    }
}

public sealed record ProcessInventoryReservedCommand(Guid OrderId, Guid ReservationId) : IRequest<bool>;

public sealed class ProcessInventoryReservedCommandHandler : IRequestHandler<ProcessInventoryReservedCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ILogger<ProcessInventoryReservedCommandHandler> _logger;

    public ProcessInventoryReservedCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IIntegrationEventPublisher eventPublisher,
        ILogger<ProcessInventoryReservedCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ProcessInventoryReservedCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Processing inventory reserved confirmation for Order {OrderId} (Reservation: {ReservationId})", request.OrderId, request.ReservationId);

        var order = await _orderRepository.GetByIdAsync(OrderId.From(request.OrderId), cancellationToken);
        if (order is null)
        {
            _logger.LogError("Cannot confirm Order {OrderId}: Order not found.", request.OrderId);
            return false;
        }

        if (order.Status == OrderStatus.Confirmed || order.Status == OrderStatus.Paid)
        {
            return true;
        }

        order.Confirm();
        await _orderRepository.UpdateAsync(order, cancellationToken);

        var integrationEvent = new OrderConfirmedIntegrationEvent(
            order.Id.Value,
            order.CustomerId.Value,
            order.TotalAmount.Amount,
            order.Currency);

        await _eventPublisher.PublishAsync(integrationEvent, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation("Successfully confirmed Order {OrderId} following inventory reservation.", request.OrderId);
        return true;
    }
}

using SnapCQ.Abstractions;
using Example.SnapCQ.Models;
using Example.SnapCQ.Services;
using Example.SnapCQ.Events;

namespace Example.SnapCQ.Commands;

public record CancelOrderCommand(Guid OrderId) : IRequest<bool>;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IDispatcher _dispatcher;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IDispatcher dispatcher)
    {
        _orderRepository = orderRepository;
        _dispatcher = dispatcher;
    }

    public async ValueTask<bool> HandleAsync(CancelOrderCommand request, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId);

        if (order == null)
            return false;

        if (order.Status == OrderStatus.Completed)
            return false;

        order.Status = OrderStatus.Cancelled;
        await _orderRepository.UpdateAsync(order);

        // Publish event
        await _dispatcher.PublishAsync(new OrderCancelledEvent(order.OrderId, order.CustomerId), ct);

        return true;
    }
}

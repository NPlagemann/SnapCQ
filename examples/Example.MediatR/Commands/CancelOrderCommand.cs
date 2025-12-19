using Example.MediatR.Models;
using Example.MediatR.Services;
using Example.MediatR.Events;
using MediatR;

namespace Example.MediatR.Commands;

public record CancelOrderCommand(Guid OrderId) : IRequest<bool>;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublisher _publisher;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IPublisher publisher)
    {
        _orderRepository = orderRepository;
        _publisher = publisher;
    }

    public async Task<bool> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId);

        if (order == null)
            return false;

        if (order.Status == OrderStatus.Completed)
            return false;

        order.Status = OrderStatus.Cancelled;
        await _orderRepository.UpdateAsync(order);

        // Publish event
        await _publisher.Publish(new OrderCancelledEvent(order.OrderId, order.CustomerId), cancellationToken);

        return true;
    }
}

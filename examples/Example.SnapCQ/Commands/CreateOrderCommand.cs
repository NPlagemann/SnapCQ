using SnapCQ.Abstractions;
using Example.SnapCQ.Models;
using Example.SnapCQ.Services;
using Example.SnapCQ.Events;

namespace Example.SnapCQ.Commands;

public record CreateOrderCommand(
    string CustomerId,
    List<OrderItemDto> Items
) : IRequest<Guid>;

public record OrderItemDto(
    string ProductId,
    int Quantity,
    decimal UnitPrice
);

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IDispatcher _dispatcher;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IDispatcher dispatcher)
    {
        _orderRepository = orderRepository;
        _dispatcher = dispatcher;
    }

    public async ValueTask<Guid> HandleAsync(CreateOrderCommand request, CancellationToken ct = default)
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        await _orderRepository.AddAsync(order);

        // Publish event
        await _dispatcher.PublishAsync(new OrderCreatedEvent(order.OrderId, order.CustomerId, order.TotalAmount), ct);

        return order.OrderId;
    }
}

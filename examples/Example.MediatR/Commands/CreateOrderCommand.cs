using Example.MediatR.Models;
using Example.MediatR.Services;
using Example.MediatR.Events;
using MediatR;

namespace Example.MediatR.Commands;

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
    private readonly IPublisher _publisher;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IPublisher publisher)
    {
        _orderRepository = orderRepository;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
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
        await _publisher.Publish(new OrderCreatedEvent(order.OrderId, order.CustomerId, order.TotalAmount), cancellationToken);

        return order.OrderId;
    }
}

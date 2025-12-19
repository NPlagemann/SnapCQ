using Example.Wolverine.Models;
using Example.Wolverine.Services;
using Example.Wolverine.Events;
using Wolverine;

namespace Example.Wolverine.Commands;

public record CreateOrderCommand(
    string CustomerId,
    List<OrderItemDto> Items
);

public record OrderItemDto(
    string ProductId,
    int Quantity,
    decimal UnitPrice
);

public static class CreateOrderCommandHandler
{
    public static async Task<Guid> Handle(CreateOrderCommand command, IOrderRepository orderRepository, IMessageBus bus)
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerId = command.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Items = command.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        await orderRepository.AddAsync(order);

        // Publish event
        await bus.PublishAsync(new OrderCreatedEvent(order.OrderId, order.CustomerId, order.TotalAmount));

        return order.OrderId;
    }
}

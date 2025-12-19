using Example.Wolverine.Models;
using Example.Wolverine.Services;
using Example.Wolverine.Events;
using Wolverine;

namespace Example.Wolverine.Commands;

public record CancelOrderCommand(Guid OrderId);

public static class CancelOrderCommandHandler
{
    public static async Task<bool> Handle(CancelOrderCommand command, IOrderRepository orderRepository, IMessageBus bus)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId);

        if (order == null)
            return false;

        if (order.Status == OrderStatus.Completed)
            return false;

        order.Status = OrderStatus.Cancelled;
        await orderRepository.UpdateAsync(order);

        // Publish event
        await bus.PublishAsync(new OrderCancelledEvent(order.OrderId, order.CustomerId));

        return true;
    }
}

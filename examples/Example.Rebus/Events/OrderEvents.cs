using Rebus.Handlers;

namespace Example.Rebus.Events;

public record OrderCreatedEvent(Guid OrderId, string CustomerId, decimal TotalAmount);

public record OrderCancelledEvent(Guid OrderId, string CustomerId);

public class OrderCreatedEventHandler : IHandleMessages<OrderCreatedEvent>
{
    public Task Handle(OrderCreatedEvent message)
    {
        Console.WriteLine($"[Rebus Event] Order created: {message.OrderId} for customer {message.CustomerId}, Total: {message.TotalAmount:C}");
        return Task.CompletedTask;
    }
}

public class OrderCancelledEventHandler : IHandleMessages<OrderCancelledEvent>
{
    public Task Handle(OrderCancelledEvent message)
    {
        Console.WriteLine($"[Rebus Event] Order cancelled: {message.OrderId} for customer {message.CustomerId}");
        return Task.CompletedTask;
    }
}

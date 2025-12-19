using SnapCQ.Abstractions;

namespace Example.SnapCQ.Events;

public record OrderCreatedEvent(Guid OrderId, string CustomerId, decimal TotalAmount) : INotification;

public record OrderCancelledEvent(Guid OrderId, string CustomerId) : INotification;

public class OrderCreatedEventHandler : INotificationHandler<OrderCreatedEvent>
{
    public ValueTask HandleAsync(OrderCreatedEvent notification, CancellationToken ct = default)
    {
        Console.WriteLine($"[SnapCQ Event] Order created: {notification.OrderId} for customer {notification.CustomerId}, Total: {notification.TotalAmount:C}");
        return ValueTask.CompletedTask;
    }
}

public class OrderCancelledEventHandler : INotificationHandler<OrderCancelledEvent>
{
    public ValueTask HandleAsync(OrderCancelledEvent notification, CancellationToken ct = default)
    {
        Console.WriteLine($"[SnapCQ Event] Order cancelled: {notification.OrderId} for customer {notification.CustomerId}");
        return ValueTask.CompletedTask;
    }
}

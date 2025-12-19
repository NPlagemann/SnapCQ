using MediatR;

namespace Example.MediatR.Events;

public record OrderCreatedEvent(Guid OrderId, string CustomerId, decimal TotalAmount) : INotification;

public record OrderCancelledEvent(Guid OrderId, string CustomerId) : INotification;

public class OrderCreatedEventHandler : INotificationHandler<OrderCreatedEvent>
{
    public Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[MediatR Event] Order created: {notification.OrderId} for customer {notification.CustomerId}, Total: {notification.TotalAmount:C}");
        return Task.CompletedTask;
    }
}

public class OrderCancelledEventHandler : INotificationHandler<OrderCancelledEvent>
{
    public Task Handle(OrderCancelledEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[MediatR Event] Order cancelled: {notification.OrderId} for customer {notification.CustomerId}");
        return Task.CompletedTask;
    }
}

namespace Example.Wolverine.Events;

public record OrderCreatedEvent(Guid OrderId, string CustomerId, decimal TotalAmount);

public record OrderCancelledEvent(Guid OrderId, string CustomerId);

public static class OrderCreatedEventHandler
{
    public static void Handle(OrderCreatedEvent @event)
    {
        Console.WriteLine($"[Wolverine Event] Order created: {@event.OrderId} for customer {@event.CustomerId}, Total: {@event.TotalAmount:C}");
    }
}

public static class OrderCancelledEventHandler
{
    public static void Handle(OrderCancelledEvent @event)
    {
        Console.WriteLine($"[Wolverine Event] Order cancelled: {@event.OrderId} for customer {@event.CustomerId}");
    }
}

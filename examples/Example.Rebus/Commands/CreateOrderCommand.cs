using Example.Rebus.Models;
using Example.Rebus.Services;
using Example.Rebus.Events;
using Rebus.Bus;
using Rebus.Handlers;
using System.Collections.Concurrent;

namespace Example.Rebus.Commands;

public record CreateOrderCommand(
    string CorrelationId,
    string CustomerId,
    List<OrderItemDto> Items
);

public record OrderItemDto(
    string ProductId,
    int Quantity,
    decimal UnitPrice
);

public class CreateOrderCommandHandler : IHandleMessages<CreateOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IBus _bus;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _responseStorage;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IBus bus, ConcurrentDictionary<string, TaskCompletionSource<object>> responseStorage)
    {
        _orderRepository = orderRepository;
        _bus = bus;
        _responseStorage = responseStorage;
    }

    public async Task Handle(CreateOrderCommand message)
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerId = message.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Items = message.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        await _orderRepository.AddAsync(order);

        // Publish event
        await _bus.Publish(new OrderCreatedEvent(order.OrderId, order.CustomerId, order.TotalAmount));

        // Set response
        if (_responseStorage.TryGetValue(message.CorrelationId, out var tcs))
        {
            tcs.SetResult(order.OrderId);
        }
    }
}

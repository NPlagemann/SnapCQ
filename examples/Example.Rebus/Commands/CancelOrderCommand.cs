using Example.Rebus.Models;
using Example.Rebus.Services;
using Example.Rebus.Events;
using Rebus.Bus;
using Rebus.Handlers;
using System.Collections.Concurrent;

namespace Example.Rebus.Commands;

public record CancelOrderCommand(string CorrelationId, Guid OrderId);

public class CancelOrderCommandHandler : IHandleMessages<CancelOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IBus _bus;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _responseStorage;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IBus bus, ConcurrentDictionary<string, TaskCompletionSource<object>> responseStorage)
    {
        _orderRepository = orderRepository;
        _bus = bus;
        _responseStorage = responseStorage;
    }

    public async Task Handle(CancelOrderCommand message)
    {
        var order = await _orderRepository.GetByIdAsync(message.OrderId);

        if (order == null)
        {
            if (_responseStorage.TryGetValue(message.CorrelationId, out var tcs1))
            {
                tcs1.SetResult(false);
            }
            return;
        }

        if (order.Status == OrderStatus.Completed)
        {
            if (_responseStorage.TryGetValue(message.CorrelationId, out var tcs2))
            {
                tcs2.SetResult(false);
            }
            return;
        }

        order.Status = OrderStatus.Cancelled;
        await _orderRepository.UpdateAsync(order);

        // Publish event
        await _bus.Publish(new OrderCancelledEvent(order.OrderId, order.CustomerId));

        if (_responseStorage.TryGetValue(message.CorrelationId, out var tcs3))
        {
            tcs3.SetResult(true);
        }
    }
}

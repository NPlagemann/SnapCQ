using Example.Rebus.Models;
using Example.Rebus.Services;
using Rebus.Handlers;
using System.Collections.Concurrent;

namespace Example.Rebus.Queries;

public record GetOrderByIdQuery(string CorrelationId, Guid OrderId);

public class GetOrderByIdQueryHandler : IHandleMessages<GetOrderByIdQuery>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _responseStorage;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository, ConcurrentDictionary<string, TaskCompletionSource<object>> responseStorage)
    {
        _orderRepository = orderRepository;
        _responseStorage = responseStorage;
    }

    public async Task Handle(GetOrderByIdQuery message)
    {
        var order = await _orderRepository.GetByIdAsync(message.OrderId);

        if (_responseStorage.TryGetValue(message.CorrelationId, out var tcs))
        {
            tcs.SetResult(order!);
        }
    }
}

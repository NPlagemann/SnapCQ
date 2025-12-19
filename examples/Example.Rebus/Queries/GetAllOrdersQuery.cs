using Example.Rebus.Models;
using Example.Rebus.Services;
using Rebus.Handlers;
using System.Collections.Concurrent;

namespace Example.Rebus.Queries;

public record GetAllOrdersQuery(string CorrelationId);

public class GetAllOrdersQueryHandler : IHandleMessages<GetAllOrdersQuery>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _responseStorage;

    public GetAllOrdersQueryHandler(IOrderRepository orderRepository, ConcurrentDictionary<string, TaskCompletionSource<object>> responseStorage)
    {
        _orderRepository = orderRepository;
        _responseStorage = responseStorage;
    }

    public async Task Handle(GetAllOrdersQuery message)
    {
        var orders = await _orderRepository.GetAllAsync();

        if (_responseStorage.TryGetValue(message.CorrelationId, out var tcs))
        {
            tcs.SetResult(orders);
        }
    }
}

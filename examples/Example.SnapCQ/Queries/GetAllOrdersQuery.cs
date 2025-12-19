using SnapCQ.Abstractions;
using Example.SnapCQ.Models;
using Example.SnapCQ.Services;

namespace Example.SnapCQ.Queries;

public record GetAllOrdersQuery : IRequest<List<Order>>;

public class GetAllOrdersQueryHandler : IRequestHandler<GetAllOrdersQuery, List<Order>>
{
    private readonly IOrderRepository _orderRepository;

    public GetAllOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async ValueTask<List<Order>> HandleAsync(GetAllOrdersQuery request, CancellationToken ct = default)
    {
        return await _orderRepository.GetAllAsync();
    }
}

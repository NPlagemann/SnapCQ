using SnapCQ.Abstractions;
using Example.SnapCQ.Models;
using Example.SnapCQ.Services;

namespace Example.SnapCQ.Queries;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<Order?>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Order?>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async ValueTask<Order?> HandleAsync(GetOrderByIdQuery request, CancellationToken ct = default)
    {
        return await _orderRepository.GetByIdAsync(request.OrderId);
    }
}

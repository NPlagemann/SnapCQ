using Example.MediatR.Models;
using Example.MediatR.Services;
using MediatR;

namespace Example.MediatR.Queries;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<Order?>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Order?>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Order?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        return await _orderRepository.GetByIdAsync(request.OrderId);
    }
}

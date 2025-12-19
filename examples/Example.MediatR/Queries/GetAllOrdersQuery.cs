using Example.MediatR.Models;
using Example.MediatR.Services;
using MediatR;

namespace Example.MediatR.Queries;

public record GetAllOrdersQuery : IRequest<List<Order>>;

public class GetAllOrdersQueryHandler : IRequestHandler<GetAllOrdersQuery, List<Order>>
{
    private readonly IOrderRepository _orderRepository;

    public GetAllOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<List<Order>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
    {
        return await _orderRepository.GetAllAsync();
    }
}

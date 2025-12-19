using Example.Wolverine.Models;
using Example.Wolverine.Services;

namespace Example.Wolverine.Queries;

public record GetOrderByIdQuery(Guid OrderId);

public static class GetOrderByIdQueryHandler
{
    public static async Task<Order?> Handle(GetOrderByIdQuery query, IOrderRepository orderRepository)
    {
        return await orderRepository.GetByIdAsync(query.OrderId);
    }
}

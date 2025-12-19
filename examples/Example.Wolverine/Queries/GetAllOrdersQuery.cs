using Example.Wolverine.Models;
using Example.Wolverine.Services;

namespace Example.Wolverine.Queries;

public record GetAllOrdersQuery();

public static class GetAllOrdersQueryHandler
{
    public static async Task<List<Order>> Handle(GetAllOrdersQuery query, IOrderRepository orderRepository)
    {
        return await orderRepository.GetAllAsync();
    }
}

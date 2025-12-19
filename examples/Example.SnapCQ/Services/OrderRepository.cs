using Example.SnapCQ.Models;

namespace Example.SnapCQ.Services;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId);
    Task<List<Order>> GetAllAsync();
    Task AddAsync(Order order);
    Task UpdateAsync(Order order);
}

public class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = new();

    public Task<Order?> GetByIdAsync(Guid orderId)
    {
        var order = _orders.FirstOrDefault(o => o.OrderId == orderId);
        return Task.FromResult(order);
    }

    public Task<List<Order>> GetAllAsync()
    {
        return Task.FromResult(_orders.ToList());
    }

    public Task AddAsync(Order order)
    {
        _orders.Add(order);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Order order)
    {
        var existing = _orders.FirstOrDefault(o => o.OrderId == order.OrderId);
        if (existing != null)
        {
            _orders.Remove(existing);
            _orders.Add(order);
        }
        return Task.CompletedTask;
    }
}

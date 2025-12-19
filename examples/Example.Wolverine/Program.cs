using Example.Wolverine.Commands;
using Example.Wolverine.Models;
using Example.Wolverine.Queries;
using Example.Wolverine.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

Console.WriteLine("=== Wolverine Example - Order Processing System ===\n");

// Setup Dependency Injection
var host = Host.CreateDefaultBuilder(args)
    .UseWolverine(opts =>
    {
        // Wolverine will automatically discover handlers from the assembly
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    })
    .ConfigureServices((context, services) =>
    {
        // Register services
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
    })
    .Build();

await host.StartAsync();

var bus = host.Services.GetRequiredService<IMessageBus>();

try
{
    Console.WriteLine("1. Creating a new order...");
    var createOrderCommand = new CreateOrderCommand(
        CustomerId: "CUST-001",
        Items: new List<OrderItemDto>
        {
            new("PROD-001", 2, 29.99m),
            new("PROD-002", 1, 49.99m)
        }
    );

    var orderId = await bus.InvokeAsync<Guid>(createOrderCommand);
    Console.WriteLine($"Order created with ID: {orderId}\n");

    Console.WriteLine("2. Retrieving order by ID...");
    var getOrderQuery = new GetOrderByIdQuery(orderId);
    var order = await bus.InvokeAsync<Order?>(getOrderQuery);

    if (order != null)
    {
        Console.WriteLine($"Order found: {order.OrderId}");
        Console.WriteLine($"Customer: {order.CustomerId}");
        Console.WriteLine($"Status: {order.Status}");
        Console.WriteLine($"Total Amount: {order.TotalAmount:C}");
        Console.WriteLine($"Items: {order.Items.Count}");
        foreach (var item in order.Items)
        {
            Console.WriteLine($"  - {item.ProductId}: {item.Quantity} x {item.UnitPrice:C} = {item.TotalPrice:C}");
        }
        Console.WriteLine();
    }

    Console.WriteLine("3. Creating another order...");
    var secondOrderId = await bus.InvokeAsync<Guid>(new CreateOrderCommand(
        "CUST-002",
        new List<OrderItemDto> { new("PROD-003", 5, 9.99m) }
    ));
    Console.WriteLine($"Second order created with ID: {secondOrderId}\n");

    Console.WriteLine("4. Retrieving all orders...");
    var allOrdersQuery = new GetAllOrdersQuery();
    var allOrders = await bus.InvokeAsync<List<Order>>(allOrdersQuery);
    Console.WriteLine($"Total orders: {allOrders.Count}\n");

    Console.WriteLine("5. Cancelling the first order...");
    var cancelCommand = new CancelOrderCommand(orderId);
    var cancelled = await bus.InvokeAsync<bool>(cancelCommand);
    Console.WriteLine($"Order cancellation result: {cancelled}\n");

    Console.WriteLine("6. Checking order status after cancellation...");
    order = await bus.InvokeAsync<Order?>(new GetOrderByIdQuery(orderId));
    if (order != null)
    {
        Console.WriteLine($"Order Status: {order.Status}\n");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("\n=== Wolverine Example Complete ===");

await host.StopAsync();

using Example.MediatR.Commands;
using Example.MediatR.Models;
using Example.MediatR.Queries;
using Example.MediatR.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("=== MediatR Example - Order Processing System ===\n");

// Setup Dependency Injection
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Register services
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
    })
    .Build();

var mediator = host.Services.GetRequiredService<IMediator>();

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

    var orderId = await mediator.Send(createOrderCommand);
    Console.WriteLine($"Order created with ID: {orderId}\n");

    Console.WriteLine("2. Retrieving order by ID...");
    var getOrderQuery = new GetOrderByIdQuery(orderId);
    var order = await mediator.Send(getOrderQuery);

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
    var secondOrderId = await mediator.Send(new CreateOrderCommand(
        "CUST-002",
        new List<OrderItemDto> { new("PROD-003", 5, 9.99m) }
    ));
    Console.WriteLine($"Second order created with ID: {secondOrderId}\n");

    Console.WriteLine("4. Retrieving all orders...");
    var allOrdersQuery = new GetAllOrdersQuery();
    var allOrders = await mediator.Send(allOrdersQuery);
    Console.WriteLine($"Total orders: {allOrders.Count}\n");

    Console.WriteLine("5. Cancelling the first order...");
    var cancelCommand = new CancelOrderCommand(orderId);
    var cancelled = await mediator.Send(cancelCommand);
    Console.WriteLine($"Order cancellation result: {cancelled}\n");

    Console.WriteLine("6. Checking order status after cancellation...");
    order = await mediator.Send(new GetOrderByIdQuery(orderId));
    if (order != null)
    {
        Console.WriteLine($"Order Status: {order.Status}\n");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("\n=== MediatR Example Complete ===");

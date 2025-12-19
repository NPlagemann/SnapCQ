using Example.Rebus.Commands;
using Example.Rebus.Models;
using Example.Rebus.Queries;
using Example.Rebus.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;
using System.Collections.Concurrent;

Console.WriteLine("=== Rebus Example - Order Processing System ===\n");

// Shared storage for synchronous request/response
var responseStorage = new ConcurrentDictionary<string, TaskCompletionSource<object>>();

// Setup Dependency Injection
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register services
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddSingleton(responseStorage);

        // Configure Rebus
        var network = new InMemNetwork();

        services.AddRebus(configure => configure
            .Transport(t => t.UseInMemoryTransport(network, "example-queue"))
            .Routing(r => r.TypeBased()
                .MapAssemblyOf<CreateOrderCommand>("example-queue")));

        services.AutoRegisterHandlersFromAssemblyOf<Program>();
    })
    .Build();

await host.StartAsync();

var bus = host.Services.GetRequiredService<Rebus.Bus.IBus>();

// Helper method to send and await response synchronously
async Task<T> SendAndAwait<T>(object message)
{
    var correlationId = Guid.NewGuid().ToString();
    var tcs = new TaskCompletionSource<object>();
    responseStorage[correlationId] = tcs;

    // Add correlation ID to message
    object messageWithCorrelation = message switch
    {
        CreateOrderCommand cmd => new CreateOrderCommand(correlationId, cmd.CustomerId, cmd.Items),
        CancelOrderCommand cmd => new CancelOrderCommand(correlationId, cmd.OrderId),
        GetOrderByIdQuery query => new GetOrderByIdQuery(correlationId, query.OrderId),
        GetAllOrdersQuery query => new GetAllOrdersQuery(correlationId),
        _ => throw new InvalidOperationException($"Unsupported message type: {message.GetType()}")
    };

    await bus.Send(messageWithCorrelation);
    var result = await tcs.Task;
    responseStorage.TryRemove(correlationId, out _);
    return (T)result;
}

try
{
    Console.WriteLine("1. Creating a new order...");
    var createOrderCommand = new CreateOrderCommand(
        CorrelationId: string.Empty,
        CustomerId: "CUST-001",
        Items: new List<OrderItemDto>
        {
            new("PROD-001", 2, 29.99m),
            new("PROD-002", 1, 49.99m)
        }
    );

    var orderId = await SendAndAwait<Guid>(createOrderCommand);
    Console.WriteLine($"Order created with ID: {orderId}\n");

    Console.WriteLine("2. Retrieving order by ID...");
    var getOrderQuery = new GetOrderByIdQuery(string.Empty, orderId);
    var order = await SendAndAwait<Order?>(getOrderQuery);

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
    var secondOrderId = await SendAndAwait<Guid>(new CreateOrderCommand(
        string.Empty,
        "CUST-002",
        new List<OrderItemDto> { new("PROD-003", 5, 9.99m) }
    ));
    Console.WriteLine($"Second order created with ID: {secondOrderId}\n");

    Console.WriteLine("4. Retrieving all orders...");
    var allOrdersQuery = new GetAllOrdersQuery(string.Empty);
    var allOrders = await SendAndAwait<List<Order>>(allOrdersQuery);
    Console.WriteLine($"Total orders: {allOrders.Count}\n");

    Console.WriteLine("5. Cancelling the first order...");
    var cancelCommand = new CancelOrderCommand(string.Empty, orderId);
    var cancelled = await SendAndAwait<bool>(cancelCommand);
    Console.WriteLine($"Order cancellation result: {cancelled}\n");

    Console.WriteLine("6. Checking order status after cancellation...");
    order = await SendAndAwait<Order?>(new GetOrderByIdQuery(string.Empty, orderId));
    if (order != null)
    {
        Console.WriteLine($"Order Status: {order.Status}\n");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("\n=== Rebus Example Complete ===");

await host.StopAsync();

using SnapCQ;
using SnapCQ.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace SnapCQ.UnitTests;

public class NotificationTests
{
    public class TestNotification : INotification
    {
        public string Message { get; init; } = string.Empty;
    }

    public class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public TestNotification? ReceivedNotification { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public List<string> Events { get; } = new();

        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            ReceivedNotification = notification;
            ReceivedToken = ct;
            Events.Add("Handled");
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_WithValidNotification_CallsAllHandlers()
    {
        var services = new ServiceCollection();
        var handler1 = new TestNotificationHandler();
        var handler2 = new TestNotificationHandler();
        services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
        services.AddSingleton<INotificationHandler<TestNotification>>(handler2);

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var notification = new TestNotification { Message = "test" };
        await dispatcher.PublishAsync(notification);

        handler1.ReceivedNotification.Should().Be(notification);
        handler2.ReceivedNotification.Should().Be(notification);
    }

    [Fact]
    public async Task PublishAsync_WithNoHandlers_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var notification = new TestNotification();

        var act = async () => await dispatcher.PublishAsync(notification);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithNullNotification_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var act = async () => await dispatcher.PublishAsync<TestNotification>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_PassesTokenToAllHandlers()
    {
        var services = new ServiceCollection();
        var handler1 = new TestNotificationHandler();
        var handler2 = new TestNotificationHandler();
        services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
        services.AddSingleton<INotificationHandler<TestNotification>>(handler2);

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var notification = new TestNotification();
        var cts = new CancellationTokenSource();

        await dispatcher.PublishAsync(notification, cts.Token);

        handler1.ReceivedToken.Should().Be(cts.Token);
        handler2.ReceivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task PublishAsync_WithMultipleHandlers_ExecutesSequentially()
    {
        var executionOrder = new List<int>();
        var services = new ServiceCollection();

        services.AddSingleton<INotificationHandler<TestNotification>>(
            new OrderTrackingHandler(1, executionOrder));
        services.AddSingleton<INotificationHandler<TestNotification>>(
            new OrderTrackingHandler(2, executionOrder));
        services.AddSingleton<INotificationHandler<TestNotification>>(
            new OrderTrackingHandler(3, executionOrder));

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var notification = new TestNotification();
        await dispatcher.PublishAsync(notification);

        executionOrder.Should().Equal(1, 2, 3);
    }

    public class OrderTrackingHandler : INotificationHandler<TestNotification>
    {
        private readonly int _number;
        private readonly List<int> _executionOrder;

        public OrderTrackingHandler(int number, List<int> executionOrder)
        {
            _number = number;
            _executionOrder = executionOrder;
        }

        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            _executionOrder.Add(_number);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_WhenHandlerThrows_PropagatesException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<TestNotification>>(
            new ThrowingHandler());

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var notification = new TestNotification();

        var act = async () => await dispatcher.PublishAsync(notification);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler failed");
    }

    public class ThrowingHandler : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Handler failed");
        }
    }
}

using SnapCQ;
using SnapCQ.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace SnapCQ.UnitTests;

public class DispatcherTests
{
    public class TestQuery : IRequest<string>;

    public class TestCommand : IRequest;

    public class TestQueryHandler : IRequestHandler<TestQuery, string>
    {
        public string Response { get; init; } = "test response";
        public TestQuery? ReceivedRequest { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public ValueTask<string> HandleAsync(TestQuery request, CancellationToken ct = default)
        {
            ReceivedRequest = request;
            ReceivedToken = ct;
            return ValueTask.FromResult(Response);
        }
    }

    public class TestCommandHandler : IRequestHandler<TestCommand, Unit>
    {
        public TestCommand? ReceivedRequest { get; private set; }

        public ValueTask<Unit> HandleAsync(TestCommand request, CancellationToken ct = default)
        {
            ReceivedRequest = request;
            return ValueTask.FromResult(Unit.Value);
        }
    }

    [Fact]
    public async Task SendAsync_WithValidRequest_CallsHandler()
    {
        var services = new ServiceCollection();
        var handler = new TestQueryHandler();
        services.AddSingleton<IRequestHandler<TestQuery, string>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var request = new TestQuery();
        var result = await dispatcher.SendAsync(request);

        result.Should().Be("test response");
        handler.ReceivedRequest.Should().Be(request);
    }

    [Fact]
    public async Task SendAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var act = async () => await dispatcher.SendAsync<string>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_WithNoRegisteredHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var request = new TestQuery();

        var act = async () => await dispatcher.SendAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No handler registered for TestQuery");
    }

    [Fact]
    public async Task SendAsync_WithCancellationToken_PassesTokenToHandler()
    {
        var services = new ServiceCollection();
        var handler = new TestQueryHandler();
        services.AddSingleton<IRequestHandler<TestQuery, string>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var request = new TestQuery();
        var cts = new CancellationTokenSource();

        await dispatcher.SendAsync(request, cts.Token);

        handler.ReceivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task SendAsync_WithPipelineBehavior_ExecutesBehaviorBeforeHandler()
    {
        var services = new ServiceCollection();
        var handler = new TestQueryHandler();
        services.AddSingleton<IRequestHandler<TestQuery, string>>(handler);

        var behavior = new TestPipelineBehavior<TestQuery, string>();
        services.AddSingleton<IPipelineBehavior<TestQuery, string>>(behavior);

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var request = new TestQuery();
        var result = await dispatcher.SendAsync(request);

        result.Should().Be("[Modified] test response");
        behavior.WasCalled.Should().BeTrue();
    }

    public class TestPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public bool WasCalled { get; private set; }

        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct = default)
        {
            WasCalled = true;
            var response = await next();

            if (response is string str)
            {
                return (TResponse)(object)$"[Modified] {str}";
            }

            return response;
        }
    }

    [Fact]
    public async Task SendAsync_WithMultiplePipelineBehaviors_ExecutesInCorrectOrder()
    {
        var executionOrder = new List<string>();
        var services = new ServiceCollection();

        services.AddSingleton<IRequestHandler<TestQuery, string>>(
            new OrderTrackingHandler(executionOrder));

        services.AddSingleton<IPipelineBehavior<TestQuery, string>>(
            new OrderTrackingBehavior<TestQuery, string>(1, executionOrder));
        services.AddSingleton<IPipelineBehavior<TestQuery, string>>(
            new OrderTrackingBehavior<TestQuery, string>(2, executionOrder));

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var request = new TestQuery();
        await dispatcher.SendAsync(request);

        executionOrder.Should().Equal(
            "Behavior1-Before",
            "Behavior2-Before",
            "Handler",
            "Behavior2-After",
            "Behavior1-After");
    }

    public class OrderTrackingHandler : IRequestHandler<TestQuery, string>
    {
        private readonly List<string> _executionOrder;

        public OrderTrackingHandler(List<string> executionOrder)
        {
            _executionOrder = executionOrder;
        }

        public ValueTask<string> HandleAsync(TestQuery request, CancellationToken ct = default)
        {
            _executionOrder.Add("Handler");
            return ValueTask.FromResult("response");
        }
    }

    public class OrderTrackingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly int _number;
        private readonly List<string> _executionOrder;

        public OrderTrackingBehavior(int number, List<string> executionOrder)
        {
            _number = number;
            _executionOrder = executionOrder;
        }

        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct = default)
        {
            _executionOrder.Add($"Behavior{_number}-Before");
            var response = await next();
            _executionOrder.Add($"Behavior{_number}-After");
            return response;
        }
    }

    [Fact]
    public async Task SendAsync_WithCommandRequest_ReturnsUnit()
    {
        var services = new ServiceCollection();
        var handler = new TestCommandHandler();
        services.AddSingleton<IRequestHandler<TestCommand, Unit>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new Dispatcher(serviceProvider, new DispatcherOptions());

        var command = new TestCommand();
        var result = await dispatcher.SendAsync<Unit>(command);

        result.Should().Be(Unit.Value);
        handler.ReceivedRequest.Should().Be(command);
    }
}

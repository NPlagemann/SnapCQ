using System.Reflection;
using SnapCQ.Abstractions;
using SnapCQ.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace SnapCQ.UnitTests;

public class PipelineBehaviorIntegrationTests
{
    public class TestRequest : IRequest<string>
    {
        public string Value { get; init; } = string.Empty;
    }

    public class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public ValueTask<string> HandleAsync(TestRequest request, CancellationToken ct = default)
        {
            return ValueTask.FromResult($"Handled: {request.Value}");
        }
    }

    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public List<string> Logs { get; } = new();

        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct = default)
        {
            Logs.Add("Before");
            var response = await next();
            Logs.Add("After");
            return response;
        }
    }

    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public bool ShouldValidate { get; init; } = true;

        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct = default)
        {
            if (!ShouldValidate)
            {
                throw new InvalidOperationException("Validation failed");
            }

            return await next();
        }
    }

    public class TransformBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct = default)
        {
            var response = await next();

            if (response is string str)
            {
                return (TResponse)(object)$"[Transformed] {str}";
            }

            return response;
        }
    }

    [Fact]
    public async Task Pipeline_WithSingleBehavior_ExecutesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var loggingBehavior = new LoggingBehavior<TestRequest, string>();
        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(loggingBehavior);

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new TestRequest { Value = "test" });

        result.Should().Be("Handled: test");
        loggingBehavior.Logs.Should().Equal("Before", "After");
    }

    [Fact]
    public async Task Pipeline_WithMultipleBehaviors_ExecutesInCorrectOrder()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var loggingBehavior = new LoggingBehavior<TestRequest, string>();
        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(loggingBehavior);
        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(
            new TransformBehavior<TestRequest, string>());

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new TestRequest { Value = "test" });

        result.Should().Be("[Transformed] Handled: test");
        loggingBehavior.Logs.Should().Equal("Before", "After");
    }

    [Fact]
    public async Task Pipeline_WhenBehaviorThrows_PropagatesException()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(
            new ValidationBehavior<TestRequest, string> { ShouldValidate = false });

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

        var act = async () => await dispatcher.SendAsync(new TestRequest { Value = "test" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Validation failed");
    }

    [Fact]
    public async Task Pipeline_WithThreeBehaviors_ExecutesInCorrectOrder()
    {
        var executionOrder = new List<string>();
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(
            new OrderTrackingBehavior<TestRequest, string>(1, executionOrder));
        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(
            new OrderTrackingBehavior<TestRequest, string>(2, executionOrder));
        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(
            new OrderTrackingBehavior<TestRequest, string>(3, executionOrder));

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new TestRequest { Value = "test" });

        executionOrder.Should().Equal(
            "Behavior1-Before",
            "Behavior2-Before",
            "Behavior3-Before",
            "Behavior3-After",
            "Behavior2-After",
            "Behavior1-After");
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
    public async Task Pipeline_BehaviorCanShortCircuit()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(
            new ShortCircuitBehavior<TestRequest, string>());

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new TestRequest { Value = "test" });

        result.Should().Be("Short-circuited");
    }

    public class ShortCircuitBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct = default)
        {
            return ValueTask.FromResult((TResponse)(object)"Short-circuited");
        }
    }

    [Fact]
    public async Task Pipeline_BehaviorReceivesCancellationToken()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var tokenCapture = new CancellationTokenCapture<TestRequest, string>();
        services.AddSingleton<IPipelineBehavior<TestRequest, string>>(tokenCapture);

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();

        var cts = new CancellationTokenSource();
        await dispatcher.SendAsync(new TestRequest { Value = "test" }, cts.Token);

        tokenCapture.CapturedToken.Should().Be(cts.Token);
    }

    public class CancellationTokenCapture<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public CancellationToken CapturedToken { get; private set; }

        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct = default)
        {
            CapturedToken = ct;
            return await next();
        }
    }
}

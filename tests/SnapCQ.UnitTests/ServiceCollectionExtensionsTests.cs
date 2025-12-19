using System.Reflection;
using SnapCQ;
using SnapCQ.Abstractions;
using SnapCQ.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace SnapCQ.UnitTests;

public class ServiceCollectionExtensionsTests
{
    public class TestQuery : IRequest<string>;

    public class TestCommand : IRequest;

    public class TestNotification : INotification;

    public class TestQueryHandler : IRequestHandler<TestQuery, string>
    {
        public ValueTask<string> HandleAsync(TestQuery request, CancellationToken ct = default)
        {
            return ValueTask.FromResult("response");
        }
    }

    public class TestCommandHandler : IRequestHandler<TestCommand, Unit>
    {
        public ValueTask<Unit> HandleAsync(TestCommand request, CancellationToken ct = default)
        {
            return ValueTask.FromResult(Unit.Value);
        }
    }

    public class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    public class TestPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct = default)
        {
            return await next();
        }
    }

    [Fact]
    public void AddDispatcher_RegistersDispatcherAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher1 = serviceProvider.GetService<IDispatcher>();
        var dispatcher2 = serviceProvider.GetService<IDispatcher>();

        dispatcher1.Should().NotBeNull();
        dispatcher2.Should().NotBeNull();
        dispatcher1.Should().BeSameAs(dispatcher2);
    }

    [Fact]
    public void AddDispatcher_WithNullServices_ThrowsArgumentNullException()
    {
        ServiceCollection services = null!;

        var act = () => services.AddDispatcher(Assembly.GetExecutingAssembly());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddDispatcher_WithNullAssemblies_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddDispatcher(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddDispatcher_AutoRegistersRequestHandlers()
    {
        var services = new ServiceCollection();

        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider.GetService<IRequestHandler<TestQuery, string>>();

        handler.Should().NotBeNull();
        handler.Should().BeOfType<TestQueryHandler>();
    }

    [Fact]
    public void AddDispatcher_AutoRegistersCommandHandlers()
    {
        var services = new ServiceCollection();

        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider.GetService<IRequestHandler<TestCommand, Unit>>();

        handler.Should().NotBeNull();
        handler.Should().BeOfType<TestCommandHandler>();
    }

    [Fact]
    public void AddDispatcher_AutoRegistersNotificationHandlers()
    {
        var services = new ServiceCollection();

        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var serviceProvider = services.BuildServiceProvider();
        var handlers = serviceProvider.GetServices<INotificationHandler<TestNotification>>();

        handlers.Should().NotBeEmpty();
        handlers.Should().ContainSingle(h => h.GetType() == typeof(TestNotificationHandler));
    }

    [Fact]
    public void AddDispatcher_WithMultipleAssemblies_RegistersHandlersFromAllAssemblies()
    {
        var services = new ServiceCollection();

        services.AddDispatcher(Assembly.GetExecutingAssembly(), typeof(Dispatcher).Assembly);

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetService<IDispatcher>();
        var handler = serviceProvider.GetService<IRequestHandler<TestQuery, string>>();

        dispatcher.Should().NotBeNull();
        handler.Should().NotBeNull();
    }

    [Fact]
    public void AddDispatcher_RegistersHandlersAsScoped()
    {
        var services = new ServiceCollection();

        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var serviceProvider = services.BuildServiceProvider();
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var handler1 = scope1.ServiceProvider.GetService<IRequestHandler<TestQuery, string>>();
        var handler2 = scope1.ServiceProvider.GetService<IRequestHandler<TestQuery, string>>();
        var handler3 = scope2.ServiceProvider.GetService<IRequestHandler<TestQuery, string>>();

        handler1.Should().BeSameAs(handler2);
        handler1.Should().NotBeSameAs(handler3);
    }

    [Fact]
    public void AddPipelineBehavior_WithType_RegistersBehavior()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        services.AddPipelineBehavior(typeof(TestPipelineBehavior<,>));

        var serviceProvider = services.BuildServiceProvider();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TestQuery, string>>();

        behaviors.Should().NotBeEmpty();
    }

    [Fact]
    public void AddPipelineBehavior_WithNullServices_ThrowsArgumentNullException()
    {
        ServiceCollection services = null!;

        var act = () => services.AddPipelineBehavior(typeof(TestPipelineBehavior<,>));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPipelineBehavior_WithNullBehaviorType_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var act = () => services.AddPipelineBehavior((Type)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPipelineBehavior_Generic_WithNullServices_ThrowsArgumentNullException()
    {
        ServiceCollection services = null!;

        var act = () => services.AddPipelineBehavior<TestPipelineBehavior<TestQuery, string>>();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPipelineBehavior_Generic_RegistersBehavior()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        services.AddPipelineBehavior<TestPipelineBehavior<TestQuery, string>>();

        var serviceProvider = services.BuildServiceProvider();
        var behavior = serviceProvider.GetService<TestPipelineBehavior<TestQuery, string>>();

        behavior.Should().NotBeNull();
    }

    [Fact]
    public void AddDispatcher_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddDispatcher(Assembly.GetExecutingAssembly());

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddPipelineBehavior_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(Assembly.GetExecutingAssembly());

        var result = services.AddPipelineBehavior(typeof(TestPipelineBehavior<,>));

        result.Should().BeSameAs(services);
    }
}

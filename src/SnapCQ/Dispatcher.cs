using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SnapCQ.Abstractions;

namespace SnapCQ;

/// <summary>
/// Represents the core implementation of <see cref="IDispatcher" />, providing mechanisms for sending requests
/// and publishing notifications within the SnapCQ framework.
/// </summary>
/// <remarks>
/// This class leverages an <see cref="IServiceProvider" /> to resolve handlers dynamically using dependency injection
/// and adheres to the configuration options specified in <see cref="DispatcherOptions" />.
/// It is designed to handle asynchronous operations and manage the lifecycle of request handlers.
/// </remarks>
public sealed class Dispatcher : IDispatcher
{
    /// <summary>
    /// Holds the configuration options for the dispatcher used by the <see cref="Dispatcher"/> class.
    /// Determines behavior such as the lifetime of the request handlers.
    /// </summary>
    private readonly DispatcherOptions _options;

    /// <summary>
    /// Represents the service provider instance used for resolving dependencies and obtaining registered services during request and notification handling processes.
    /// </summary>
    /// <remarks>
    /// The <c>_provider</c> field is an instance of <see cref="IServiceProvider"/> used internally by the <see cref="Dispatcher"/> class.
    /// It facilitates the retrieval of handler implementations, pipeline behaviors, and other dependencies required for processing requests and notifications.
    /// This field ensures that the logic in the <see cref="Dispatcher"/> class is decoupled from specific handler and behavior implementations.
    /// </remarks>
    private readonly IServiceProvider _provider;


    /// <summary>
    /// A thread-safe cache for storing type-specific information related to request handlers and behaviors.
    /// Used internally by the <see cref="Dispatcher"/> to optimize the resolution of handlers and behaviors
    /// for incoming requests, improving performance by avoiding repeated reflection-based lookups.
    /// </summary>
    /// <remarks>
    /// The cache is implemented using a ConcurrentDictionary to provide concurrent access for multiple threads.
    /// It stores instances of the nested <c>CachedHandlerInfo</c> class, which encapsulates metadata and state
    /// related to a specific request type, such as the associated handler type, behavior types, and their delegates.
    /// </remarks>
    private readonly ConcurrentDictionary<Type, CachedHandlerInfo> _typeCache = new();


    /// <summary>
    /// Default implementation of <see cref="IDispatcher" />.
    /// </summary>
    /// <remarks>
    /// Provides methods to send requests and publish notifications. Utilizes dependency injection to resolve
    /// handlers and supports various behaviors for request processing.
    /// </remarks>
    public Dispatcher(IServiceProvider provider, DispatcherOptions options)
    {
        _provider = provider;
        _options = options;
    }


    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken ct = default)
    {
#if DEBUG
        ArgumentNullException.ThrowIfNull(request);
#endif

        var requestType = request.GetType();


        if (!_typeCache.TryGetValue(requestType, out var cachedInfo))
        {
            cachedInfo = CreateCachedHandlerInfo<TResponse>(requestType);
            cachedInfo = _typeCache.GetOrAdd(requestType, cachedInfo);
        }


        object handler;
        if (cachedInfo is { IsSingleton: true, CachedHandlerInstance: not null })
        {
            handler = cachedInfo.CachedHandlerInstance;
        }
        else
        {
            handler = _provider.GetService(cachedInfo.HandlerType)
                      ?? throw new InvalidOperationException($"No handler registered for {requestType.Name}");


            if (cachedInfo is { IsSingleton: true, CachedHandlerInstance: null })
                cachedInfo.CachedHandlerInstance = handler;
        }

        if (!cachedInfo.HasBehaviors)
        {
            var resultTask = cachedInfo.HandlerDelegate(handler, request, ct);
            if (resultTask.IsCompletedSuccessfully) return (TResponse)resultTask.Result;
            return await ExecuteFastPathAsync<TResponse>(resultTask).ConfigureAwait(false);
        }

        var behaviorsArray = cachedInfo.CachedBehaviors!;

        RequestHandlerDelegate<TResponse> handlerDelegate = async () =>
        {
            var result = await cachedInfo.HandlerDelegate(handler, request, ct).ConfigureAwait(false);
            return (TResponse)result;
        };
        
        for (var i = behaviorsArray.Length - 1; i >= 0; i--)
        {
            var behavior = behaviorsArray[i];
            var currentDelegate = handlerDelegate;

            handlerDelegate = async () =>
            {
                var result = await cachedInfo.BehaviorDelegate!(behavior, request, currentDelegate, ct)
                    .ConfigureAwait(false);
                return (TResponse)result;
            };
        }

        return await handlerDelegate().ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes a notification to all registered notification handlers for the specified notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification to publish. Must implement <see cref="INotification"/>.</typeparam>
    /// <param name="notification">The notification instance to be published. Cannot be null.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask PublishAsync<TNotification>(
        TNotification notification,
        CancellationToken ct = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);


        var handlers = _provider.GetServices<INotificationHandler<TNotification>>();


        foreach (var handler in handlers) await handler.HandleAsync(notification, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a cached handler information object for the specified request type.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response expected from the handler.</typeparam>
    /// <param name="requestType">The type of the request for which to create the handler information.</param>
    /// <returns>A <see cref="Dispatcher.CachedHandlerInfo"/> instance containing metadata and delegates for handling the request.</returns>
    private CachedHandlerInfo CreateCachedHandlerInfo<TResponse>(Type requestType)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));


        var handlerMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync))!;
        var handlerDelegate = CreateCompiledDelegate(handlerMethod);


        var behaviorsEnumerable = _provider.GetServices(behaviorType);
        var behaviorsArray = behaviorsEnumerable.Cast<object>().ToArray();
        var hasBehaviors = behaviorsArray.Length > 0;


        Func<object, object, Delegate, CancellationToken, ValueTask<object>>? behaviorDelegate = null;
        if (hasBehaviors)
        {
            var behaviorMethod =
                behaviorType.GetMethod(nameof(IPipelineBehavior<IRequest<TResponse>, TResponse>.HandleAsync))!;
            behaviorDelegate = CreateCompiledBehaviorDelegate(behaviorMethod);
        }


        var (isSingleton, isScoped) = GetHandlerLifetime();

        return new CachedHandlerInfo
        {
            HandlerType = handlerType,
            BehaviorType = behaviorType,
            HasBehaviors = hasBehaviors,
            HandlerDelegate = handlerDelegate,
            BehaviorDelegate = behaviorDelegate,
            IsSingleton = isSingleton,
            IsScoped = isScoped,
            CachedHandlerInstance = null,
            CachedBehaviors = hasBehaviors ? behaviorsArray : null
        };
    }

    /// <summary>
    /// Determines the lifetime of a handler based on the configured <see cref="DispatcherOptions.HandlerLifetime" />.
    /// </summary>
    /// <returns>
    /// A tuple indicating the lifetime of the handler:
    /// - <c>IsSingleton</c>: True if the handler has a singleton lifetime.
    /// - <c>IsScoped</c>: True if the handler has a scoped lifetime.
    /// If both values are false, the handler has a transient lifetime.
    /// </returns>
    private (bool IsSingleton, bool IsScoped) GetHandlerLifetime()
    {
        return _options.HandlerLifetime switch
        {
            ServiceLifetime.Singleton => (true, false),
            ServiceLifetime.Scoped => (false, true),
            ServiceLifetime.Transient => (false, false),
            _ => (false, true)
        };
    }

    /// <summary>
    /// Compiles a delegate for invoking a specified method with a given handler, request, and cancellation token.
    /// </summary>
    /// <param name="method">The method to create a compiled delegate for. Should be compatible with the expected signature: handler, request, and cancellation token.</param>
    /// <returns>A compiled delegate that allows invoking the specified method dynamically.</returns>
    private static Func<object, object, CancellationToken, ValueTask<object>> CreateCompiledDelegate(MethodInfo method)
    {
        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");


        var handlerCast = Expression.Convert(handlerParam, method.DeclaringType!);
        var requestCast = Expression.Convert(requestParam, method.GetParameters()[0].ParameterType);


        var methodCall = Expression.Call(handlerCast, method, requestCast, ctParam);
        
        var taskResultType = method.ReturnType.GetGenericArguments()[0];
        
        var convertMethod = typeof(Dispatcher)
            .GetMethod(nameof(ConvertValueTaskToObject), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(taskResultType);

        var convertCall = Expression.Call(convertMethod, methodCall);
        
        var lambda = Expression.Lambda<Func<object, object, CancellationToken, ValueTask<object>>>(
            convertCall, handlerParam, requestParam, ctParam);

        return lambda.Compile();
    }

    /// <summary>
    /// Executes a fast path operation for a task that produces a response of type <typeparamref name="TResponse" />.
    /// This method is optimized to handle scenarios where the task completes successfully
    /// without requiring additional processing overhead.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response returned by the task.</typeparam>
    /// <param name="task">The task that produces an object result, which will be cast to <typeparamref name="TResponse" />.</param>
    /// <returns>A value task that represents the asynchronous operation, containing the response of type <typeparamref name="TResponse" />.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<TResponse> ExecuteFastPathAsync<TResponse>(ValueTask<object> task)
    {
        var result = await task.ConfigureAwait(false);
        return (TResponse)result;
    }

    /// <summary>
    /// Converts a <see cref="ValueTask{TResponse}"/> to an object.
    /// </summary>
    /// <typeparam name="TResponse">The type of the task result.</typeparam>
    /// <param name="task">The asynchronous task to be converted.</param>
    /// <returns>A <see cref="ValueTask{T}"/> representing the result as an object.</returns>
    /// <remarks>
    /// This method is used internally to handle the conversion of generic asynchronous task results
    /// into a non-generic object for compatibility with the dispatcher runtime.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<object> ConvertValueTaskToObject<TResponse>(ValueTask<TResponse> task)
    {
        var result = await task.ConfigureAwait(false);
        return result!;
    }

    /// <summary>
    /// Creates a compiled delegate for invoking a pipeline behavior's HandleAsync method.
    /// This method constructs a lambda expression to dynamically call the specified method,
    /// ensuring proper type conversions for the parameters and return type.
    /// </summary>
    /// <param name="method">
    /// The <see cref="MethodInfo"/> representing the method to be compiled into a delegate.
    /// The method must conform to the expected signature for pipeline behaviors, with specific
    /// parameters for the behavior instance, request, next delegate, and cancellation token.
    /// </param>
    /// <returns>
    /// A compiled delegate of type <see cref="Func{T1, T2, T3, T4, TResult}"/> that takes
    /// an object representing the behavior instance, an object for the request, a delegate
    /// representing the next behavior in the pipeline, a <see cref="CancellationToken"/>,
    /// and returns a <see cref="ValueTask{TResult}"/> wrapped as a <see cref="ValueTask{Object}"/>.
    /// </returns>
    private static Func<object, object, Delegate, CancellationToken, ValueTask<object>> CreateCompiledBehaviorDelegate(
        MethodInfo method)
    {
        var behaviorParam = Expression.Parameter(typeof(object), "behavior");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var nextParam = Expression.Parameter(typeof(Delegate), "next");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");


        var behaviorCast = Expression.Convert(behaviorParam, method.DeclaringType!);
        var requestCast = Expression.Convert(requestParam, method.GetParameters()[0].ParameterType);
        var nextCast = Expression.Convert(nextParam, method.GetParameters()[1].ParameterType);
        
        var methodCall = Expression.Call(behaviorCast, method, requestCast, nextCast, ctParam);
        
        var taskResultType = method.ReturnType.GetGenericArguments()[0];
        
        var convertMethod = typeof(Dispatcher)
            .GetMethod(nameof(ConvertValueTaskToObject), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(taskResultType);

        var convertCall = Expression.Call(convertMethod, methodCall);
        
        var lambda = Expression.Lambda<Func<object, object, Delegate, CancellationToken, ValueTask<object>>>(
            convertCall, behaviorParam, requestParam, nextParam, ctParam);

        return lambda.Compile();
    }

    /// <summary>
    /// Represents a cached structure used internally by <see cref="Dispatcher"/> to manage
    /// information about request handlers and pipeline behaviors.
    /// </summary>
    private sealed class CachedHandlerInfo
    {
        /// <summary>
        /// Gets the type of the handler associated with the request.
        /// This property represents the concrete implementation type of the handler
        /// used to process a specific request in the dispatcher pipeline.
        /// </summary>
        public required Type HandlerType { get; init; }

        /// <summary>
        /// Represents the type of the pipeline behavior associated with a handler for a specific request-response pair.
        /// </summary>
        /// <remarks>
        /// This property is used to identify the behavior type tied to the execution of the request handler in a pipeline.
        /// It allows for the implementation of cross-cutting concerns such as logging, validation, and monitoring
        /// by enabling behaviors to be composed around the request handling process.
        /// </remarks>
        public required Type BehaviorType { get; init; }

        /// <summary>
        /// Indicates whether the pipeline for a given request includes any behaviors.
        /// </summary>
        /// <remarks>
        /// This property is set during the creation of a cached handler and reflects
        /// whether any pipeline behaviors are registered for a specific request type.
        /// Pipeline behaviors are middleware components that can execute logic before or
        /// after the request handler is executed.
        /// </remarks>
        public required bool HasBehaviors { get; init; }

        /// <summary>
        /// A delegate used to handle the core request-processing logic for a specific request type.
        /// This delegate is invoked by the <see cref="Dispatcher"/> to execute the handler's
        /// asynchronous processing function, taking the handler instance, request, and cancellation token
        /// as parameters, and returning the result as a <see cref="ValueTask{TResult}"/>.
        /// </summary>
        public required Func<object, object, CancellationToken, ValueTask<object>> HandlerDelegate { get; init; }

        /// <summary>
        /// Represents a delegate that defines the execution behavior of a pipeline component within the
        /// <see cref="Dispatcher"/>. This delegate facilitates the invocation of a behavior and its interaction
        /// with subsequent behaviors or the final request handler in the pipeline. It accepts the current
        /// behavior instance, the request object, the next delegate in the chain, and a cancellation token,
        /// producing an asynchronous task that yields the result of the operation.
        /// </summary>
        public required Func<object, object, Delegate, CancellationToken, ValueTask<object>>? BehaviorDelegate
        {
            get;
            init;
        }

        /// <summary>
        /// Represents a lazily initialized and optionally cached instance of the request handler,
        /// which is used to process incoming requests within the dispatcher.
        /// </summary>
        /// <remarks>
        /// This property holds the handler instance for a specific request type, primarily when the handler is marked as singleton.
        /// If the handler is not a singleton or if it has not been initialized, this property may be null.
        /// For singleton handlers, caching the instance improves performance by avoiding repeated resolution or instantiation of the handler.
        /// </remarks>
        public object? CachedHandlerInstance { get; set; }

        /// <summary>
        /// Gets a value indicating whether the handler is registered as a singleton within the dependency injection container.
        /// </summary>
        /// <remarks>
        /// When this property is <c>true</c>, the handler instance will be shared across all requests
        /// and the same instance will be used for each invocation. Singleton handlers are ideal for
        /// stateless or thread-safe implementations that do not require per-request instantiation.
        /// </remarks>
        public bool IsSingleton { get; init; }

        /// Indicates whether the handler is scoped in its lifetime.
        /// A value of true means the handler's lifetime is tied to the scope in which it is resolved,
        /// typically corresponding to a single request or operation. If false, the handler is not scoped.
        /// Scoped handlers are created fresh for each scoped context and ensure isolation of dependencies.
        /// This property is used internally to determine the lifetime behavior for the associated handler.
        public bool IsScoped { get; init; }

        /// <summary>
        /// Stores an array of cached behaviors associated with a request handler pipeline.
        /// </summary>
        /// <remarks>
        /// If behaviors are registered in the pipeline, this property contains the instances of those behaviors.
        /// It is used to execute the behaviors in the defined order when processing a request. If no behaviors
        /// are defined, this property will be null. The caching mechanism optimizes the pipeline execution by
        /// avoiding repeated resolutions of behavior instances.
        /// </remarks>
        public object[]? CachedBehaviors { get; init; }
    }
}
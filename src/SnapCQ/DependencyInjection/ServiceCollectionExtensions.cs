using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SnapCQ.Abstractions;

namespace SnapCQ.DependencyInjection;

/// <summary>
/// Provides extension methods to extend the functionality of the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers handlers from the specified assembly that implement the given interface type
    /// and configures them with the provided service lifetime.
    /// </summary>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <param name="assembly">The assembly to scan for handler implementations.</param>
    /// <param name="interfaceType">The generic interface type to match handlers against.</param>
    /// <param name="lifetime">The service lifetime to configure for the registered handlers.</param>
    private static void RegisterHandlers(
        IServiceCollection services,
        Assembly assembly,
        Type interfaceType,
        ServiceLifetime lifetime)
    {
        var handlers = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType)
                .Select(i => new { Interface = i, Implementation = t }));

        foreach (var handler in handlers)
            services.Add(new ServiceDescriptor(handler.Interface, handler.Implementation, lifetime));
    }

    /// <summary>
    /// Represents options for configuring the dispatcher.
    /// </summary>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the dispatcher and automatically registers all handlers found in the provided assemblies.
        /// </summary>
        /// <param name="assemblies">An array of assemblies to scan for handler implementations.</param>
        /// <param name="handlerLifetime">
        /// Specifies the service lifetime for the registered request handlers. Defaults to
        /// <see cref="ServiceLifetime.Scoped" />.
        /// </param>
        /// <returns>The updated service collection to enable method chaining.</returns>
        public IServiceCollection AddDispatcher(params Assembly[] assemblies)
            => services.AddDispatcher(assemblies, ServiceLifetime.Scoped);

        public IServiceCollection AddDispatcher(Assembly[] assemblies,
            ServiceLifetime handlerLifetime)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(assemblies);

            var options = new DispatcherOptions { HandlerLifetime = handlerLifetime };
            services.AddSingleton(options);

            services.AddSingleton<Dispatcher>();
            services.AddSingleton<IDispatcher>(sp => sp.GetRequiredService<Dispatcher>());

            foreach (var assembly in assemblies)
            {
                RegisterHandlers(services, assembly, typeof(IRequestHandler<,>), handlerLifetime);
                RegisterHandlers(services, assembly, typeof(INotificationHandler<>), handlerLifetime);
            }

            return services;
        }

        /// <summary>
        /// Adds a pipeline behavior.
        /// </summary>
        /// <param name="behaviorType">The open generic behavior type.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddPipelineBehavior(Type behaviorType)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(behaviorType);

            services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
            return services;
        }

        /// <summary>
        /// Adds a pipeline behavior of the specified generic type to the service collection.
        /// </summary>
        /// <typeparam name="TBehavior">The type of the pipeline behavior to register.</typeparam>
        /// <returns>The updated service collection to enable method chaining.</returns>
        public IServiceCollection AddPipelineBehavior<TBehavior>()
            where TBehavior : class
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddScoped<TBehavior>();
            return services;
        }
    }
}
using Microsoft.Extensions.DependencyInjection;

namespace SnapCQ;

/// <summary>
///     Configuration options for the dispatcher.
/// </summary>
public sealed class DispatcherOptions
{
    /// <summary>
    ///     Gets or sets the service lifetime for request handlers.
    ///     Default is <see cref="ServiceLifetime.Scoped" />.
    /// </summary>
    public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Scoped;
}
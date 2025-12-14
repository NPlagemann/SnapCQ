namespace SnapCQ.Abstractions;

/// <summary>
/// Represents a mechanism for dispatching requests and notifications to their respective handlers.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Sends a request through the pipeline to its handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response from the handler.</returns>
    ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);

    /// <summary>
    /// Publishes a notification to all registered notification handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification to publish.</typeparam>
    /// <param name="notification">The notification to be published.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification;
}
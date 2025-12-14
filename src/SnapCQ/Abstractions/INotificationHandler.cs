namespace SnapCQ.Abstractions;

/// <summary>
/// Represents a contract for handling notifications.
/// </summary>
/// <typeparam name="TNotification">The type of the notification being handled.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the notification.
    /// </summary>
    /// <param name="notification">The notification to be processed.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask HandleAsync(TNotification notification, CancellationToken ct = default);
}
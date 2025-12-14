namespace SnapCQ.Abstractions;

/// <summary>
/// Delegate representing the next handler in the pipeline during request processing.
/// </summary>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Represents a pipeline behavior that can be used to process requests and responses within a pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of the request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response being returned.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request in the pipeline.
    /// </summary>
    /// <param name="request">The request to be processed.</param>
    /// <param name="next">The next handler in the pipeline to invoke.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The response from the request after processing.</returns>
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct = default);
}
namespace SnapCQ.Abstractions;

/// <summary>
/// Marker interface for requests with or without a response.
/// </summary>
public interface IRequest<out TResponse>;

/// <summary>
/// Marker interface for requests without a response.
/// </summary>
public interface IRequest : IRequest<Unit>;
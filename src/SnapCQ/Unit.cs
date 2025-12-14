namespace SnapCQ;

/// <summary>
/// Represents a void type, since void is not a valid return type in C#.
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// Represents the default instance of the <see cref="Unit"/> type.
    /// </summary>
    public static readonly Unit Value = new();
}
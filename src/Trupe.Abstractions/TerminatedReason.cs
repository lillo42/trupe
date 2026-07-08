namespace Trupe.Abstractions;

/// <summary>
/// Represents the reason an actor was terminated.
/// </summary>
/// <param name="Reason">An optional description of the termination reason.</param>
public record TerminatedReason(string? Reason = null)
{
    /// <summary>
    /// Gets a predefined termination reason indicating the actor was explicitly stopped.
    /// </summary>
    public static TerminatedReason Stopped { get; } = new("Stopped");
}

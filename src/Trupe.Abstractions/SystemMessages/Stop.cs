namespace Trupe.Abstractions.SystemMessages;

/// <summary>
/// System message sent to an actor to request its termination.
/// </summary>
/// <param name="Reason">An optional reason describing why the actor should be terminated.</param>
public record Stop(string? Reason = null);

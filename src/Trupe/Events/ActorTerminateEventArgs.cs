using System;

namespace Trupe.Events;

/// <summary>
/// Provides data for actor terminate events within the Trupe actor system.
/// </summary>
/// <remarks>
/// This event args class is used to communicate details about an actor termination request,
/// including the actor being terminated and an optional reason for the termination.
/// </remarks>
/// <param name="actor">The actor that is being terminated.</param>
/// <param name="reason">An optional reason describing why the actor is being terminated.</param>
public class ActorTerminateEventArgs(IActor actor, string? reason) : EventArgs
{
    /// <summary>
    /// Gets the actor that is being terminated.
    /// </summary>
    public IActor Actor { get; } = actor;

    /// <summary>
    /// Gets the optional reason describing why the actor is being terminated.
    /// </summary>
    public string? Reason { get; } = reason;
}

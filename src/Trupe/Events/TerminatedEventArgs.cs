using System;
using Trupe.ActorReferences;

namespace Trupe.Events;

/// <summary>
/// Provides data for actor terminated events within the Trupe actor system.
/// </summary>
/// <remarks>
/// This event args class is used to notify listeners that an actor has been terminated,
/// including the actor reference and an optional reason for the termination.
/// </remarks>
/// <param name="reference">The reference to the actor that was terminated.</param>
/// <param name="reason">An optional reason describing why the actor was terminated.</param>
public class TerminatedEventArgs(IActorReference reference, string? reason) : EventArgs
{
    /// <summary>
    /// Gets the reference to the actor that was terminated.
    /// </summary>
    public IActorReference Reference { get; } = reference;

    /// <summary>
    /// Gets the optional reason describing why the actor was terminated.
    /// </summary>
    public string? Reason { get; } = reason;
}

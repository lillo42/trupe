using System;

namespace Trupe.Abstractions.Events;

/// <summary>
/// Provides data for the event raised when an actor process is stopped.
/// </summary>
/// <param name="process">The actor process that was stopped.</param>
/// <param name="reason">The reason for the process being stopped.</param>
public class ActorProcessStoppedEventArgs(IActorProcess process, TerminatedReason reason)
    : EventArgs
{
    /// <summary>
    /// Gets the actor process that was stopped.
    /// </summary>
    public IActorProcess Process { get; } = process;

    /// <summary>
    /// Gets the reason the actor process was stopped.
    /// </summary>
    public TerminatedReason Reason { get; } = reason;
}

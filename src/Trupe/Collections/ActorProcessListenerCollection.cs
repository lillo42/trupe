using System;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;

namespace Trupe.Collections;

/// <summary>
/// A thread-safe collection of <see cref="IActorProcessListener"/> instances that supports
/// adding, removing, and notifying listeners about actor process lifecycle events.
/// </summary>
public class ActorProcessListenerCollection : ListenerCollection<IActorProcessListener>
{
    /// <summary>
    /// Notifies all registered listeners that an actor process has encountered an unhandled exception.
    /// Exceptions thrown by individual listeners are silently swallowed.
    /// </summary>
    /// <param name="process">The actor process that failed.</param>
    /// <param name="message">The message being processed when the failure occurred.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    public void InvokeOnFailed(IActorProcess process, IMessage message, Exception exception)
    {
        Invoke(listener => listener.OnFailed(process, message, exception));
    }

    /// <summary>
    /// Notifies all registered listeners that an actor process has stopped.
    /// Exceptions thrown by individual listeners are silently swallowed.
    /// </summary>
    /// <param name="process">The actor process that stopped.</param>
    /// <param name="reason">The reason for the stop.</param>
    public void InvokeOnStopped(IActorProcess process, TerminatedReason reason)
    {
        Invoke(listener => listener.OnStopped(process, reason));
    }
}

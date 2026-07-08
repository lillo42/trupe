using Trupe.Abstractions;

namespace Trupe.Collections;

/// <summary>
/// A thread-safe collection of <see cref="IActorReferenceListener"/> instances that supports
/// adding, removing, and notifying listeners about actor reference termination events.
/// </summary>
public class ActorReferenceListenerCollection : ListenerCollection<IActorReferenceListener>
{
    /// <summary>
    /// Notifies all registered listeners that the specified actor reference has terminated.
    /// Exceptions thrown by individual listeners are silently swallowed.
    /// </summary>
    /// <param name="reference">The actor reference that was terminated.</param>
    /// <param name="reason">The reason for termination.</param>
    public void InvokeOnTerminated(IActorReference reference, TerminatedReason reason)
    {
        Invoke(listener => listener.OnTerminated(reference, reason));
    }
}

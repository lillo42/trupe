namespace Trupe.Abstractions;

/// <summary>
/// Defines a listener that receives notifications when an actor reference is terminated.
/// </summary>
public interface IActorReferenceListener
{
    /// <summary>
    /// Called when the observed actor reference has been terminated.
    /// </summary>
    /// <param name="reference">The actor reference that was terminated.</param>
    /// <param name="reason">The reason for termination.</param>
    void OnTerminated(IActorReference reference, TerminatedReason reason);
}

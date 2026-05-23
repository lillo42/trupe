namespace Trupe.Abstractions.SystemMessages;

/// <summary>
/// System message indicating that a watched actor reference has been terminated.
/// </summary>
/// <param name="Reference">The actor reference that was terminated.</param>
/// <param name="Reason">The reason for the termination.</param>
public record ActorTerminated(IActorReference Reference, TerminatedReason Reason);

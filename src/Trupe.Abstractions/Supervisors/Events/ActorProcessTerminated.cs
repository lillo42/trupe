namespace Trupe.Abstractions.Supervisors.Commands;

/// <summary>
/// Event indicating that an actor process has been stopped.
/// </summary>
/// <param name="Process">The actor process that was stopped.</param>
/// <param name="Reason">The reason for the process being stopped, or <see langword="null"/> if not specified.</param>
public record ActorProcessStopped(IActorProcess Process, TerminatedReason? Reason);

using Trupe.Abstractions;

namespace Trupe.Supervisors.Commands;

/// <summary>
/// Command indicating that a supervised actor has been terminated.
/// </summary>
/// <param name="Actor">The actor instance that was terminated.</param>
/// <param name="Reason">An optional reason describing why the actor was terminated.</param>
/// <remarks>
/// This command is sent to a supervisor when one of its child actors is terminated.
/// The supervisor uses this information to update its internal state and
/// take any necessary follow-up actions.
/// </remarks>
public record ActorTerminated(IActor Actor, string? Reason);

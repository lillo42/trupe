using System;
using Trupe.ActorReferences;
using Trupe.Messages;

namespace Trupe.Exceptions;

/// <summary>
/// Exception thrown when a failure is escalated from a child actor to its supervisor.
/// </summary>
/// <remarks>
/// This exception is used by the supervision system to propagate failures up the actor hierarchy.
/// When a supervisor decides to escalate a failure (rather than restart, stop, or resume the actor),
/// this exception wraps the original failure information and is passed to the parent supervisor.
/// </remarks>
/// <param name="message">The message that describes the escalated failure.</param>
/// <param name="actorReference">The reference to the actor that caused the failure.</param>
/// <param name="actorMessage">The message that was being processed when the failure occurred.</param>
/// <param name="inner">The original exception that caused the failure.</param>
public class EscalateFailureException(
    string message,
    IActorReference actorReference,
    IMessage actorMessage,
    Exception inner
) : TrupeException(message, inner)
{
    /// <summary>
    /// Gets the message that was being processed when the failure occurred.
    /// </summary>
    public IMessage ActorMessage { get; } = actorMessage;

    /// <summary>
    /// Gets the reference to the actor that caused the failure.
    /// </summary>
    public IActorReference ActorReference { get; } = actorReference;
}

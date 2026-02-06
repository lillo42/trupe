using System;
using Trupe.Messages;

namespace Trupe.Events;

/// <summary>
/// Provides data for actor failure events within the Trupe actor system.
/// </summary>
/// <remarks>
/// This event args class is used to communicate details about actor failures to event handlers,
/// including the actor that failed, the message being processed, and the exception that occurred.
/// </remarks>
/// <param name="actor">The actor that experienced the failure.</param>
/// <param name="message">The message that was being processed when the failure occurred.</param>
/// <param name="exception">The exception that caused the failure.</param>
public class ActorFailureEventArgs(IActor actor, IMessage message, Exception exception) : EventArgs
{
    /// <summary>
    /// Gets the actor that experienced the failure.
    /// </summary>
    public IActor Actor { get; } = actor;

    /// <summary>
    /// Gets the message that was being processed when the failure occurred.
    /// </summary>
    public IMessage Message { get; } = message;

    /// <summary>
    /// Gets the exception that caused the actor failure.
    /// </summary>
    public Exception Exception { get; } = exception;
}

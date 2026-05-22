using System;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Events;

/// <summary>
/// Provides data for the event raised when an actor process fails during message processing.
/// </summary>
/// <param name="process">The actor process that encountered the failure.</param>
/// <param name="message">The message that was being processed when the failure occurred.</param>
/// <param name="exception">The exception that caused the failure.</param>
public class ActorProcessFailedEvetArgs(
    IActorProcess process,
    IMessage message,
    Exception exception
) : EventArgs
{
    /// <summary>
    /// Gets the actor process that encountered the failure.
    /// </summary>
    public IActorProcess Process { get; } = process;

    /// <summary>
    /// Gets the message that was being processed when the failure occurred.
    /// </summary>
    public IMessage Message { get; } = message;

    /// <summary>
    /// Gets the exception that caused the failure.
    /// </summary>
    public Exception Exception { get; } = exception;
}

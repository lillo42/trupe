using System;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions;

/// <summary>
/// Defines a listener that receives notifications about actor process lifecycle events,
/// including failures and stops.
/// </summary>
public interface IActorProcessListener
{
    /// <summary>
    /// Called when an actor process encounters an unhandled exception while processing a message.
    /// </summary>
    /// <param name="process">The actor process that failed.</param>
    /// <param name="message">The message that was being processed when the failure occurred.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    void OnFailed(IActorProcess process, IMessage message, Exception exception);

    /// <summary>
    /// Called when an actor process has stopped, either gracefully or due to termination.
    /// </summary>
    /// <param name="process">The actor process that stopped.</param>
    /// <param name="reason">The reason for the stop.</param>
    void OnStopped(IActorProcess process, TerminatedReason reason);
}

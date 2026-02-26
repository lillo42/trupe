using System;
using Trupe.Abstractions;
using Trupe.Abstractions.Messages;

namespace Trupe.Supervisors.Commands;

/// <summary>
/// Command indicating that an actor failed while processing a message.
/// </summary>
/// <param name="Actor">The actor instance that encountered the failure.</param>
/// <param name="Message">The message that was being processed when the failure occurred.</param>
/// <param name="Exception">The exception that caused the failure.</param>
/// <remarks>
/// This command is sent to a supervisor when an actor throws an unhandled exception
/// during message processing. The supervisor uses this information to determine the
/// appropriate supervision strategy (e.g., restart, stop, escalate).
/// </remarks>
public record ActorFailed(IActor Actor, IMessage Message, Exception Exception);

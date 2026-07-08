using System;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Supervisors.Events;

/// <summary>
/// Event indicating that an actor process failed while processing a message.
/// </summary>
/// <param name="Process">The actor process that encountered the failure.</param>
/// <param name="Message">The message that was being processed when the failure occurred.</param>
/// <param name="Exception">The exception that caused the failure.</param>
public record ActorProcessFailed(IActorProcess Process, IMessage Message, Exception Exception);

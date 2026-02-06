using System;
using Trupe.ActorReferences;
using Trupe.Messages;

namespace Trupe.Exceptions;

public class EscalateFailureException(
    string message,
    IActorReference actorReference,
    IMessage actorMessage,
    Exception inner
) : TrupeException(message, inner)
{
    public IMessage ActorMessage { get; } = actorMessage;
    public IActorReference ActorReference { get; } = actorReference;
}

using System;
using Trupe.Messages;

namespace Trupe.Events;

public class ActorFailureEventArgs(IActor actor, IMessage message, Exception exception) : EventArgs
{
    public IActor Actor { get; } = actor;

    public IMessage Message { get; } = message;

    public Exception Exception { get; } = exception;
}

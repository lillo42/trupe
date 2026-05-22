using System;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions.Events;

public class ActorProcessFailedEvetArgs(
    IActorProcess process,
    IMessage message,
    Exception exception
) : EventArgs
{
    public IActorProcess Process { get; } = process;

    public IMessage Message { get; } = message;

    public Exception Exception { get; } = exception;
}

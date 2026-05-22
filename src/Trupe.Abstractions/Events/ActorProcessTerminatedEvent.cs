using System;

namespace Trupe.Abstractions.Events;

public class ActorProcessStoppedEventArgs(IActorProcess process, TerminatedReason reason)
    : EventArgs
{
    public IActorProcess Process { get; } = process;

    public TerminatedReason Reason { get; } = reason;
}

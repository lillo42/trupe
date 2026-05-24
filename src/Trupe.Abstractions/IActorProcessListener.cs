using System;
using Trupe.Abstractions.Messages;

namespace Trupe.Abstractions;

public interface IActorProcessListener
{
    void OnFailed(IActorProcess process, IMessage message, Exception exception);

    void OnStopped(IActorProcess process, TerminatedReason reason);
}

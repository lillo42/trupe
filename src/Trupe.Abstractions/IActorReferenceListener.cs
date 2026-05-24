namespace Trupe.Abstractions;

public interface IActorReferenceListener
{
    void OnTerminated(IActorReference reference, TerminatedReason reason);
}

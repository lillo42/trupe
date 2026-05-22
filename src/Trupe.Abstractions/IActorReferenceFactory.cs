namespace Trupe.Abstractions;

public interface IActorReferenceFactory
{
    IActorReference Create(string name, IActorProcess process);
}

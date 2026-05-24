namespace Trupe.Abstractions.Exceptions;

public class ActorProcessNotRegisterException(IActorReference reference)
    : TrupeException($"Actor process not found for {reference.Name}")
{
    public IActorReference Reference { get; } = reference;
}

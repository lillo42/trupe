namespace Trupe.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when an actor process cannot be found in the registry for a given actor reference.
/// </summary>
/// <param name="reference">The actor reference for which no process was registered.</param>
public class ActorProcessNotRegisterException(IActorReference reference)
    : TrupeException($"Actor process not found for {reference.Name}")
{
    /// <summary>
    /// Gets the actor reference for which no registered process was found.
    /// </summary>
    public IActorReference Reference { get; } = reference;
}

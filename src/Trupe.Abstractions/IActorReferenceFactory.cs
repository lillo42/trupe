namespace Trupe.Abstractions;

/// <summary>
/// Factory for creating actor references and registering them in the process registry.
/// </summary>
public interface IActorReferenceFactory
{
    /// <summary>
    /// Creates a new actor reference with the specified name and associates it with the given process.
    /// </summary>
    /// <param name="name">The unique name for the actor reference.</param>
    /// <param name="process">The actor process to associate with the reference.</param>
    /// <returns>A new actor reference.</returns>
    IActorReference Create(string name, IActorProcess process);
}

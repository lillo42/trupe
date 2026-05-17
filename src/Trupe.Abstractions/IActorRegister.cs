namespace Trupe.Abstractions;

/// <summary>
/// Provides a registry for looking up and managing actor references by their identifiers.
/// </summary>
public interface IActorRegister
{
    /// <summary>
    /// Registers an actor reference with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the actor.</param>
    /// <param name="actor">The actor reference to register.</param>
    /// <exception cref="System.ArgumentException">Thrown when an actor with the same <paramref name="id"/> is already registered.</exception>
    void Register(string id, IActorReference actor);

    /// <summary>
    /// Attempts to register an actor reference with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the actor.</param>
    /// <param name="actor">The actor reference to register.</param>
    /// <returns><see langword="true"/> if the actor was registered successfully; <see langword="false"/> if an actor with the same <paramref name="id"/> already exists.</returns>
    bool TryRegister(string id, IActorReference actor);

    /// <summary>
    /// Gets the actor reference associated with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the actor to retrieve.</param>
    /// <returns>The <see cref="IActorReference"/> if found; otherwise, <see langword="null"/>.</returns>
    IActorReference? Get(string id);

    /// <summary>
    /// Attempts to get the actor reference associated with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the actor to retrieve.</param>
    /// <param name="actor">When this method returns, contains the actor reference if found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an actor with the specified <paramref name="id"/> was found; otherwise, <see langword="false"/>.</returns>
    bool TryGet(string id, out IActorReference? actor);

    /// <summary>
    /// Determines whether an actor with the specified identifier is registered.
    /// </summary>
    /// <param name="id">The unique identifier to check.</param>
    /// <returns><see langword="true"/> if an actor with the specified <paramref name="id"/> is registered; otherwise, <see langword="false"/>.</returns>
    bool Contains(string id);

    void Unregister(string id);
}

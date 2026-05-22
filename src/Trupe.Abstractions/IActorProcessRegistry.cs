using System;

namespace Trupe.Abstractions;

/// <summary>
/// Provides a registry for looking up and managing actor references and their associated processes.
/// </summary>
public interface IActorProcessRegistry
{
    /// <summary>
    /// Registers an actor reference and its associated process in the registry.
    /// </summary>
    /// <param name="reference">The actor reference to register.</param>
    /// <param name="process">The actor process associated with the reference.</param>
    void Register(IActorReference reference, IActorProcess process);

    /// <summary>
    /// Gets an actor reference by its URI. Returns a dead letter reference if not found.
    /// </summary>
    /// <param name="reference">The URI identifying the actor.</param>
    /// <returns>The actor reference, or a dead letter reference if not found.</returns>
    IActorReference GetReference(Uri reference);

    /// <summary>
    /// Gets the actor process associated with the specified actor reference.
    /// </summary>
    /// <param name="reference">The actor reference to look up.</param>
    /// <returns>The actor process associated with the reference.</returns>
    IActorProcess Get(IActorReference reference);

    /// <summary>
    /// Removes the specified actor reference and its associated process from the registry.
    /// </summary>
    /// <param name="reference">The actor reference to remove.</param>
    void Remove(IActorReference reference);
}

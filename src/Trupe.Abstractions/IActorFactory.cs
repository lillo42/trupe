using System;
using System.Diagnostics.CodeAnalysis;

namespace Trupe.Abstractions;

/// <summary>
/// Defines a factory for creating actor instances within the Trupe actor system.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for instantiating actors based on their type.
/// This abstraction allows for different actor creation strategies, such as:
/// - Direct instantiation using reflection
/// - Dependency injection container integration
/// - Object pooling for actor reuse
/// </remarks>
public interface IActorFactory
{
    /// <summary>
    /// Creates a new actor instance of the specified type.
    /// </summary>
    /// <param name="actorType">The type of actor to create. Must implement <see cref="IActor"/>.</param>
    /// <returns>A new instance of the specified actor type.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="actorType"/> does not implement <see cref="IActor"/>.</exception>
    IActor CreateActor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType
    );
}

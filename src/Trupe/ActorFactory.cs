using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;

namespace Trupe;

/// <summary>
/// An <see cref="IActorFactory"/> implementation that resolves actor instances from the dependency injection container.
/// </summary>
/// <param name="serviceProvider">The service provider used to resolve actor instances.</param>
public class ActorFactory(IServiceProvider serviceProvider) : IActorFactory
{
    /// <inheritdoc />
    public IActor CreateActor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType
    )
    {
        return (IActor)serviceProvider.GetRequiredService(actorType);
    }
}

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Factories;

namespace Trupe.Factories;

/// <summary>
/// An <see cref="IActorFactory"/> implementation that resolves actor instances from the dependency injection container.
/// </summary>
/// <param name="serviceProvider">The service provider used to resolve actor instances.</param>
public class DependencyInjectionActorFactory(IServiceProvider serviceProvider) : IActorFactory
{
    /// <inheritdoc />
    public IActor CreateActor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type actorType
    )
    {
        return (IActor)serviceProvider.GetRequiredService(actorType)!;
    }
}

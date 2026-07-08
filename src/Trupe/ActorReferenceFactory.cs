using System;
using System.Diagnostics.CodeAnalysis;
using Trupe.Abstractions;

namespace Trupe;

/// <summary>
/// Default implementation of <see cref="IActorReferenceFactory"/> that creates
/// <see cref="ActorReferenceProxyProcessor"/> instances and registers them in the process registry.
/// </summary>
/// <param name="provider">The service provider for resolving dependencies.</param>
/// <param name="registry">The actor process registry for registering new references.</param>
public class ActorReferenceFactory(IServiceProvider provider, IActorProcessRegistry registry)
    : IActorReferenceFactory
{
    /// <inheritdoc />
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method",
        Justification = "Actor types are registered at startup and preserved by DI container registration."
    )]
    public IActorReference Create(string name, IActorProcess process)
    {
        var @ref = new ActorReferenceProxyProcessor(
            new Uri($"trupe://localhost/{name}"),
            process.Actor.GetType(),
            provider
        );

        registry.Register(@ref, process);

        return @ref;
    }
}

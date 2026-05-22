using System;
using Trupe.Abstractions;

namespace Trupe;

public class ActorReferenceFactory(IServiceProvider provider, IActorProcessRegistry registry)
    : IActorReferenceFactory
{
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

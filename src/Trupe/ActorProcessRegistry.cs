using System;
using System.Collections.Concurrent;
using Trupe.Abstractions;

namespace Trupe;

public class ActorProcessRegistry : IActorProcessRegistry
{
    public static IActorProcessRegistry Instance { get; } = new ActorProcessRegistry();

    private readonly ConcurrentDictionary<
        Uri,
        (IActorReference reference, IActorProcess process)
    > _pids = new();

    public void Register(IActorReference reference, IActorProcess process)
    {
        _pids.TryAdd(reference.Name, (reference, process));
    }

    public IActorProcess Get(IActorReference reference)
    {
        if (_pids.TryGetValue(reference.Name, out var registery))
        {
            return registery.process;
        }

        throw new System.Exception();
    }

    public void Remove(IActorReference pid)
    {
        _pids.TryRemove(pid.Name, out _);
    }

    public IActorReference GetReference(Uri reference)
    {
        if (_pids.TryGetValue(reference, out var registery))
        {
            return registery.reference;
        }

        return new DeadLetterActorReference(new Uri(reference, "/not-found"));
    }
}

using System;
using System.Collections.Concurrent;
using Trupe.Abstractions;

namespace Trupe;

/// <summary>
/// Default implementation of <see cref="IActorProcessRegistry"/> that maintains a concurrent
/// mapping of actor references to their processes.
/// </summary>
public class ActorProcessRegistry : IActorProcessRegistry
{
    /// <summary>
    /// Gets the singleton instance of the actor process registry.
    /// </summary>
    public static IActorProcessRegistry Instance { get; } = new ActorProcessRegistry();

    private readonly ConcurrentDictionary<
        Uri,
        (IActorReference reference, IActorProcess process)
    > _pids = new();

    /// <inheritdoc />
    public void Register(IActorReference reference, IActorProcess process)
    {
        _pids.TryAdd(reference.Name, (reference, process));
    }

    /// <inheritdoc />
    public IActorProcess Get(IActorReference reference)
    {
        if (_pids.TryGetValue(reference.Name, out var registery))
        {
            return registery.process;
        }

        throw new System.Exception();
    }

    /// <inheritdoc />
    public void Remove(IActorReference pid)
    {
        _pids.TryRemove(pid.Name, out _);
    }

    /// <inheritdoc />
    public IActorReference GetReference(Uri reference)
    {
        if (_pids.TryGetValue(reference, out var registery))
        {
            return registery.reference;
        }

        return new DeadLetterActorReference(new Uri(reference, "/not-found"));
    }
}

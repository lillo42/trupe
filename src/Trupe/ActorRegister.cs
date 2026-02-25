using System;
using System.Collections.Concurrent;
using Trupe.Abstractions;

namespace Trupe;

/// <summary>
/// Thread-safe implementation of <see cref="IActorRegister"/> backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
public class ActorRegister : IActorRegister
{
    /// <summary>
    /// Gets the default shared instance of <see cref="ActorRegister"/>.
    /// </summary>
    public static IActorRegister Instance { get; } = new ActorRegister();

    private readonly ConcurrentDictionary<string, IActorReference> _actors = new();

    /// <inheritdoc />
    public void Register(string id, IActorReference actor)
    {
        if (!_actors.TryAdd(id, actor))
        {
            throw new InvalidOperationException($"An actor with id '{id}' is already registered.");
        }
    }

    /// <inheritdoc />
    public bool TryRegister(string id, IActorReference actor)
    {
        return _actors.TryAdd(id, actor);
    }

    /// <inheritdoc />
    public IActorReference? Get(string id)
    {
        return _actors.TryGetValue(id, out var actor) ? actor : null;
    }

    /// <inheritdoc />
    public bool TryGet(string id, out IActorReference? actor)
    {
        return _actors.TryGetValue(id, out actor);
    }

    /// <inheritdoc />
    public bool Contains(string id)
    {
        return _actors.ContainsKey(id);
    }
}

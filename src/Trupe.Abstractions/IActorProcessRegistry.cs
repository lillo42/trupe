using System;

namespace Trupe.Abstractions;

/// <summary>
/// Provides a registry for looking up and managing actor references by their identifiers.
/// </summary>
public interface IActorProcessRegistry
{
    void Register(IActorReference reference, IActorProcess process);

    IActorReference GetReference(Uri reference);

    IActorProcess Get(IActorReference reference);

    void Remove(IActorReference reference);
}

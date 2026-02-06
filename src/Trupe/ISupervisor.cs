using System.Collections.Generic;
using Trupe.ActorReferences;

namespace Trupe;

/// <summary>
/// Represents a supervisor actor that manages child actors.
/// </summary>
public interface ISupervisor : IActor
{
    /// <summary>
    /// Gets the collection of child actor references managed by this supervisor.
    /// </summary>
    IEnumerable<IActorReference> Children { get; }
}

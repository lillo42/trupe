using System.Collections.Generic;

namespace Trupe.Abstractions.Supervisors;

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

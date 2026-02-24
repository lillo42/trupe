using System.Collections.Generic;
using Trupe.Abstractions.Supervisors;

namespace Trupe.Abstractions.Options;

/// <summary>
/// Configuration options for the root supervisor, defining which child actors to manage.
/// </summary>
public class RootSupervisorOptions
{
    /// <summary>
    /// Gets or sets the list of child specifications that define the actors managed by the root supervisor.
    /// </summary>
    public List<IChildSpecification> Children { get; set; } = [];
}

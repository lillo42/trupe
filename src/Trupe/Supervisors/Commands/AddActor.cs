using Trupe.ActorReferences;

namespace Trupe.Supervisors.Commands;

/// <summary>
/// Command to add a new child actor to a supervisor.
/// </summary>
/// <param name="Specification">The specification defining the actor type and configuration.</param>
/// <param name="Reference">The local actor reference to associate with the new child actor.</param>
public record AddActor(ChildSpecification Specification, LocalActorReference Reference);

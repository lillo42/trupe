using Trupe.Abstractions;
using Trupe.Abstractions.Supervisors;
using Trupe.Abstractions.SystemMessages;

namespace Trupe.Supervisors.Commands;

/// <summary>
/// Command to add a new child actor to a supervisor.
/// </summary>
/// <param name="Specification">The specification defining the actor type and configuration.</param>
/// <param name="Reference">The local actor reference to associate with the new child actor.</param>
public record AddActor(IChildSpecification Specification, IActorReference Reference)
    : IUseSameActorScopeServiceMessage;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Trupe.Abstractions;
using Trupe.Abstractions.Mailboxes;
using Trupe.Abstractions.Supervisors;

namespace Trupe.Supervisors;

/// <summary>
/// Represents the metadata and state of a child actor managed by a supervisor.
/// </summary>
/// <param name="actor">The actor instance.</param>
/// <param name="process">The actor process managing execution.</param>
/// <param name="restartPolicy">The restart policy for this child.</param>
/// <param name="mailboxFactory">Factory function to create mailboxes for this child.</param>
/// <param name="actorType">The original type of the actor, used for recreation during restart.</param>
public class Child(
    IActor actor,
    IActorProcess process,
    RestartPolicy restartPolicy,
    Func<IServiceProvider, IMailbox> mailboxFactory,
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )]
        Type actorType
)
{
    /// <summary>
    /// Gets or sets the actor instance managed by this child entry.
    /// </summary>
    public IActor Actor { get; set; } = actor;

    /// <summary>
    /// Gets or sets the actor process managing execution for this child.
    /// </summary>
    public IActorProcess Process { get; set; } = process;

    /// <summary>
    /// Gets the factory function used to create mailboxes for this child actor.
    /// </summary>
    public Func<IServiceProvider, IMailbox> MailboxFactory { get; } = mailboxFactory;

    /// <summary>
    /// Gets the unique name (URI) of this child actor.
    /// </summary>
    public Uri Name => Actor.Context.Name;

    /// <summary>
    /// Gets the actor reference for communicating with this child actor.
    /// </summary>
    public IActorReference Reference => Actor.Context.Self;

    /// <summary>
    /// Gets the restart policy that determines how this actor is handled after termination or failure.
    /// </summary>
    public RestartPolicy RestartPolicy { get; } = restartPolicy;

    /// <summary>
    /// Gets the original type of the actor, used for recreation during restart.
    /// </summary>
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )]
    public Type ActorType { get; } = actorType;

    /// <summary>
    /// Gets or sets the number of times this actor has been restarted within the current window.
    /// </summary>
    public int RestartCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the timestamp of the last restart, used for restart window calculations.
    /// </summary>
    public DateTimeOffset LastRestartTime { get; set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets a dictionary for storing custom metadata associated with this actor.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the supervised actor is itself a supervisor.
    /// </summary>
    public bool IsSupervisor => Actor is ISupervisor;
}

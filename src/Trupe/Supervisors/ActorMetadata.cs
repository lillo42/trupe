using System;
using System.Collections.Generic;
using Trupe.ActorReferences;
using Trupe.Mailboxes;

namespace Trupe.Supervisors;

/// <summary>
/// Contains metadata and runtime state for a supervised actor instance.
/// </summary>
/// <remarks>
/// This class tracks all information needed by a supervisor to manage an actor's lifecycle,
/// including restart counts, timing, and the actor's runtime components.
/// </remarks>
/// <param name="actor">The actor instance being supervised.</param>
/// <param name="mailbox">The mailbox used for message delivery to the actor.</param>
/// <param name="process">The process managing the actor's message loop.</param>
/// <param name="reference">The reference used to communicate with this actor.</param>
public class ActorMetadata(
    IActor actor,
    IMailbox mailbox,
    ActorProcess process,
    LocalActorReference reference
)
{
    /// <summary>
    /// Gets or sets the actor instance. This may be replaced during restart.
    /// </summary>
    public IActor Actor { get; set; } = actor;

    /// <summary>
    /// Gets or sets the actor process managing the message loop. This may be replaced during restart.
    /// </summary>
    public ActorProcess Process { get; set; } = process;

    /// <summary>
    /// Gets the mailbox used for message delivery to the actor.
    /// </summary>
    public IMailbox Mailbox { get; } = mailbox;

    /// <summary>
    /// Gets the actor reference used to communicate with this actor.
    /// </summary>
    public LocalActorReference Reference { get; } = reference;

    /// <summary>
    /// Gets the original type of the actor, used for recreation during restart.
    /// </summary>
    public Type ActorType { get; } = actor.GetType();

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

using System;
using System.Collections.Generic;
using Trupe.ActorReferences;
using Trupe.Mailboxes;

namespace Trupe.Supervisors;

/// <summary>
/// Represents the metadata and state of a child actor managed by a supervisor.
/// </summary>
/// <param name="actor">The actor instance.</param>
/// <param name="mailbox">The mailbox used for message delivery.</param>
/// <param name="process">The actor process managing the message loop.</param>
/// <param name="reference">The local actor reference for communication.</param>
/// <param name="restartPolicy">The restart policy for this child actor.</param>
public class Child(
    IActor actor,
    IMailbox mailbox,
    ActorProcess process,
    LocalActorReference reference,
    RestartPolicy restartPolicy
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
    /// Gets the restart policy that determines how this actor is handled after termination or failure.
    /// </summary>
    public RestartPolicy RestartPolicy { get; } = restartPolicy;

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
